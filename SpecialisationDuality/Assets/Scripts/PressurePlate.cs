using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    public PressurePlateGroup plateGroup;
    private bool isPressed = false;

    private void OnTriggerEnter(Collider other)
    {
        if ((other.CompareTag("Player") || other.CompareTag("Interactable")) && !isPressed)
        {
            isPressed = true;
            plateGroup.PlatePressed();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if ((other.CompareTag("Player") || other.CompareTag("Interactable")) && isPressed)
        {
            isPressed = false;
            plateGroup.PlateReleased();
        }
    }
}