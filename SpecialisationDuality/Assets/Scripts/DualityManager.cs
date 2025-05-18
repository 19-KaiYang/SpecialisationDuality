using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // Added for URP support

public class DualityManager : MonoBehaviour
{
    [Header("Toggle Key")]
    public KeyCode toggleKey = KeyCode.Q;

    [Header("Transition Settings")]
    [Range(0f, 1f)] public float shadowMode = 0f;
    public float transitionDuration = 1.5f;

    [Header("World Object Groups")]
    public List<GameObject> lightModeObjects;
    public List<GameObject> shadowModeObjects;

    [Header("Post-Processing Volumes")]
    public Volume lightPostFX;
    public Volume shadowPostFX;

    [Header("Lighting (Optional)")]
    public GameObject lightSun;
    public GameObject shadowSun;

    [Header("Dissolve Settings")]
    public Shader dissolveShader;  // Reference to the URP_DissolveEffect shader
    public Texture2D dissolveNoiseTexture;
    public Color edgeColor = Color.white;
    public float edgeWidth = 0.01f;
    public bool preserveOriginalColors = true;

    private bool isInShadow = false;
    private Coroutine transitionRoutine;

    // Store original materials for restoration
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> instanceMaterials = new Dictionary<Renderer, Material[]>();

    // Material property names - Updated for URP
    private static readonly string DISSOLVE_AMOUNT = "_Dissolve";
    private static readonly string DISSOLVE_TEXTURE = "_DissolveTexture";
    private static readonly string EDGE_COLOR = "_EdgeColor";
    private static readonly string EDGE_WIDTH = "_EdgeWidth";
    private static readonly string PRESERVE_COLOR = "_PreserveOriginalColor";
    private static readonly string BASE_MAP = "_BaseMap";         // URP equivalent of _MainTex
    private static readonly string BASE_COLOR = "_BaseColor";     // URP equivalent of _Color

    private void Start()
    {
        // Make sure we have a reference to the dissolve shader
        if (dissolveShader == null)
        {
            dissolveShader = Shader.Find("Custom/URP_DissolveEffect");
            if (dissolveShader == null)
            {
                Debug.LogError("URP Dissolve Shader not found! Make sure it's properly imported.");
                enabled = false;
                return;
            }
        }

        // Setup materials
        SetupMaterialsForObjects(lightModeObjects, true);
        SetupMaterialsForObjects(shadowModeObjects, false);

        // Initial visibility
        SetInitialVisibility();
    }

    private void SetInitialVisibility()
    {
        foreach (GameObject obj in lightModeObjects)
        {
            if (obj == null) continue;
            obj.SetActive(true);
        }

        foreach (GameObject obj in shadowModeObjects)
        {
            if (obj == null) continue;
            obj.SetActive(false);
        }

        // Set initial state for post-processing and lighting
        if (lightPostFX != null) lightPostFX.enabled = true;
        if (shadowPostFX != null) shadowPostFX.enabled = false;
        if (lightSun != null) lightSun.SetActive(true);
        if (shadowSun != null) shadowSun.SetActive(false);
    }

    private void SetupMaterialsForObjects(List<GameObject> objects, bool isLightGroup)
    {
        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                // Skip if already processed
                if (originalMaterials.ContainsKey(renderer)) continue;

                // Store original materials
                Material[] originalMats = renderer.sharedMaterials;
                Material[] instanceMats = new Material[originalMats.Length];

                // Create instances of each material
                for (int i = 0; i < originalMats.Length; i++)
                {
                    // Create a new material with our dissolve shader
                    Material instanceMat = new Material(dissolveShader);

                    // Copy properties from original material to preserve appearance
                    CopyMaterialProperties(originalMats[i], instanceMat);

                    // Set dissolve-specific properties
                    if (dissolveNoiseTexture != null)
                        instanceMat.SetTexture(DISSOLVE_TEXTURE, dissolveNoiseTexture);
                    instanceMat.SetColor(EDGE_COLOR, edgeColor);
                    instanceMat.SetFloat(EDGE_WIDTH, edgeWidth);
                    instanceMat.SetFloat(PRESERVE_COLOR, preserveOriginalColors ? 1f : 0f);

                    instanceMats[i] = instanceMat;
                }

                // Store references
                originalMaterials[renderer] = originalMats;
                instanceMaterials[renderer] = instanceMats;

                // Apply instance materials
                renderer.materials = instanceMats;
            }
        }
    }

    // Helper method to copy relevant properties from original material - Updated for URP
    private void CopyMaterialProperties(Material source, Material target)
    {
        // Handle main texture and color - accounting for both Built-in and URP naming
        if (source.HasProperty("_MainTex"))
        {
            Texture mainTex = source.GetTexture("_MainTex");
            if (mainTex != null && target.HasProperty(BASE_MAP))
                target.SetTexture(BASE_MAP, mainTex);
        }
        else if (source.HasProperty(BASE_MAP))
        {
            Texture mainTex = source.GetTexture(BASE_MAP);
            if (mainTex != null && target.HasProperty(BASE_MAP))
                target.SetTexture(BASE_MAP, mainTex);
        }

        // Handle color
        if (source.HasProperty("_Color"))
        {
            Color color = source.GetColor("_Color");
            if (target.HasProperty(BASE_COLOR))
                target.SetColor(BASE_COLOR, color);
        }
        else if (source.HasProperty(BASE_COLOR))
        {
            Color color = source.GetColor(BASE_COLOR);
            if (target.HasProperty(BASE_COLOR))
                target.SetColor(BASE_COLOR, color);
        }

        // Copy metallic and smoothness properties
        CopyFloatProperty(source, target, "_Metallic", "_Metallic");
        CopyFloatProperty(source, target, "_Glossiness", "_Smoothness");
        CopyFloatProperty(source, target, "_Smoothness", "_Smoothness");

        // Copy normal map if available
        CopyTextureProperty(source, target, "_BumpMap", "_BumpMap");
        CopyTextureProperty(source, target, "_NormalMap", "_BumpMap");

        // Copy texture tiling and offset
        if (source.HasProperty("_MainTex") && target.HasProperty(BASE_MAP))
        {
            Vector2 offset = source.GetTextureOffset("_MainTex");
            Vector2 scale = source.GetTextureScale("_MainTex");
            target.SetTextureOffset(BASE_MAP, offset);
            target.SetTextureScale(BASE_MAP, scale);
        }
        else if (source.HasProperty(BASE_MAP) && target.HasProperty(BASE_MAP))
        {
            Vector2 offset = source.GetTextureOffset(BASE_MAP);
            Vector2 scale = source.GetTextureScale(BASE_MAP);
            target.SetTextureOffset(BASE_MAP, offset);
            target.SetTextureScale(BASE_MAP, scale);
        }
    }

    // Helper to copy a float property between materials
    private void CopyFloatProperty(Material source, Material target, string sourceProp, string targetProp)
    {
        if (source.HasProperty(sourceProp) && target.HasProperty(targetProp))
        {
            target.SetFloat(targetProp, source.GetFloat(sourceProp));
        }
    }

    // Helper to copy a texture property between materials
    private void CopyTextureProperty(Material source, Material target, string sourceProp, string targetProp)
    {
        if (source.HasProperty(sourceProp) && target.HasProperty(targetProp))
        {
            Texture tex = source.GetTexture(sourceProp);
            if (tex != null)
                target.SetTexture(targetProp, tex);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (transitionRoutine == null)
                transitionRoutine = StartCoroutine(Transition());
        }
    }

    private IEnumerator Transition()
    {
        // Activate all objects
        SetAllObjectsActive(true);

        float time = 0f;
        float startValue = isInShadow ? 1f : 0f;
        float targetValue = isInShadow ? 0f : 1f;

        // Enable both light and shadow volumes temporarily
        if (lightPostFX != null) lightPostFX.enabled = true;
        if (shadowPostFX != null) shadowPostFX.enabled = true;
        if (lightSun != null) lightSun.SetActive(true);
        if (shadowSun != null) shadowSun.SetActive(true);

        while (time < transitionDuration)
        {
            time += Time.deltaTime;
            float t = time / transitionDuration;
            shadowMode = Mathf.Lerp(startValue, targetValue, t);

            ApplyDissolveToObjects(lightModeObjects, shadowMode, true);
            ApplyDissolveToObjects(shadowModeObjects, shadowMode, false);

            yield return null;
        }

        shadowMode = targetValue;
        isInShadow = !isInShadow;

        // Final state - deactivate fully dissolved objects for performance
        DeactivateDissolvedObjects();

        // Post FX and lighting
        if (lightPostFX != null) lightPostFX.enabled = !isInShadow;
        if (shadowPostFX != null) shadowPostFX.enabled = isInShadow;
        if (lightSun != null) lightSun.SetActive(!isInShadow);
        if (shadowSun != null) shadowSun.SetActive(isInShadow);

        transitionRoutine = null;
    }

    private void SetAllObjectsActive(bool active)
    {
        foreach (GameObject obj in lightModeObjects)
        {
            if (obj != null) obj.SetActive(active);
        }

        foreach (GameObject obj in shadowModeObjects)
        {
            if (obj != null) obj.SetActive(active);
        }
    }

    private void DeactivateDissolvedObjects()
    {
        // Deactivate objects that are fully dissolved
        foreach (GameObject obj in lightModeObjects)
        {
            if (obj != null) obj.SetActive(!isInShadow);
        }

        foreach (GameObject obj in shadowModeObjects)
        {
            if (obj != null) obj.SetActive(isInShadow);
        }
    }

    private void ApplyDissolveToObjects(List<GameObject> objects, float shadowValue, bool isLightGroup)
    {
        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;

            float dissolve = isLightGroup ? shadowValue : 1f - shadowValue;

            // Update materials
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (!instanceMaterials.ContainsKey(renderer)) continue;

                Material[] materials = instanceMaterials[renderer];
                foreach (Material mat in materials)
                {
                    if (mat.HasProperty(DISSOLVE_AMOUNT))
                    {
                        mat.SetFloat(DISSOLVE_AMOUNT, dissolve);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        // Restore original materials to prevent memory leaks
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
    }
}