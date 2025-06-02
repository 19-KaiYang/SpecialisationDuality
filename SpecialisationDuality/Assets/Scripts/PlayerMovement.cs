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

    [Header("Camera")]
    public Transform cameraTransform;

    [HideInInspector] public bool isGrappling = false;

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
        HandleCrouch();

        if (!isGrappling)
        {
            HandleJump();
        }
    }

    void FixedUpdate()
    {
        if (!isGrappling)
        {
            Move();
        }
    }

    void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * (isCrouching ? crouchSpeed : walkSpeed);

        Vector3 velocity = move;
        velocity.y = rb.velocity.y; 
        rb.velocity = velocity;
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

    public bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, (capsule.height / 2f) + 0.1f);
    }
}