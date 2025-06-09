using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConeLightReveal : MonoBehaviour
{
    [Header("Light Control")]
    public bool isLightActive = true; // Can be controlled by buttons

    [Header("Dissolve Settings")]
    public float dissolveSpeed = 2f;

    [Header("Visual Feedback")]
    public GameObject lightVisualEffect; // Optional: assign a light or particle effect
    public Material activeMaterial; // Optional: material when light is on
    public Material inactiveMaterial; // Optional: material when light is off

    [Header("Debug")]
    public bool showDebugGizmos = true;

    private DualityManager dualityManager;
    private Dictionary<GameObject, DissolveState> objectStates = new Dictionary<GameObject, DissolveState>();
    private Dictionary<GameObject, Coroutine> activeCoroutines = new Dictionary<GameObject, Coroutine>();

    private HashSet<GameObject> objectsInTrigger = new HashSet<GameObject>();
    private bool lastKnownShadowMode = false;
    private bool lastLightActiveState = true;

    private class DissolveState
    {
        public bool shouldBeVisible;
        public float currentDissolve;
        public Material[] materials;
        public Material[] originalMaterials;
        public Renderer renderer;
        public bool isTransitioning;
        public bool hasBeenAffected;
        public List<Collider> originalColliders = new List<Collider>();
    }

    private void Start()
    {
        dualityManager = FindObjectOfType<DualityManager>();
        if (dualityManager == null)
        {
            Debug.LogError("DualityManager not found!");
            enabled = false;
            return;
        }

        lastKnownShadowMode = dualityManager.IsInShadowMode();
        lastLightActiveState = isLightActive;

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Debug.LogError("ConeLightReveal needs a Collider component set as trigger!");
            enabled = false;
            return;
        }

        UpdateLightVisuals();
    }

    private void Update()
    {
        // Check if mode has changed
        bool currentShadowMode = dualityManager.IsInShadowMode();
        if (currentShadowMode != lastKnownShadowMode)
        {
            lastKnownShadowMode = currentShadowMode;
            HandleModeSwitch();
        }

        // Check if light active state has changed
        if (isLightActive != lastLightActiveState)
        {
            lastLightActiveState = isLightActive;
            HandleLightToggle();
            UpdateLightVisuals();
        }

        CheckForObjectsToRestore();
    }

    public void ToggleLight()
    {
        isLightActive = !isLightActive;
        Debug.Log($"Cone light {gameObject.name} toggled to: {(isLightActive ? "ON" : "OFF")}");
    }

    public void SetLightActive(bool active)
    {
        isLightActive = active;
        Debug.Log($"Cone light {gameObject.name} set to: {(isLightActive ? "ON" : "OFF")}");
    }

    private void UpdateLightVisuals()
    {
        // Update visual feedback
        if (lightVisualEffect != null)
        {
            Light lightComp = lightVisualEffect.GetComponent<Light>();
            if (lightComp != null)
            {
                lightComp.enabled = isLightActive;
            }
        }

        // Update material if specified
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            if (isLightActive && activeMaterial != null)
            {
                rend.material = activeMaterial;
            }
            else if (!isLightActive && inactiveMaterial != null)
            {
                rend.material = inactiveMaterial;
            }
        }
    }

    private void HandleLightToggle()
    {
        // When light is turned off, restore all affected objects to normal state
        if (!isLightActive)
        {
            RestoreAllObjectsToNormal();
        }
        else
        {
            // When light is turned on, re-evaluate all objects in trigger
            HandleModeSwitch();
        }
    }

    private void RestoreAllObjectsToNormal()
    {
        bool inShadow = dualityManager.IsInShadowMode();

        foreach (var kvp in objectStates)
        {
            GameObject obj = kvp.Key;
            DissolveState state = kvp.Value;

            if (obj == null || state.isTransitioning) continue;

            // Return to normal visibility for current mode
            bool normalVisible = (obj.CompareTag("LightOnly") && !inShadow) || (obj.CompareTag("ShadowOnly") && inShadow);

            if (state.shouldBeVisible != normalVisible)
            {
                state.shouldBeVisible = normalVisible;
                StartDissolveTransition(obj, state);
            }
        }
    }

    private void HandleModeSwitch()
    {
        if (!isLightActive) return; // Don't process if light is off

        bool inShadow = dualityManager.IsInShadowMode();

        foreach (var kvp in objectStates)
        {
            GameObject obj = kvp.Key;
            DissolveState state = kvp.Value;

            if (state.isTransitioning) continue;

            bool reveal = ShouldRevealObject(obj, inShadow);
            bool hide = ShouldHideObjectInCone(obj, inShadow);
            bool targetVisible = state.shouldBeVisible;

            if (reveal)
            {
                targetVisible = true;
            }
            else if (hide)
            {
                targetVisible = false;
            }
            else
            {
                targetVisible = (obj.CompareTag("LightOnly") && !inShadow) || (obj.CompareTag("ShadowOnly") && inShadow);
            }

            if (state.shouldBeVisible != targetVisible)
            {
                state.shouldBeVisible = targetVisible;
                StartDissolveTransition(obj, state);
            }
        }
    }

    private void CheckForObjectsToRestore()
    {
        List<GameObject> toRestore = new List<GameObject>();

        foreach (var kvp in objectStates)
        {
            GameObject obj = kvp.Key;
            DissolveState state = kvp.Value;

            if (obj == null || state.isTransitioning || objectsInTrigger.Contains(obj))
                continue;

            if (!state.hasBeenAffected) continue;

            toRestore.Add(obj);
        }

        foreach (GameObject obj in toRestore)
        {
            RestoreObjectFromTrigger(obj);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isLightActive) return; // Don't process if light is off

        GameObject obj = other.transform.root.gameObject;
        if (!obj.CompareTag("LightOnly") && !obj.CompareTag("ShadowOnly")) return;

        objectsInTrigger.Add(obj);
        ProcessObjectInTrigger(obj);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isLightActive) return; // Don't process if light is off

        GameObject obj = other.transform.root.gameObject;
        if (!obj.CompareTag("LightOnly") && !obj.CompareTag("ShadowOnly")) return;

        objectsInTrigger.Add(obj);

        bool inShadow = dualityManager.IsInShadowMode();

        if (!objectStates.ContainsKey(obj))
        {
            SetupObjectForDissolve(obj);
        }

        DissolveState state = objectStates[obj];
        if (state.isTransitioning) return;

        bool reveal = ShouldRevealObject(obj, inShadow);
        bool hide = ShouldHideObjectInCone(obj, inShadow);

        bool shouldChangeVisibility = false;
        bool targetVisible = state.shouldBeVisible;

        if (reveal)
        {
            targetVisible = true;
            shouldChangeVisibility = state.shouldBeVisible != true || !state.hasBeenAffected;
        }
        else if (hide)
        {
            targetVisible = false;
            shouldChangeVisibility = state.shouldBeVisible != false || !state.hasBeenAffected;
        }

        if (shouldChangeVisibility)
        {
            state.shouldBeVisible = targetVisible;
            state.hasBeenAffected = true;
            StartDissolveTransition(obj, state);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject obj = other.transform.root.gameObject;

        if (!objectsInTrigger.Contains(obj)) return;

        objectsInTrigger.Remove(obj);
    }

    private void ProcessObjectInTrigger(GameObject obj)
    {
        bool inShadow = dualityManager.IsInShadowMode();
        if (!objectStates.ContainsKey(obj)) SetupObjectForDissolve(obj);

        DissolveState state = objectStates[obj];
        if (state.hasBeenAffected || state.isTransitioning) return;

        bool reveal = ShouldRevealObject(obj, inShadow);
        bool hide = ShouldHideObjectInCone(obj, inShadow);

        if (reveal || hide)
        {
            state.shouldBeVisible = reveal;
            state.hasBeenAffected = true;
            StartDissolveTransition(obj, state);
        }
    }

    private void RestoreObjectFromTrigger(GameObject obj)
    {
        if (!objectStates.ContainsKey(obj)) return;

        DissolveState state = objectStates[obj];
        if (state.isTransitioning) return;

        bool inShadow = dualityManager.IsInShadowMode();
        bool normalVisible = (obj.CompareTag("LightOnly") && !inShadow) || (obj.CompareTag("ShadowOnly") && inShadow);

        bool targetVisible = normalVisible;

        if (state.shouldBeVisible != targetVisible)
        {
            state.shouldBeVisible = targetVisible;
            StartDissolveTransition(obj, state);
        }
    }

    private bool ShouldRevealObject(GameObject obj, bool inShadow)
    {
        return (!inShadow && obj.CompareTag("ShadowOnly")) || (inShadow && obj.CompareTag("LightOnly"));
    }

    private bool ShouldHideObjectInCone(GameObject obj, bool inShadow)
    {
        return (!inShadow && obj.CompareTag("LightOnly")) || (inShadow && obj.CompareTag("ShadowOnly"));
    }

    private void SetupObjectForDissolve(GameObject obj)
    {
        Renderer rend = obj.GetComponent<Renderer>();

        bool inShadow = dualityManager.IsInShadowMode();
        bool initiallyVisible = (obj.CompareTag("LightOnly") && !inShadow) || (obj.CompareTag("ShadowOnly") && inShadow);

        DissolveState state = new DissolveState();
        state.renderer = rend;
        state.originalMaterials = rend.sharedMaterials;
        state.shouldBeVisible = initiallyVisible;
        state.currentDissolve = initiallyVisible ? 0f : 1f;
        state.isTransitioning = false;
        state.hasBeenAffected = false;

        Collider[] cols = obj.GetComponentsInChildren<Collider>();
        foreach (var col in cols)
        {
            if (!col.isTrigger) state.originalColliders.Add(col);
        }

        objectStates[obj] = state;
    }

    private void StartDissolveTransition(GameObject obj, DissolveState state)
    {
        if (activeCoroutines.ContainsKey(obj) && activeCoroutines[obj] != null)
        {
            StopCoroutine(activeCoroutines[obj]);
        }
        activeCoroutines[obj] = StartCoroutine(DissolveTransition(obj, state));
    }

    private IEnumerator DissolveTransition(GameObject obj, DissolveState state)
    {
        state.isTransitioning = true;

        if (state.renderer != null)
            state.renderer.enabled = true;

        Material[] dissolveMats = new Material[state.originalMaterials.Length];
        for (int i = 0; i < state.originalMaterials.Length; i++)
        {
            Material mat = new Material(dualityManager.dissolveShader);
            dualityManager.CopyMaterialProperties(state.originalMaterials[i], mat);
            if (dualityManager.dissolveNoiseTexture != null)
                mat.SetTexture("_DissolveTexture", dualityManager.dissolveNoiseTexture);
            mat.SetColor("_EdgeColor", dualityManager.edgeColor);
            mat.SetFloat("_EdgeWidth", dualityManager.edgeWidth);
            mat.SetFloat("_PreserveOriginalColor", dualityManager.preserveOriginalColors ? 1f : 0f);
            mat.SetFloat("_Dissolve", state.currentDissolve);
            dissolveMats[i] = mat;
        }

        state.renderer.materials = dissolveMats;
        state.materials = dissolveMats;

        float target = state.shouldBeVisible ? 0f : 1f;
        float start = state.currentDissolve;
        float t = 0f;
        float duration = 1f / dissolveSpeed;

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            float dissolveVal = Mathf.Lerp(start, target, progress);

            foreach (Material mat in state.materials)
            {
                if (mat != null && mat.HasProperty("_Dissolve"))
                    mat.SetFloat("_Dissolve", dissolveVal);
            }
            yield return null;
        }

        state.currentDissolve = target;

        if (state.shouldBeVisible)
        {
            if (state.originalMaterials != null && state.renderer != null)
            {
                state.renderer.materials = state.originalMaterials;
            }

            if (state.materials != null)
            {
                foreach (Material mat in state.materials)
                {
                    if (mat != null) DestroyImmediate(mat);
                }
                state.materials = null;
            }
        }
        else
        {
            if (state.renderer != null)
            {
                state.renderer.enabled = false;
            }
        }

        if (state.originalColliders != null)
        {
            foreach (var col in state.originalColliders)
            {
                if (col != null)
                {
                    col.enabled = state.shouldBeVisible;
                }
            }
        }

        if (!objectsInTrigger.Contains(obj))
            state.hasBeenAffected = false;

        state.isTransitioning = false;
        activeCoroutines.Remove(obj);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.color = isLightActive ? Color.yellow : Color.gray;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }

    private void OnDestroy()
    {
        foreach (var coroutine in activeCoroutines.Values)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
    }
}