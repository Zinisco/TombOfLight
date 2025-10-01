using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runMultiplier = 1.5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight = 1.5f;
    private float orbSlowFactor = 0f;
    private bool canRun = true;
    private bool canJump = true;

    [Header("Look Settings")]
    [SerializeField] private float lookSensitivity = 0.2f;
    [SerializeField] private float controllerLookSensitivity = 0.7f;
    [SerializeField] private Transform cam;

    public float MouseSensitivity { get => lookSensitivity; set => lookSensitivity = value; }
    public float ControllerSensitivity { get => controllerLookSensitivity; set => controllerLookSensitivity = value; }
    public bool InvertX { get; set; } = false;
    public bool InvertY { get; set; } = false;

    private CharacterController controller;
    private float verticalVelocity;
    private float xRotation = 0f;

    private bool isGrounded;

    public bool IsLocked { get; set; } = false;

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        gameInput.OnJumpAction += GameInput_OnJumpAction;
        GameInput.Instance.OnControlSchemeChanged += OnControlSchemeChanged;
    }

    private void Update()
    {
        if (IsLocked) return;

        HandleLook();
        HandleMovement();
    }

    private void HandleMovement()
    {
        // Run the move first
        Vector2 inputVector = gameInput.GetMovementVectorNormalized();
        Vector3 move = transform.right * inputVector.x + transform.forward * inputVector.y;

        float currentSpeed = moveSpeed * (1f - orbSlowFactor);

        if (gameInput.IsRunHeld() && canRun)
            currentSpeed *= runMultiplier;

        // Apply gravity
        verticalVelocity += gravity * Time.deltaTime;

        // Move player
        Vector3 motion = (move * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime;
        CollisionFlags flags = controller.Move(motion);

        // Update grounded state AFTER moving
        isGrounded = (flags & CollisionFlags.Below) != 0;

        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f; // stick to ground
    }


    private void HandleLook()
    {
        Vector2 mouseDelta = gameInput.GetMouseDelta();
        mouseDelta = Vector2.ClampMagnitude(mouseDelta, 10f);

        float sensitivity = GameInput.Instance.IsGamepadActive ? controllerLookSensitivity : lookSensitivity;
        mouseDelta *= sensitivity;

        float sx = InvertX ? -1f : 1f;
        transform.Rotate(Vector3.up * (sx * mouseDelta.x));

        float sy = InvertY ? 1f : -1f;
        xRotation += sy * mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    private void GameInput_OnJumpAction(object sender, EventArgs e)
    {
        Debug.Log($"Jump event fired. isGrounded = {isGrounded}, canJump = {canJump}");

        if (isGrounded && canJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log("Applied vertical velocity: " + verticalVelocity);
        }
    }

    public void ExitUI()
    {
        IsLocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        if (GameInput.Instance != null)
        {
            GameInput.Instance.OnControlSchemeChanged -= OnControlSchemeChanged;
            gameInput.OnJumpAction -= GameInput_OnJumpAction;
        }
    }

    private void OnControlSchemeChanged(string scheme)
    {
        Debug.Log($"Control scheme changed: {scheme}");
    }

    public void SetOrbSlow(float percent) => orbSlowFactor = Mathf.Clamp01(percent);
    public void SetCanRun(bool value) => canRun = value;
    public void SetCanJump(bool value)
    {
        canJump = value;
    }
    public float GetCameraPitch() => xRotation;

    public void Teleport(Vector3 pos, float yawDegrees, float pitchDegrees)
    {
        if (!controller) controller = GetComponent<CharacterController>();

        bool wasEnabled = controller.enabled;
        controller.enabled = false;

        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        xRotation = pitchDegrees;
        if (cam) cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        verticalVelocity = 0f;
        Physics.SyncTransforms();

        controller.enabled = wasEnabled;
    }

    private void OnDrawGizmosSelected()
    {
        if (controller != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 bottom = controller.bounds.center - new Vector3(0, controller.height / 2f, 0);
            Gizmos.DrawWireSphere(bottom + Vector3.up * 0.1f, controller.radius * 0.95f);
        }
    }

}


