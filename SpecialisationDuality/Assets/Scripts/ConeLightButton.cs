using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConeLightButton : MonoBehaviour
{
    [Header("Button Settings")]
    public float interactionRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Target Cone Lights")]
    public List<ConeLightReveal> targetConeLights = new List<ConeLightReveal>();

    [Header("Light Control Options")]
    [Tooltip("If true, all lights toggle together. If false, they cycle through states.")]
    public bool toggleAllTogether = true;

    [Tooltip("Only used when toggleAllTogether is false. Cycles through: All Off -> Light 1 -> Light 2 -> Both On")]
    public bool useCycleMode = false;

 

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip buttonPressSound;

    private Transform player;
    private bool playerInRange = false;
    private Renderer buttonRenderer;
    private int currentCycleState = 0; // 0: all off, 1: first light, 2: second light, 3: all on

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        buttonRenderer = GetComponent<Renderer>();

        if (player == null)
            Debug.LogError("Player not found! Make sure player has 'Player' tag.");

        // Remove any null references
        targetConeLights.RemoveAll(light => light == null);

        if (targetConeLights.Count == 0)
            Debug.LogWarning($"Button {gameObject.name} has no target cone lights assigned!");

       

        // Initialize cycle state based on current light states
        if (useCycleMode && !toggleAllTogether)
        {
            UpdateCycleStateFromLights();
        }
    }

    private void Update()
    {
        if (player == null) return;

        // Check if player is in range
        float distance = Vector3.Distance(transform.position, player.position);
        playerInRange = distance <= interactionRange;

        // Handle interaction
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            if (toggleAllTogether)
            {
                ToggleAllLights();
            }
            else if (useCycleMode)
            {
                CycleThroughLights();
            }
            else
            {
                ToggleAllLights(); 
            }
        }
    }
    private void ToggleAllLights()
    {
        if (targetConeLights.Count == 0) return;

        foreach (var light in targetConeLights)
            light.ToggleLight();

        
      
    }

    private void CycleThroughLights()
    {
        if (targetConeLights.Count == 0) return;

        // States: 0 = ALL OFF, 1..N = ONLY that index ON
        currentCycleState = (currentCycleState + 1) % (targetConeLights.Count + 1);

        // Turn off everything first
        foreach (var light in targetConeLights)
            light.SetLightActive(false);

        // If 1..N, turn on that one
        if (currentCycleState > 0 && currentCycleState <= targetConeLights.Count)
            targetConeLights[currentCycleState - 1].SetLightActive(true);

       
        PlayButtonSound();
        Debug.Log($"[{name}] Cycle state: {currentCycleState}");
    }

    private void UpdateCycleStateFromLights()
    {
        if (targetConeLights.Count == 0) return;

        int activeLights = 0;
        int lastActiveLightIndex = -1;

        for (int i = 0; i < targetConeLights.Count; i++)
        {
            if (targetConeLights[i].isLightActive)
            {
                activeLights++;
                lastActiveLightIndex = i;
            }
        }

        if (activeLights == 0)
        {
            currentCycleState = targetConeLights.Count; 
        }
        else if (activeLights == 1)
        {
            currentCycleState = lastActiveLightIndex + 1;
        }
        else
        {
            currentCycleState = 0; 
        }
    }

    private string GetCycleStateDescription()
    {
        if (targetConeLights.Count == 0) return "No lights";

        if (currentCycleState == 0)
        {
            return targetConeLights.Count > 1 ? "All lights ON" : "Light OFF";
        }
        else if (currentCycleState <= targetConeLights.Count)
        {
            return $"Light {currentCycleState} ON only";
        }
        return "Unknown state";
    }


    private void PlayButtonSound()
    {
        if (audioSource != null && buttonPressSound != null)
        {
            audioSource.PlayOneShot(buttonPressSound);
        }
    }

    // Public method to add a light to the control list
    public void AddTargetLight(ConeLightReveal light)
    {
        if (light != null && !targetConeLights.Contains(light))
        {
            targetConeLights.Add(light);
           
        }
    }

    // Public method to remove a light from the control list
    public void RemoveTargetLight(ConeLightReveal light)
    {
        if (targetConeLights.Contains(light))
        {
            targetConeLights.Remove(light);
           
        }
    }

    // Public method to get the number of active lights
    public int GetActiveLightCount()
    {
        int count = 0;
        foreach (var light in targetConeLights)
        {
            if (light.isLightActive) count++;
        }
        return count;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw interaction range
        Gizmos.color = playerInRange ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Draw lines to all target cone lights
        for (int i = 0; i < targetConeLights.Count; i++)
        {
            if (targetConeLights[i] != null)
            {
                // Different colors for different lights
                Color lineColor = targetConeLights[i].isLightActive ? Color.cyan : Color.red;
                if (i == 1) lineColor = targetConeLights[i].isLightActive ? Color.blue : Color.magenta;

                Gizmos.color = lineColor;
                Gizmos.DrawLine(transform.position, targetConeLights[i].transform.position);

                // Draw a small sphere at the target to show which light is which
                Gizmos.DrawWireSphere(targetConeLights[i].transform.position, 0.5f);
            }
        }


    }
}