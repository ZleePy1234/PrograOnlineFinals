using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    private static InputManager _instance = null;
    public static InputManager Instance { get => _instance; private set => _instance = value; }

    private PlayerControls playerControls;

    private void Awake()
    {
        playerControls = new PlayerControls();

        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    private void OnEnable()
    {
        playerControls.Enable();
    }

    private void OnDisable()
    {
        if (playerControls != null)
        {
            playerControls.Disable();
        }
    }

    public Vector2 GetMoveInput()
    {
        return playerControls.Player.Move.ReadValue<Vector2>();
    }

    public bool IsMoveInputPressed()
    {
        return playerControls.Player.Move.IsPressed();
    }

    public bool WasRunInputPressed()
    {
        return playerControls.Player.Run.IsPressed();
    }

    public bool IsMovingBackwards()
    {
        return playerControls.Player.Move.ReadValue<Vector2>().y < 0;
    }

    public bool IsMovingOnXAxis()
    {
        return playerControls.Player.Move.ReadValue<Vector2>().x != 0;
    }

    public Vector2 GetMouseDelta()
    {
        return playerControls.Player.Look.ReadValue<Vector2>();
    }

    public bool IsFiring()
    {
        return playerControls.Player.Fire.IsPressed();
    }

    public bool ShootModeChange()
    {
        return playerControls.Player.ChangeShootMode.WasPressedThisFrame();
    }

    public bool IsCarryingBox()
    {
        return playerControls.Player.PickupBox.IsPressed();
    }
}