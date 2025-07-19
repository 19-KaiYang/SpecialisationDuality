using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float crouchSpeed = 2.5f;
    public float jumpForce = 5f;
    public float lookSensitivity = 2f;
    public float cameraSmoothing = 10f;

    [Header("Crouch Settings")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 8f;

    [Header("Launch Settings")]
    public float launchCooldown = 0.5f;
    public float groundFriction = 0.9f;

    [Header("Camera")]
    public Transform cameraTransform;

    [HideInInspector] public bool isGrappling = false;
    [HideInInspector] public bool isLaunched = false;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private bool isCrouching;
    private float currentCameraY;
    private float xRotation;
    private float targetXRotation;
    private float launchTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        jumpAction = playerInput.actions["Jump"];
        crouchAction = playerInput.actions["Crouch"];
    }

    void Start()
    {
        rb.freezeRotation = true;
        currentCameraY = cameraTransform.localPosition.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Look();

        // Only handle crouch and jump when not launched
        if (!isLaunched)
        {
            HandleCrouch();
            if (!isGrappling)
            {
                HandleJump();
            }
        }

        if (isLaunched)
        {
            launchTimer -= Time.deltaTime;
            if (launchTimer <= 0f && IsActuallyGrounded())
            {
                OnLanded();
            }
        }
    }

    void FixedUpdate()
    {
        if (isGrappling)
            return;

        if (isLaunched)
        {
            // Apply ground friction when actually grounded during launch
            if (IsActuallyGrounded())
            {
                Vector3 velocity = rb.velocity;
                velocity.x *= groundFriction;
                velocity.z *= groundFriction;
                rb.velocity = velocity;
            }
            // No movement input processing during launch
            return;
        }

        // Normal movement when not launched
        Vector2 input = moveAction.ReadValue<Vector2>();
        bool hasInput = input.magnitude > 0.1f;

        if (IsGrounded() || hasInput)
        {
            Move();
        }
    }

    void Move()
    {
        if (isLaunched)
            return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 inputDirection = (transform.right * input.x + transform.forward * input.y).normalized;
        float speed = isCrouching ? crouchSpeed : walkSpeed;

        if (IsGrounded())
        {
            Vector3 move = inputDirection * speed;
            move.y = rb.velocity.y;
            rb.velocity = move;
        }
        else
        {
            Vector3 airControl = inputDirection * speed * 0.05f;
            Vector3 newVelocity = rb.velocity + new Vector3(airControl.x, 0f, airControl.z);

            Vector3 horizontal = new Vector3(newVelocity.x, 0f, newVelocity.z);
            if (horizontal.magnitude > speed)
            {
                horizontal = horizontal.normalized * speed;
                newVelocity.x = horizontal.x;
                newVelocity.z = horizontal.z;
            }

            rb.velocity = newVelocity;
        }
    }

    void Look()
    {
        Vector2 mouse = lookAction.ReadValue<Vector2>() * lookSensitivity;

        targetXRotation -= mouse.y;
        targetXRotation = Mathf.Clamp(targetXRotation, -90f, 90f);

        float smoothing = isGrappling ? cameraSmoothing * 2f : cameraSmoothing;
        xRotation = Mathf.Lerp(xRotation, targetXRotation, Time.deltaTime * smoothing);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouse.x);
    }

    void HandleJump()
    {
        if (jumpAction.WasPressedThisFrame() && IsGrounded())
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void HandleCrouch()
    {
        isCrouching = crouchAction.IsPressed();
        float targetHeight = isCrouching ? crouchHeight : standHeight;
        float targetCameraY = isCrouching ? crouchHeight / 2f : standHeight / 2f;

        capsule.height = Mathf.Lerp(capsule.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        Vector3 camPos = cameraTransform.localPosition;
        currentCameraY = Mathf.Lerp(currentCameraY, targetCameraY, Time.deltaTime * crouchTransitionSpeed);
        cameraTransform.localPosition = new Vector3(camPos.x, currentCameraY, camPos.z);
    }

    public void OnLaunched()
    {
        isLaunched = true;
        launchTimer = launchCooldown;

        // Disable movement input actions
        moveAction.Disable();
        jumpAction.Disable();
        crouchAction.Disable();
    }

    private void OnLanded()
    {
        isLaunched = false;

        // Re-enable movement input actions
        moveAction.Enable();
        jumpAction.Enable();
        crouchAction.Enable();
    }

    public bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, (capsule.height / 2f) + 0.1f);
    }

    public bool IsActuallyGrounded()
    {
        float rayDistance = (capsule.height / 2f) + 0.05f;
        return Physics.Raycast(transform.position, Vector3.down, rayDistance) &&
               Mathf.Abs(rb.velocity.y) < 0.5f;
    }
}