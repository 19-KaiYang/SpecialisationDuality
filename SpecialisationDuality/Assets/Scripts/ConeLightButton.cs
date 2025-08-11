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

    public bool useCycleMode = false;

 

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip buttonPressSound;

    private Transform player;
    private bool playerInRange = false;
    private Renderer buttonRenderer;
    private int currentCycleState = 0; 

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        buttonRenderer = GetComponent<Renderer>();

        if (player == null)

        // Remove any null references
        targetConeLights.RemoveAll(light => light == null);


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
            AudioManager.Instance.PlaySFX("Button");
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

        currentCycleState = (currentCycleState + 1) % (targetConeLights.Count + 1);

        // Turn off all lights 
        foreach (var light in targetConeLights)
            light.SetLightActive(false);

        if (currentCycleState > 0 && currentCycleState <= targetConeLights.Count)
            targetConeLights[currentCycleState - 1].SetLightActive(true);



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


 
    public void AddTargetLight(ConeLightReveal light)
    {
        if (light != null && !targetConeLights.Contains(light))
        {
            targetConeLights.Add(light);
           
        }
    }


    public void RemoveTargetLight(ConeLightReveal light)
    {
        if (targetConeLights.Contains(light))
        {
            targetConeLights.Remove(light);
           
        }
    }

 
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
    
        Gizmos.color = playerInRange ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

     
        for (int i = 0; i < targetConeLights.Count; i++)
        {
            if (targetConeLights[i] != null)
            {
               
                Color lineColor = targetConeLights[i].isLightActive ? Color.cyan : Color.red;
                if (i == 1) lineColor = targetConeLights[i].isLightActive ? Color.blue : Color.magenta;

                Gizmos.color = lineColor;
                Gizmos.DrawLine(transform.position, targetConeLights[i].transform.position);

              
                Gizmos.DrawWireSphere(targetConeLights[i].transform.position, 0.5f);
            }
        }


    }
}