using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UiModeControls : MonoBehaviour
{
    [Header("References")]
    public DualityManager dualityManager;
    public Image indicatorImage;

    private Animator animator;
    private bool lastKnownMode;

    void Start()
    {
        // Get references if not assigned
        if (dualityManager == null)
            dualityManager = FindObjectOfType<DualityManager>();

        if (indicatorImage == null)
            indicatorImage = GetComponent<Image>();

        // Get animator component
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            enabled = false;
            return;
        }

        // Initialize based on current mode
        if (dualityManager != null)
        {
            lastKnownMode = dualityManager.IsInShadowMode();
            animator.SetBool("IsShadowMode", lastKnownMode);
        }
    }

    void Update()
    {
        if (dualityManager == null) return;

        bool currentMode = dualityManager.IsInShadowMode();

        // Check if mode changed
        if (currentMode != lastKnownMode)
        {
            lastKnownMode = currentMode;
            animator.SetBool("IsShadowMode", currentMode);
        }
    }
}
