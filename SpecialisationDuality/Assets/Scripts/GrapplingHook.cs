using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public LineRenderer lineRenderer;
    public Transform playerTransform;

    [Header("Grapple Settings")]
    public float maxGrappleDistance = 50f;
    public float aimAssistRadius = 0.5f;
    public LayerMask grappleLayerMask;
    public KeyCode grappleKey = KeyCode.E;

    [Header("Swing Physics")]
    public float swingForce = 300f;
    public float ropeClimbSpeed = 8f;
    public float playerControlForce = 150f;
    public float momentumMultiplier = 0.6f;
    public float maxSwingSpeed = 15f;

    [Header("Air Control")]
    public float airControlForce = 200f;
    public float releaseBoostMultiplier = 1.1f;

    private Rigidbody rb;
    private Vector3 grapplePoint;
    private float ropeLength;
    private bool isGrappling = false;
    private PlayerMovement playerMovement;
    private DualityManager dualityManager;

    // Input actions
    private InputAction moveAction;
    private InputAction climbAction;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<PlayerMovement>();
        dualityManager = FindObjectOfType<DualityManager>();

        // Get input actions
        var playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        climbAction = playerInput.actions["Climb"]; 

        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }

    void Update()
    {
        HandleGrappleInput();
        UpdateLineRenderer();
        //HandleRopeClimbing();
    }

    void FixedUpdate()
    {
        if (isGrappling)
        {
            HandleSwingPhysics();
            HandlePlayerControl();
            LimitSwingSpeed();
        }
        else
        {
            HandleAirControl();
        }
    }

    void HandleGrappleInput()
    {
        if (Input.GetKey(grappleKey) && !isGrappling)
        {
            TryStartGrapple();
        }

        if (Input.GetKeyUp(grappleKey) && isGrappling)
        {
            StopGrapple();
        }
    }

    void TryStartGrapple()
    {
        if (dualityManager != null && !dualityManager.IsInShadowMode())
            return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.SphereCast(ray, aimAssistRadius, out RaycastHit hit, maxGrappleDistance, grappleLayerMask))
        {
            if (dualityManager != null && !hit.collider.CompareTag("ShadowOnly"))
                return;

            StartGrapple(hit.point);
        }
    }

    void StartGrapple(Vector3 point)
    {
        grapplePoint = point;
        ropeLength = Vector3.Distance(transform.position, grapplePoint);
        isGrappling = true;

        // Set player movement state
        if (playerMovement != null)
            playerMovement.isGrappling = true;

        // Setup line renderer
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, grapplePoint);
        }

        
    }

    void StopGrapple()
    {
        if (!isGrappling) return;

        isGrappling = false;

        if (playerMovement != null)
            playerMovement.isGrappling = false;

        if (lineRenderer != null)
            lineRenderer.positionCount = 0;

     
        Vector3 toGrapple = (grapplePoint - transform.position).normalized;
        Vector3 currentVelocity = rb.velocity;

       
        Vector3 tangentVelocity = currentVelocity - Vector3.Dot(currentVelocity, toGrapple) * toGrapple;

        // Apply that as your new velocity
        rb.velocity = tangentVelocity * releaseBoostMultiplier;
    }


    void HandleSwingPhysics()
    {
        Vector3 playerPos = transform.position;
        Vector3 ropeVector = grapplePoint - playerPos;
        float currentDistance = ropeVector.magnitude;

        // Only apply rope constraint if player is beyond rope length
        if (currentDistance > ropeLength)
        {
            Vector3 ropeDirection = ropeVector.normalized;
            Vector3 playerVelocity = rb.velocity;

            // Project velocity onto rope direction
            float velocityAlongRope = Vector3.Dot(playerVelocity, ropeDirection);

            // Only constrain if moving away from anchor point
            if (velocityAlongRope > 0)
            {
                // Remove velocity component along rope - pure constraint
                Vector3 constrainedVelocity = playerVelocity - (ropeDirection * velocityAlongRope);
                rb.velocity = constrainedVelocity;
            }


            if (currentDistance > ropeLength)
            {
                Vector3 tensionDirection = ropeVector.normalized;
                float overshoot = currentDistance - ropeLength;
                rb.AddForce(tensionDirection * overshoot * swingForce, ForceMode.Acceleration);
            }

        }


    }

    void HandlePlayerControl()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input.magnitude > 0.1f)
        {
            // Get movement direction relative to camera
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            // Remove Y component to keep movement horizontal
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (right * input.x + forward * input.y).normalized;

            // Apply control force
            rb.AddForce(moveDirection * playerControlForce, ForceMode.Acceleration);
        }
    }

    void HandleAirControl()
    {
        if (playerMovement != null && !playerMovement.IsGrounded())
        {
            Vector2 input = moveAction.ReadValue<Vector2>();

            if (input.magnitude > 0.1f)
            {
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;

                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();

                Vector3 moveDirection = (right * input.x + forward * input.y).normalized;
                rb.AddForce(moveDirection * airControlForce, ForceMode.Acceleration);
            }
        }
    }

    void LimitSwingSpeed()
    {
        if (rb.velocity.magnitude > maxSwingSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSwingSpeed;
        }
    }

    Vector3 GetSwingDirection()
    {
        Vector3 toAnchor = (grapplePoint - transform.position).normalized;
        Vector3 perpendicular = Vector3.Cross(Vector3.up, toAnchor);

        // Choose direction based on current velocity
        if (Vector3.Dot(rb.velocity, perpendicular) < 0)
            perpendicular = -perpendicular;

        return perpendicular;
    }

    void UpdateLineRenderer()
    {
        if (isGrappling && lineRenderer != null && lineRenderer.positionCount >= 2)
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, grapplePoint);
        }
    }

    public bool IsGrappling() => isGrappling;
    public Vector3 GetGrapplePoint() => grapplePoint;
    public float GetRopeLength() => ropeLength;
}

