using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Launcher : MonoBehaviour
{
    public float launchForce = 20f;
    public float launchAngle = 45f; 

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.GetComponent<Rigidbody>();

        
        if (other.gameObject.tag == "Player")
        {
            LaunchObject(rb);
        }
    }

    private void LaunchObject(Rigidbody rb)
    {
        // Calculate launch direction based on launcher's forward direction and angle
        Vector3 launchDirection = CalculateLaunchDirection();

        // FIXED: Reset velocity completely, then set new velocity
        rb.velocity = launchDirection * launchForce;

        AudioManager.Instance.PlaySFX("JumpPad");

        PlayerMovement playerMovement = rb.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.OnLaunched();
        }
    }

    private Vector3 CalculateLaunchDirection()
    {
        // Convert angle to radians
        float angleRad = launchAngle * Mathf.Deg2Rad;

        // Calculate launch direction based on forward direction and angle
        Vector3 forward = transform.forward;
        Vector3 up = Vector3.up;

        // Create launch vector with the specified angle
        Vector3 launchDirection = (forward * Mathf.Cos(angleRad) + up * Mathf.Sin(angleRad)).normalized;

        return launchDirection;
    }
}