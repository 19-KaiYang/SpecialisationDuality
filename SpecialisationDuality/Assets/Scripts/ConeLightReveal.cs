using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConeLightReveal : MonoBehaviour
{
    [Header("Dissolve Settings")]
    public float dissolveSpeed = 2f;

    [Header("Debug")]
    public bool showDebugGizmos = true;

    private DualityManager dualityManager;
    private Dictionary<GameObject, DissolveState> objectStates = new Dictionary<GameObject, DissolveState>();
    private Dictionary<GameObject, Coroutine> activeCoroutines = new Dictionary<GameObject, Coroutine>();

    private HashSet<GameObject> objectsInTrigger = new HashSet<GameObject>();
    private bool lastKnownShadowMode = false; // Track mode changes

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

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Debug.LogError("ConeLightReveal needs a Collider component set as trigger!");
            enabled = false;
            return;
        }
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

        CheckForObjectsToRestore();
    }

    private void HandleModeSwitch()
    {
        bool inShadow = dualityManager.IsInShadowMode();

        foreach (var kvp in objectStates)
        {
            GameObject obj = kvp.Key;
            DissolveState state = kvp.Value;

            if (state.isTransitioning) continue;

            // These 2 booleans determine visibility
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
                // Neither revealed nor hidden, restore to normal mode visibility
                targetVisible = (obj.CompareTag("LightOnly") && !inShadow) || (obj.CompareTag("ShadowOnly") && inShadow);
            }

            // Only trigger dissolve if visibility changes
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
        GameObject obj = other.transform.root.gameObject;
        if (!obj.CompareTag("LightOnly") && !obj.CompareTag("ShadowOnly")) return;


        objectsInTrigger.Add(obj);
        ProcessObjectInTrigger(obj);
    }

    private void OnTriggerStay(Collider other)
    {
        GameObject obj = other.transform.root.gameObject;
        if (!obj.CompareTag("LightOnly") && !obj.CompareTag("ShadowOnly")) return;


        objectsInTrigger.Add(obj);

        bool inShadow = dualityManager.IsInShadowMode();

        if (!objectStates.ContainsKey(obj))
        {
            SetupObjectForDissolve(obj);
        }

        DissolveState state = objectStates[obj];
        if (state.isTransitioning)
            return;

        // Check both reveal and hide cases independently
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

        // When light leaves, object should return to its normal state for the current mode
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
        // DON'T force enable the renderer here - let the dissolve shader handle visibility

        bool inShadow = dualityManager.IsInShadowMode();

        // Determine initial visibility based on current mode
        bool initiallyVisible = (obj.CompareTag("LightOnly") && !inShadow) || (obj.CompareTag("ShadowOnly") && inShadow);

        DissolveState state = new DissolveState();
        state.renderer = rend;
        state.originalMaterials = rend.sharedMaterials;
        state.shouldBeVisible = initiallyVisible;
        state.currentDissolve = initiallyVisible ? 0f : 1f; // Set dissolve based on visibility
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

        // Ensure renderer is enabled for dissolve effect to work
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

        // Always update the current dissolve value
        state.currentDissolve = target;

        // Handle final state based on whether object should be visible
        if (state.shouldBeVisible)
        {
            // Object should be visible - restore original materials
            if (state.originalMaterials != null && state.renderer != null)
            {
                state.renderer.materials = state.originalMaterials;
                // Keep renderer enabled since object is visible
            }

            // Clean up dissolve materials
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
            // Object should be hidden - disable renderer only after dissolve is complete
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

        // Only reset hasBeenAffected if object is no longer in trigger
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
            Gizmos.color = Color.yellow;
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