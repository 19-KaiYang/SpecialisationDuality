using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIGuide : MonoBehaviour
{
    [Header("Circle Area Settings")]
    public float circleRadius = 5f;
    public LayerMask detectionLayers = -1;

    [Header("AI Movement")]
    public Transform[] waypoints;
    public float moveSpeed = 3f;
    public float waitTimeAtWaypoint = 1f;
    public bool loopWaypoints = true;
    public bool reverseOnEnd = false;

    [Header("Dissolve Settings")]
    public float dissolveSpeed = 2f;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public Color gizmoColor = Color.cyan;

    private DualityManager dualityManager;
    private Dictionary<GameObject, DissolveState> objectStates = new Dictionary<GameObject, DissolveState>();
    private Dictionary<GameObject, Coroutine> activeCoroutines = new Dictionary<GameObject, Coroutine>();

    private HashSet<GameObject> objectsInArea = new HashSet<GameObject>();
    private bool lastKnownShadowMode = false;

    // AI Movement variables
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private bool movingForward = true;
    private Coroutine movementCoroutine;

    // Track if the guide should be active in the current mode
    private bool isActiveInCurrentMode = true;

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

        // Determine initial active state
        UpdateActiveState();

        // Start AI movement if waypoints are set and guide is active
        if (waypoints != null && waypoints.Length > 0 && isActiveInCurrentMode)
        {
            // Position at first waypoint
            if (waypoints[0] != null)
                transform.position = waypoints[0].position;

            movementCoroutine = StartCoroutine(AIMovementLoop());
        }
        else if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("No waypoints set for AIGuide AI movement!");
        }
    }

    private void Update()
    {
        // Check if mode has changed
        bool currentShadowMode = dualityManager.IsInShadowMode();
        if (currentShadowMode != lastKnownShadowMode)
        {
            lastKnownShadowMode = currentShadowMode;
            UpdateActiveState();
            HandleModeSwitch();
        }

        // Only perform guide functions if active in current mode
        if (!isActiveInCurrentMode)
            return;

        // Check for objects in circular area
        CheckObjectsInArea();
        CheckForObjectsToRestore();
    }

    private void UpdateActiveState()
    {
        bool inShadow = dualityManager.IsInShadowMode();
        bool wasActive = isActiveInCurrentMode;

        // Determine if guide should be active based on its tag and current mode
        if (gameObject.CompareTag("ShadowOnly"))
        {
            isActiveInCurrentMode = inShadow;
        }
        else if (gameObject.CompareTag("LightOnly"))
        {
            isActiveInCurrentMode = !inShadow;
        }
        else
        {
            // If no specific tag, assume always active
            isActiveInCurrentMode = true;
        }

        // Handle movement coroutine based on active state
        if (isActiveInCurrentMode && !wasActive)
        {
            // Guide became active - start movement if waypoints exist
            if (waypoints != null && waypoints.Length > 0 && movementCoroutine == null)
            {
                movementCoroutine = StartCoroutine(AIMovementLoop());
            }
        }
        else if (!isActiveInCurrentMode && wasActive)
        {
            // Guide became inactive - stop movement
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
                movementCoroutine = null;
            }
        }
    }

    private IEnumerator AIMovementLoop()
    {
        while (isActiveInCurrentMode)
        {
            if (waypoints == null || waypoints.Length == 0)
                yield break;

            // Move to current waypoint
            Transform targetWaypoint = waypoints[currentWaypointIndex];
            if (targetWaypoint != null)
            {
                yield return StartCoroutine(MoveToWaypoint(targetWaypoint.position));

                // Wait at waypoint
                if (waitTimeAtWaypoint > 0)
                {
                    isWaiting = true;
                    yield return new WaitForSeconds(waitTimeAtWaypoint);
                    isWaiting = false;
                }
            }

            // Calculate next waypoint index
            GetNextWaypointIndex();

            // Check if we should still be moving (in case mode changed during movement)
            if (!isActiveInCurrentMode)
                break;
        }

        movementCoroutine = null;
    }

    private IEnumerator MoveToWaypoint(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float distance = Vector3.Distance(startPosition, targetPosition);
        float travelTime = distance / moveSpeed;
        float elapsedTime = 0f;

        while (elapsedTime < travelTime && isActiveInCurrentMode)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / travelTime;
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        if (isActiveInCurrentMode)
        {
            transform.position = targetPosition;
        }
    }

    private void GetNextWaypointIndex()
    {
        if (waypoints.Length <= 1) return;

        if (loopWaypoints)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else if (reverseOnEnd)
        {
            if (movingForward)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length - 1)
                {
                    currentWaypointIndex = waypoints.Length - 1;
                    movingForward = false;
                }
            }
            else
            {
                currentWaypointIndex--;
                if (currentWaypointIndex <= 0)
                {
                    currentWaypointIndex = 0;
                    movingForward = true;
                }
            }
        }
        else
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Length)
            {
                currentWaypointIndex = waypoints.Length - 1;
                // Stop movement
                if (movementCoroutine != null)
                {
                    StopCoroutine(movementCoroutine);
                    movementCoroutine = null;
                }
            }
        }
    }

    private void CheckObjectsInArea()
    {
        // Only check if guide is active in current mode
        if (!isActiveInCurrentMode)
            return;

        // Get all colliders in the circular area
        Collider[] collidersInRange = Physics.OverlapSphere(transform.position, circleRadius, detectionLayers);

        HashSet<GameObject> currentFrameObjects = new HashSet<GameObject>();

        foreach (Collider col in collidersInRange)
        {
            GameObject obj = col.transform.root.gameObject;
            if (!obj.CompareTag("LightOnly") && !obj.CompareTag("ShadowOnly")) continue;

            currentFrameObjects.Add(obj);

            if (!objectsInArea.Contains(obj))
            {
                objectsInArea.Add(obj);
                ProcessObjectInArea(obj);
            }
            else
            {
                ProcessObjectStayInArea(obj);
            }
        }

        // Check for objects that left the area
        List<GameObject> objectsToRemove = new List<GameObject>();
        foreach (GameObject obj in objectsInArea)
        {
            if (!currentFrameObjects.Contains(obj))
            {
                objectsToRemove.Add(obj);
            }
        }

        foreach (GameObject obj in objectsToRemove)
        {
            objectsInArea.Remove(obj);
        }
    }

    private void HandleModeSwitch()
    {
        bool inShadow = dualityManager.IsInShadowMode();

        foreach (var kvp in objectStates)
        {
            GameObject obj = kvp.Key;
            DissolveState state = kvp.Value;

            if (state.isTransitioning) continue;

            bool reveal = ShouldRevealObject(obj, inShadow);
            bool hide = ShouldHideObjectInArea(obj, inShadow);
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

        // Handle guide's own colliders
        BoxCollider[] guideColliders = GetComponentsInChildren<BoxCollider>();
        foreach (var col in guideColliders)
        {
            col.enabled = isActiveInCurrentMode;
        }
    }

    private void CheckForObjectsToRestore()
    {
        // Only restore if guide is active in current mode
        if (!isActiveInCurrentMode)
            return;

        List<GameObject> toRestore = new List<GameObject>();

        foreach (var kvp in objectStates)
        {
            GameObject obj = kvp.Key;
            DissolveState state = kvp.Value;

            if (obj == null || state.isTransitioning || objectsInArea.Contains(obj))
                continue;

            if (!state.hasBeenAffected) continue;

            toRestore.Add(obj);
        }

        foreach (GameObject obj in toRestore)
        {
            RestoreObjectFromArea(obj);
        }
    }

    private void ProcessObjectInArea(GameObject obj)
    {
        bool inShadow = dualityManager.IsInShadowMode();
        if (!objectStates.ContainsKey(obj)) SetupObjectForDissolve(obj);

        DissolveState state = objectStates[obj];
        if (state.hasBeenAffected || state.isTransitioning) return;

        bool reveal = ShouldRevealObject(obj, inShadow);
        bool hide = ShouldHideObjectInArea(obj, inShadow);

        if (reveal || hide)
        {
            state.shouldBeVisible = reveal;
            state.hasBeenAffected = true;
            StartDissolveTransition(obj, state);
        }
    }

    private void ProcessObjectStayInArea(GameObject obj)
    {
        bool inShadow = dualityManager.IsInShadowMode();

        if (!objectStates.ContainsKey(obj))
        {
            SetupObjectForDissolve(obj);
        }

        DissolveState state = objectStates[obj];
        if (state.isTransitioning)
            return;

        bool reveal = ShouldRevealObject(obj, inShadow);
        bool hide = ShouldHideObjectInArea(obj, inShadow);

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

    private void RestoreObjectFromArea(GameObject obj)
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

    private bool ShouldHideObjectInArea(GameObject obj, bool inShadow)
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

        if (!objectsInArea.Contains(obj))
            state.hasBeenAffected = false;

        state.isTransitioning = false;
        activeCoroutines.Remove(obj);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        if (Application.isPlaying && !isActiveInCurrentMode) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, circleRadius);

        if (waypoints != null && waypoints.Length > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }

            if (loopWaypoints && waypoints[waypoints.Length - 1] != null && waypoints[0] != null)
            {
                Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
            }

            // Draw waypoints
            Gizmos.color = Color.red;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawWireCube(waypoints[i].position, Vector3.one * 0.5f);
                }
            }

            if (currentWaypointIndex < waypoints.Length && waypoints[currentWaypointIndex] != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(waypoints[currentWaypointIndex].position, Vector3.one * 0.8f);
            }
        }
    }

    private void OnDestroy()
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }

        foreach (var coroutine in activeCoroutines.Values)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
    }

    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newSpeed);
    }

    public void SetCircleRadius(float newRadius)
    {
        circleRadius = Mathf.Max(0.1f, newRadius);
    }

    public void PauseMovement()
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
    }

    public void ResumeMovement()
    {
        if (movementCoroutine == null && waypoints != null && waypoints.Length > 0 && isActiveInCurrentMode)
        {
            movementCoroutine = StartCoroutine(AIMovementLoop());
        }
    }
}