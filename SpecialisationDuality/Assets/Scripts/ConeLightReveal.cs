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
        CheckForObjectsToRestore();
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
        GameObject obj = other.gameObject;
        if (!obj.CompareTag("LightOnly") && !obj.CompareTag("ShadowOnly")) return;

        objectsInTrigger.Add(obj);
        ProcessObjectInTrigger(obj);
    }

    private void OnTriggerStay(Collider other)
    {
        GameObject obj = other.gameObject;
        if (!obj.CompareTag("LightOnly") && !obj.CompareTag("ShadowOnly")) return;

        objectsInTrigger.Add(obj);

        bool inShadow = dualityManager.IsInShadowMode();
        bool reveal = ShouldRevealObject(obj, inShadow);
        bool hide = ShouldHideObjectInCone(obj, inShadow);
        bool desiredVisible = reveal;

        if (!objectStates.ContainsKey(obj)) SetupObjectForDissolve(obj);

        DissolveState state = objectStates[obj];
        if (state.isTransitioning) return;
        if (state.shouldBeVisible == desiredVisible && state.hasBeenAffected) return;

        state.shouldBeVisible = desiredVisible;
        state.hasBeenAffected = true;
        StartDissolveTransition(obj, state);
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject obj = other.gameObject;
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

        bool targetVisible = ShouldRevealObject(obj, inShadow) ? false : normalVisible;

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
        if (rend == null) return;

        DissolveState state = new DissolveState();
        state.renderer = rend;
        state.originalMaterials = rend.sharedMaterials;
        state.shouldBeVisible = true;
        state.currentDissolve = 0f;
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
                if (mat.HasProperty("_Dissolve"))
                    mat.SetFloat("_Dissolve", dissolveVal);
            }
            yield return null;
        }

        if (state.shouldBeVisible && state.originalMaterials != null)
        {
            state.renderer.materials = state.originalMaterials;
        }

        if (!objectsInTrigger.Contains(obj))
            state.hasBeenAffected = false;

        state.currentDissolve = target;
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
