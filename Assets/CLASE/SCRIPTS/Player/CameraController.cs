using Fusion;
using UnityEngine;
using UnityEngine.Serialization;

public class CameraController : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform player;
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private float maxAngleY = 80f;
    [SerializeField] private float minAngleY = -80f;

    [Networked] private float pitchAngle { get; set; }
    [Networked] private float yawAngle { get; set; }

    public Vector3 AimDirection { get; private set; }
    public Vector3 CamPosition { get; private set; }

    [Header("Blob Movement")]
    [SerializeField] private float walkingSpeed = 1f;
    [SerializeField, Range(0, 0.1f)] private float walkingAmplitude = 0.015f;
    [SerializeField, Range(0, 0.1f)] private float runningAmplitude = 0.015f;
    [SerializeField, Range(0, 15)] private float walkingFrequency = 10.0f;
    [SerializeField, Range(10, 20)] private float runningFrequency = 18f;
    [SerializeField] private float resetPosSpeed = 3.0f;

    private Vector3 startPos;
    [SerializeField] private bool moveHead;
    private InputManager inputManager;
    private InputInfo input;

    public float YawAngle => yawAngle;

    /// <summary>
    /// Direccion de mira en simulacion (host/servidor). No depende de Render: necesario
    /// para armas y proyectiles cuando el StateAuthority no es el que tiene la camara.
    /// </summary>
    public Vector3 GetSimulationAimDirection()
    {
        Quaternion yaw = Quaternion.Euler(0f, yawAngle, 0f);
        Quaternion pitch = Quaternion.AngleAxis(-pitchAngle, Vector3.right);
        return (yaw * pitch) * Vector3.forward;
    }

    /// <summary>Posicion aproximada de la camara en simulacion (sin depender de Render).</summary>
    public Vector3 GetSimulationCameraWorldPosition()
    {
        if (player == null)
            return transform.position;
        return player.position + Quaternion.Euler(0f, yawAngle, 0f) * transform.localPosition;
    }

    private void Awake()
    {
        startPos = transform.localPosition;
        AimDirection = Vector3.forward;
    }

    public override void Spawned()
    {
        inputManager = InputManager.Instance;

        if (player == null)
        {
            var movement = GetComponentInParent<MovementController>();
            if (movement != null) player = movement.transform;
        }

        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (cam != null) cam.enabled = false;

            AudioListener listener = GetComponent<AudioListener>();
            if (listener == null) listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out input))
        {
            pitchAngle += input.lookDirection.y * mouseSensitivity;
            pitchAngle = Mathf.Clamp(pitchAngle, minAngleY, maxAngleY);
            yawAngle += input.lookDirection.x * mouseSensitivity;
        }

        if (!HasInputAuthority) return;
        if (inputManager == null) inputManager = InputManager.Instance;
        if (player == null) return;

        if (moveHead)
        {
            BlobMove();
            ResetPosition();
        }
    }

    public override void Render()
    {
        transform.localRotation = Quaternion.AngleAxis(-pitchAngle, Vector3.right);

        if (player != null)
            player.rotation = Quaternion.Euler(0f, yawAngle, 0f);

        AimDirection = transform.rotation * Vector3.forward;
        CamPosition = transform.position;
    }

    private void BlobMove()
    {
        if (!inputManager.IsMoveInputPressed()) return;

        bool isRunning = inputManager.WasRunInputPressed();
        bool isSpecialMove = inputManager.IsMovingBackwards() || inputManager.IsMovingOnXAxis();

        Vector3 motion = isSpecialMove ? FootStepMotion() : (isRunning ? RunningFootStepMotion() : FootStepMotion());
        transform.localPosition += motion;
    }

    private void ResetPosition()
    {
        if (transform.localPosition == startPos) return;
        transform.localPosition = Vector3.Lerp(transform.localPosition, startPos, resetPosSpeed * Runner.DeltaTime);
    }

    private Vector3 FootStepMotion()
    {
        Vector3 pos = Vector3.zero;
        float t = (float)Runner.SimulationTime;
        pos.y = Mathf.Sin(t * walkingFrequency) * walkingAmplitude * walkingSpeed;
        pos.x = Mathf.Cos(t * walkingFrequency / 2f) * walkingAmplitude * 2f * walkingSpeed;
        return pos;
    }

    private Vector3 RunningFootStepMotion()
    {
        Vector3 pos = Vector3.zero;
        float t = (float)Runner.SimulationTime;
        pos.y = Mathf.Sin(t * runningFrequency) * runningAmplitude * walkingSpeed;
        pos.x = Mathf.Cos(t * runningFrequency / 2f) * runningAmplitude * 2f * walkingSpeed;
        return pos;
    }
}