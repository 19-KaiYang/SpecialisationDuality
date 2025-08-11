using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonToggle : MonoBehaviour
{
    [Header("Settings")]
    public float interactionRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    private DualityManager dualityManager;
    private Transform player;
    private bool playerInRange = false;

    private void Start()
    {
        dualityManager = FindObjectOfType<DualityManager>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

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
            if (dualityManager != null && !dualityManager.IsTransitioning())
            {
                dualityManager.TriggerDimensionSwitch();
               
             
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = playerInRange ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
