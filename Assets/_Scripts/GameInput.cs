using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    public static GameInput Instance { get; private set; }

    public event EventHandler OnJumpAction;
    public event EventHandler OnInteractAction;
    public event EventHandler OnRunAction;
    public event EventHandler OnThrowStart;   // when button pressed
    public event EventHandler OnThrowRelease; // when released

    [SerializeField] private PlayerInput playerInput;
    public bool IsGamepadActive => playerInput != null && playerInput.currentControlScheme == "Gamepad";
    public bool IsKeyboardMouseActive => playerInput != null && playerInput.currentControlScheme == "KeyboardMouse";

    private enum ControlType { KeyboardMouse, Gamepad }
    private ControlType lastUsedControlType = ControlType.KeyboardMouse;

    public event Action<string> OnControlSchemeChanged;

    private PlayerInputActions playerInputActions;

    void Awake()
    {
        Instance = this;

        playerInput = GetComponent<PlayerInput>();

        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component not found!");
            return;
        }

        Debug.Log("Initial scheme: " + playerInput.currentControlScheme);

        playerInput.onControlsChanged += OnControlsChanged;

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        playerInputActions.Player.Interact.performed += Interact_performed;
        playerInputActions.Player.Run.performed += Run_performed;
        playerInputActions.Player.Jump.performed += Jump_performed;

        // Debug for DropThrow
        playerInputActions.Player.DropThrow.started += ctx =>
        {
            Debug.Log("DropThrow button pressed (started)");
            OnThrowStart?.Invoke(this, EventArgs.Empty);
        };

        playerInputActions.Player.DropThrow.canceled += ctx =>
        {
            Debug.Log(" DropThrow button released (canceled)");
            OnThrowRelease?.Invoke(this, EventArgs.Empty);
        };
    }

    private void OnDestroy()
    {
        playerInputActions.Player.Interact.performed -= Interact_performed;
        playerInputActions.Player.Run.performed -= Run_performed;

        playerInputActions.Dispose();
    }

    private void Interact_performed(InputAction.CallbackContext obj)
    {
        OnInteractAction?.Invoke(this, EventArgs.Empty);
    }

    private void Run_performed(InputAction.CallbackContext obj)
    {
        OnRunAction?.Invoke(this, EventArgs.Empty);
    }

    private void Jump_performed(InputAction.CallbackContext obj)
    {
        OnJumpAction?.Invoke(this, EventArgs.Empty);
    }

    public Vector2 GetMovementVectorNormalized()
    {
        Vector2 input = playerInputActions.Player.Move.ReadValue<Vector2>();

        if (Gamepad.current != null && input.sqrMagnitude > 0.01f)
            lastUsedControlType = ControlType.Gamepad;

        return input.normalized;
    }

    public Vector2 GetMouseDelta()
    {
        Vector2 input = playerInputActions.Player.Look.ReadValue<Vector2>();

        if (Mouse.current != null && input.sqrMagnitude > 0.01f)
            lastUsedControlType = ControlType.KeyboardMouse;

        return input;
    }

    public bool IsGamepad()
    {
        return Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
    }

    public bool IsRunHeld()
    {
        return playerInputActions.Player.Run.IsPressed();
    }

    private void OnControlsChanged(PlayerInput input)
    {
        Debug.Log("Control Scheme Changed To: " + input.currentControlScheme);
        OnControlSchemeChanged?.Invoke(input.currentControlScheme);
    }

    public bool IsUsingGamepad() => IsGamepadActive;
}
