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

    [Header("Lighting")]
    public GameObject lightSun;
    public GameObject shadowSun;
    // Light lists for smooth transitions
    public List<Light> lightModeLights;
    public List<Light> shadowModeLights;

    [Header("Dissolve Settings")]
    public Shader dissolveShader;  // Reference to the URP_DissolveEffect shader
    public Texture2D dissolveNoiseTexture;
    public Color edgeColor = Color.white;
    public float edgeWidth = 0.01f;
    public bool preserveOriginalColors = true;

    private bool isInShadow = false;
    private bool isTransitioning = false;
    private Coroutine transitionRoutine;

    // Store original materials for restoration
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> instanceMaterials = new Dictionary<Renderer, Material[]>();

    // Store original light intensities
    private Dictionary<Light, float> originalLightIntensities = new Dictionary<Light, float>();

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
        // reference to the dissolve shader
        if (dissolveShader == null)
        {
            dissolveShader = Shader.Find("Custom/URP_DissolveEffect");
            if (dissolveShader == null)
            {
                Debug.LogError("Dissolve shader not found! Make sure to assign it in the inspector.");
                enabled = false;
                return;
            }
        }

        // Cache original materials but don't replace them yet
        CacheMaterialsForObjects(lightModeObjects);
        CacheMaterialsForObjects(shadowModeObjects);

        // Cache original light intensities
        CacheLightIntensities();

        // Initial visibility
        SetInitialVisibility();
    }

    private void CacheLightIntensities()
    {
        // Store original light intensities for light mode lights
        if (lightModeLights != null)
        {
            foreach (Light light in lightModeLights)
            {
                if (light != null && !originalLightIntensities.ContainsKey(light))
                {
                    originalLightIntensities[light] = light.intensity;
                }
            }
        }

        // Store original light intensities for shadow mode lights
        if (shadowModeLights != null)
        {
            foreach (Light light in shadowModeLights)
            {
                if (light != null && !originalLightIntensities.ContainsKey(light))
                {
                    originalLightIntensities[light] = light.intensity;
                    // Initially set shadow lights to 0 intensity
                    light.intensity = 0f;
                }
            }
        }
    }

    private void SetInitialVisibility()
    {
        SetObjectsVisibilityState(lightModeObjects, true);
        SetObjectsVisibilityState(shadowModeObjects, false);

        // Set initial state for post-processing and lighting
        if (lightPostFX != null) lightPostFX.enabled = true;
        if (shadowPostFX != null) shadowPostFX.enabled = false;
        if (lightSun != null) lightSun.SetActive(true);
        if (shadowSun != null) shadowSun.SetActive(false);

        // Set initial light intensities
        SetLightIntensities(1.0f, 0.0f);
    }

    private void SetObjectsVisibilityState(List<GameObject> objects, bool visible)
    {
        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;

            // Keep GameObject active for cone detection
            obj.SetActive(true);

            // Control visibility and collision through components
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            Collider[] colliders = obj.GetComponentsInChildren<Collider>();

            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = visible;
            }

            foreach (Collider collider in colliders)
            {
                // Don't disable trigger colliders as they might be used for other detection
                if (!collider.isTrigger)
                {
                    collider.enabled = visible;
                }
            }
        }
    }

    private void SetLightIntensities(float lightModeIntensityFactor, float shadowModeIntensityFactor)
    {
        // Apply intensity factors to light mode lights
        if (lightModeLights != null)
        {
            foreach (Light light in lightModeLights)
            {
                if (light != null && originalLightIntensities.ContainsKey(light))
                {
                    light.intensity = originalLightIntensities[light] * lightModeIntensityFactor;
                }
            }
        }

        // Apply intensity factors to shadow mode lights
        if (shadowModeLights != null)
        {
            foreach (Light light in shadowModeLights)
            {
                if (light != null && originalLightIntensities.ContainsKey(light))
                {
                    light.intensity = originalLightIntensities[light] * shadowModeIntensityFactor;
                }
            }
        }
    }

    private void CacheMaterialsForObjects(List<GameObject> objects)
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

                // Create but don't apply dissolve materials yet
                Material[] instanceMats = new Material[originalMats.Length];
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

                // Only store references, don't apply materials yet
                originalMaterials[renderer] = originalMats;
                instanceMaterials[renderer] = instanceMats;
            }
        }
    }

    // Enhanced property copying method for URP
    public void CopyMaterialProperties(Material source, Material target)
    {
        // Better texture handling for URP
        if (source.HasProperty(BASE_MAP))
        {
            Texture mainTex = source.GetTexture(BASE_MAP);
            if (mainTex != null && target.HasProperty(BASE_MAP))
            {
                target.SetTexture(BASE_MAP, mainTex);
                Vector2 offset = source.GetTextureOffset(BASE_MAP);
                Vector2 scale = source.GetTextureScale(BASE_MAP);
                target.SetTextureOffset(BASE_MAP, offset);
                target.SetTextureScale(BASE_MAP, scale);
            }
        }
        else if (source.HasProperty("_MainTex"))
        {
            Texture mainTex = source.GetTexture("_MainTex");
            if (mainTex != null && target.HasProperty(BASE_MAP))
            {
                target.SetTexture(BASE_MAP, mainTex);
                Vector2 offset = source.GetTextureOffset("_MainTex");
                Vector2 scale = source.GetTextureScale("_MainTex");
                target.SetTextureOffset(BASE_MAP, offset);
                target.SetTextureScale(BASE_MAP, scale);
            }
        }

        // Better color handling for URP
        if (source.HasProperty(BASE_COLOR))
        {
            Color color = source.GetColor(BASE_COLOR);
            if (target.HasProperty(BASE_COLOR))
            {
                target.SetColor(BASE_COLOR, color);
            }
        }
        else if (source.HasProperty("_Color"))
        {
            Color color = source.GetColor("_Color");
            if (target.HasProperty(BASE_COLOR))
            {
                target.SetColor(BASE_COLOR, color);
            }
        }

        // Copy material rendering mode and properties
        CopyRenderMode(source, target);

        // Copy additional properties
        CopyTextureProperty(source, target, "_BumpMap", "_BumpMap");
        CopyTextureProperty(source, target, "_NormalMap", "_BumpMap");
        CopyTextureProperty(source, target, "_EmissionMap", "_EmissionMap");
        CopyColorProperty(source, target, "_EmissionColor", "_EmissionColor");
        CopyFloatProperty(source, target, "_Metallic", "_Metallic");
        CopyFloatProperty(source, target, "_Glossiness", "_Smoothness");
        CopyFloatProperty(source, target, "_Smoothness", "_Smoothness");
        CopyFloatProperty(source, target, "_GlossMapScale", "_Smoothness");
        CopyFloatProperty(source, target, "_SpecularHighlights", "_SpecularHighlights");
        CopyFloatProperty(source, target, "_GlossyReflections", "_EnvironmentReflections");
        CopyFloatProperty(source, target, "_BumpScale", "_BumpScale");
        CopyFloatProperty(source, target, "_OcclusionStrength", "_OcclusionStrength");
        CopyTextureProperty(source, target, "_OcclusionMap", "_OcclusionMap");
        CopyFloatProperty(source, target, "_Mode", "_Mode");
        CopyFloatProperty(source, target, "_Cutoff", "_Cutoff");
    }

    private void CopyFloatProperty(Material source, Material target, string sourceProp, string targetProp)
    {
        if (source.HasProperty(sourceProp) && target.HasProperty(targetProp))
        {
            float value = source.GetFloat(sourceProp);
            target.SetFloat(targetProp, value);
        }
    }

    private void CopyColorProperty(Material source, Material target, string sourceProp, string targetProp)
    {
        if (source.HasProperty(sourceProp) && target.HasProperty(targetProp))
        {
            Color value = source.GetColor(sourceProp);
            target.SetColor(targetProp, value);
        }
    }

    private void CopyTextureProperty(Material source, Material target, string sourceProp, string targetProp)
    {
        if (source.HasProperty(sourceProp) && target.HasProperty(targetProp))
        {
            Texture tex = source.GetTexture(sourceProp);
            if (tex != null)
            {
                target.SetTexture(targetProp, tex);
                if (source.HasProperty(sourceProp))
                {
                    Vector2 offset = source.GetTextureOffset(sourceProp);
                    Vector2 scale = source.GetTextureScale(sourceProp);
                    target.SetTextureOffset(targetProp, offset);
                    target.SetTextureScale(targetProp, scale);
                }
            }
        }
    }

    private void CopyRenderMode(Material source, Material target)
    {
        if (source.HasProperty("_SrcBlend") && target.HasProperty("_SrcBlend"))
        {
            target.SetFloat("_SrcBlend", source.GetFloat("_SrcBlend"));
        }

        if (source.HasProperty("_DstBlend") && target.HasProperty("_DstBlend"))
        {
            target.SetFloat("_DstBlend", source.GetFloat("_DstBlend"));
        }

        if (source.HasProperty("_ZWrite") && target.HasProperty("_ZWrite"))
        {
            target.SetFloat("_ZWrite", source.GetFloat("_ZWrite"));
        }

        if (source.HasProperty("_Surface") && target.HasProperty("_Surface"))
        {
            target.SetFloat("_Surface", source.GetFloat("_Surface"));
        }

        if (source.HasProperty("_Blend") && target.HasProperty("_Blend"))
        {
            target.SetFloat("_Blend", source.GetFloat("_Blend"));
        }

        target.renderQueue = source.renderQueue;

        string[] keywords = source.shaderKeywords;
        foreach (string keyword in keywords)
        {
            target.EnableKeyword(keyword);
        }

        target.SetOverrideTag("RenderType", source.GetTag("RenderType", false, ""));
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !isTransitioning)
        {
            transitionRoutine = StartCoroutine(Transition());
        }
    }

    private IEnumerator Transition()
    {
        isTransitioning = true;

        // Apply dissolve materials when starting transition
        ApplyDissolveMaterials();

        // Activate all objects and their components for transition
        SetAllObjectsForTransition(true);

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

            // Smoothly transition light intensities
            float lightFactor = isInShadow ? t : 1 - t;
            float shadowFactor = isInShadow ? 1 - t : t;
            SetLightIntensities(lightFactor, shadowFactor);

            yield return null;
        }

        shadowMode = targetValue;
        isInShadow = !isInShadow;

        // Final state - set proper visibility
        SetObjectsVisibilityState(lightModeObjects, !isInShadow);
        SetObjectsVisibilityState(shadowModeObjects, isInShadow);

        // Post FX and lighting
        if (lightPostFX != null) lightPostFX.enabled = !isInShadow;
        if (shadowPostFX != null) shadowPostFX.enabled = isInShadow;
        if (lightSun != null) lightSun.SetActive(!isInShadow);
        if (shadowSun != null) shadowSun.SetActive(isInShadow);

        // Set final light states
        SetLightIntensities(!isInShadow ? 1.0f : 0.0f, isInShadow ? 1.0f : 0.0f);

        // Restore original materials when transition is complete
        RestoreOriginalMaterials();

        isTransitioning = false;
        transitionRoutine = null;
    }

    private void SetAllObjectsForTransition(bool enableComponents)
    {
        SetObjectsTransitionState(lightModeObjects, enableComponents);
        SetObjectsTransitionState(shadowModeObjects, enableComponents);
    }

    private void SetObjectsTransitionState(List<GameObject> objects, bool enableComponents)
    {
        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;

            obj.SetActive(true);

            if (enableComponents)
            {
                // Enable all renderers and colliders for transition
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                Collider[] colliders = obj.GetComponentsInChildren<Collider>();

                foreach (Renderer renderer in renderers)
                {
                    renderer.enabled = true;
                }

                foreach (Collider collider in colliders)
                {
                    if (!collider.isTrigger)
                    {
                        collider.enabled = true;
                    }
                }
            }
        }
    }

    private void ApplyDissolveMaterials()
    {
        foreach (var kvp in instanceMaterials)
        {
            Renderer renderer = kvp.Key;
            if (renderer != null)
            {
                renderer.materials = kvp.Value;
            }
        }
    }

    private void RestoreOriginalMaterials()
    {
        foreach (var kvp in originalMaterials)
        {
            Renderer renderer = kvp.Key;
            if (renderer != null)
            {
                renderer.materials = kvp.Value;
            }
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
                if (!instanceMaterials.ContainsKey(renderer))
                {
                    continue;
                }

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
        RestoreOriginalMaterials();
    }

    public bool IsInShadowMode()
    {
        return isInShadow;
    }
}