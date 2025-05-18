using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float crouchSpeed = 2.5f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public float lookSensitivity = 0.2f;

    [Header("Crouch Settings")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 8f;
    private float currentHeight;
    private float currentCameraY;


    [Header("Camera")]
    public Transform cameraTransform;

    private CharacterController controller;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching;
    private float xRotation;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        jumpAction = playerInput.actions["Jump"];
        crouchAction = playerInput.actions["Crouch"];
    }

    void Start()
    {
        currentHeight = standHeight;
        currentCameraY = cameraTransform.localPosition.y;
    }


    void Update()
    {
        Look();
        Move();
        HandleJump();
        HandleCrouch();
        ApplyGravity();
    }

    void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        float speed = isCrouching ? crouchSpeed : walkSpeed;
        controller.Move(move * speed * Time.deltaTime);
    }

    void Look()
    {
        Vector2 mouse = lookAction.ReadValue<Vector2>() * lookSensitivity;
        xRotation -= mouse.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouse.x);
    }

    void HandleJump()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (jumpAction.WasPressedThisFrame() && isGrounded && !isCrouching)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    void HandleCrouch()
    {
        bool isCrouchHeld = crouchAction.IsPressed();
        isCrouching = isCrouchHeld;

        float targetHeight = isCrouching ? crouchHeight : standHeight;
        float targetCameraY = isCrouching ? crouchHeight / 2 : standHeight / 2;

        // Smoothly change the character height
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.height = currentHeight;

        // Smoothly adjust camera Y position
        Vector3 camPos = cameraTransform.localPosition;
        currentCameraY = Mathf.Lerp(currentCameraY, targetCameraY, Time.deltaTime * crouchTransitionSpeed);
        cameraTransform.localPosition = new Vector3(camPos.x, currentCameraY, camPos.z);
    }



    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
