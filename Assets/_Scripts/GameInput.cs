using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    public static GameInput Instance { get; private set; }

    public event EventHandler OnJumpAction;
    public event EventHandler OnInteractAction;
    public event EventHandler OnRunAction;

    [SerializeField] private PlayerInput playerInput;
    public bool IsGamepadActive => playerInput != null && playerInput.currentControlScheme == "Gamepad";
    public bool IsKeyboardMouseActive => playerInput != null && playerInput.currentControlScheme == "KeyboardMouse";


    private enum ControlType { KeyboardMouse, Gamepad }
    private ControlType lastUsedControlType = ControlType.KeyboardMouse;

    public event Action<string> OnControlSchemeChanged;


    private PlayerInputActions playerInputActions;

    // Start is called before the first frame update
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

    }

    private void OnDestroy()
    {
        playerInputActions.Player.Interact.performed -= Interact_performed;
        playerInputActions.Player.Run.performed -= Run_performed;

        playerInputActions.Dispose();
    }

    private void Interact_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnInteractAction?.Invoke(this, EventArgs.Empty);
    }

    private void Run_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnRunAction?.Invoke(this, EventArgs.Empty);
    }

    private void Jump_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnJumpAction?.Invoke(this, EventArgs.Empty);
    }

    public Vector2 GetMovementVectorNormalized()
    {
        Vector2 input = playerInputActions.Player.Move.ReadValue<Vector2>();

        if (Gamepad.current != null && input.sqrMagnitude > 0.01f)
        {
            lastUsedControlType = ControlType.Gamepad;
        }

        return input.normalized;
    }


    public Vector2 GetMouseDelta()
    {
        Vector2 input = playerInputActions.Player.Look.ReadValue<Vector2>();

        if (Mouse.current != null && input.sqrMagnitude > 0.01f)
        {
            lastUsedControlType = ControlType.KeyboardMouse;
        }

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
        //Debug.Log("Control Scheme Changed To: " + input.currentControlScheme);
        OnControlSchemeChanged?.Invoke(input.currentControlScheme);
    }

    public bool IsUsingGamepad() => IsGamepadActive;


}
