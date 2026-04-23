using Fusion;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Reflection;
using System;

public class Health : NetworkBehaviour, IDamagable
{
    [Networked] public byte CurrentHealth { get; set; }
    [SerializeField] private byte maxHealth;
    [SerializeField] private Image healthBar;
    public UnityEvent onDeath;
    [SerializeField] private float respawnDelay = 3f;

    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private NetworkBool PendingRespawn { get; set; }

    [Networked] private Vector3 RespawnPos { get; set; }
    [Networked] private Quaternion RespawnRot { get; set; }
    [Networked] private NetworkBool HasRespawnTarget { get; set; }

    private MovementController player;
    private NetworkCharacterController fusionController;
    private Component simpleKccComponent;
    private Gravity gravity;

    private void Start()
    {
        player = GetComponent<MovementController>();
        fusionController = GetComponent<NetworkCharacterController>();
        gravity = GetComponent<Gravity>();
        CacheSimpleKcc();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            CurrentHealth = maxHealth;

        UpdateHealthUI();
    }

    public void TakeDamage(byte damage)
    {
        RPC_TakeDamage(damage);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(byte damage)
    {
        Debug.Log($"[RESPAWN] RPC_TakeDamage | target={gameObject.name} auth={Object.InputAuthority.PlayerId} dmg={damage} hp={CurrentHealth}->{Mathf.Max(0, CurrentHealth - damage)}");
        int newHealth = Mathf.Max(0, CurrentHealth - damage);
        CurrentHealth = (byte)newHealth;

        if (CurrentHealth <= 0)
        {
            // Respawn instantáneo: se spawnea en el spawn del equipo y queda congelado hasta que termine el timer.
            if (PhotonManager.Instance != null)
            {
                Debug.Log($"[RESPAWN] Death -> Respawn instant | auth={Object.InputAuthority.PlayerId} freeze={respawnDelay}s");
                PhotonManager.Instance.RespawnPlayer(Object.InputAuthority, Object, respawnDelay);
                return;
            }

            // Fallback sin PhotonManager (se queda en el mismo objeto)
            CurrentHealth = 0;
            PendingRespawn = true;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
            Debug.Log($"[RESPAWN] Death triggered (fallback) | auth={Object.InputAuthority.PlayerId} pending={PendingRespawn} delay={respawnDelay}s");
            RPC_OnDeath();
        }
    }

    // Se llama desde servidor/state authority luego del spawn del nuevo player.
    public void Server_BeginRespawnFreeze(float freezeSeconds)
    {
        if (!Object.HasStateAuthority)
            return;

        CurrentHealth = 0;
        PendingRespawn = true;
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, freezeSeconds);
        Debug.Log($"[RESPAWN] BeginRespawnFreeze | auth={Object.InputAuthority.PlayerId} freeze={freezeSeconds}s");
        RPC_OnDeath();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnDeath()
    {
        Debug.Log($"[RESPAWN] RPC_OnDeath | {gameObject.name} auth={Object.InputAuthority.PlayerId}");
        SetLocomotionEnabled(false);
        if (player != null) player.OnDeath();
        onDeath.Invoke();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (PendingRespawn && RespawnTimer.Expired(Runner))
        {
            Debug.Log($"[RESPAWN] Freeze timer expired | auth={Object.InputAuthority.PlayerId} enabling movement");
            PendingRespawn = false;
            CurrentHealth = maxHealth;
            RPC_OnRespawn();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnRespawn()
    {
        SetLocomotionEnabled(true);
        if (player != null) player.OnRespawn();
    }

    public override void Render()
    {
        UpdateHealthUI();
        UpdateRespawnUI();
    }

    private void UpdateHealthUI()
    {
        if (healthBar != null)
            healthBar.fillAmount = maxHealth <= 0 ? 0f : (float)CurrentHealth / maxHealth;
    }

    private void RespawnPlayer()
    {
        Vector3 pos = RespawnPos;
        Quaternion rot = RespawnRot;
        Debug.Log($"[RESPAWN] RespawnPlayer | auth={Object.InputAuthority.PlayerId} hasTarget={HasRespawnTarget} pos={pos}");

        SetLocomotionEnabled(true);
        if (HasRespawnTarget)
            SetPlayerPosition(pos, rot);

        CurrentHealth = maxHealth;

        if (player != null) player.OnRespawn();
        Debug.Log($"[RESPAWN] RespawnPlayer done | auth={Object.InputAuthority.PlayerId} finalPos={transform.position}");

        // Importante para KCCs con predicción: forzamos snap local en todos (especialmente InputAuthority)
        if (HasRespawnTarget)
            RPC_ForceTeleport(pos, rot);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ForceTeleport(Vector3 position, Quaternion rotation)
    {
        // Evitar repetir en servidor si ya se aplicó, pero no es grave si se repite.
        Debug.Log($"[RESPAWN] RPC_ForceTeleport | auth={Object.InputAuthority.PlayerId} localHasInput={HasInputAuthority} applying pos={position} before={transform.position}");
        // No apagamos el KCC antes del teleport: algunos KCC ignoran/rompen su estado si están disabled.
        SetLocomotionEnabled(false);
        ApplyTeleportLocally(position, rotation);
        SetLocomotionEnabled(true);
        Debug.Log($"[RESPAWN] RPC_ForceTeleport done | auth={Object.InputAuthority.PlayerId} after={transform.position}");
    }

    private void SetPlayerPosition(Vector3 position, Quaternion rotation)
    {
        Vector3 before = transform.position;
        if (fusionController != null)
        {
            fusionController.Teleport(position, rotation);
            ResetRigidbodyMotionIfAny();
            transform.SetPositionAndRotation(position, rotation);
            Debug.Log($"[RESPAWN] Teleport via NetworkCharacterController | {before} -> {transform.position}");
            return;
        }

        Component[] components = GetComponents<Component>();

        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null || c.GetType().Name != "SimpleKCC")
                continue;

            Type t = c.GetType();

            // Intentamos APIs comunes de KCC por reflection (según versión)
            MethodInfo teleport = t.GetMethod("Teleport", new[] { typeof(Vector3), typeof(Quaternion) });
            if (teleport != null)
            {
                teleport.Invoke(c, new object[] { position, rotation });
                ResetRigidbodyMotionIfAny();
                transform.SetPositionAndRotation(position, rotation);
                Debug.Log($"[RESPAWN] Teleport via SimpleKCC.Teleport(pos,rot) | {before} -> {transform.position}");
                return;
            }

            MethodInfo setPosRot = t.GetMethod("SetPositionAndRotation", new[] { typeof(Vector3), typeof(Quaternion) });
            if (setPosRot != null)
            {
                setPosRot.Invoke(c, new object[] { position, rotation });
                ResetRigidbodyMotionIfAny();
                transform.SetPositionAndRotation(position, rotation);
                Debug.Log($"[RESPAWN] Teleport via SimpleKCC.SetPositionAndRotation | {before} -> {transform.position}");
                return;
            }

            MethodInfo setPos = t.GetMethod("SetPosition", new[] { typeof(Vector3) });
            if (setPos != null)
            {
                setPos.Invoke(c, new object[] { position });
                transform.rotation = rotation;
                ResetRigidbodyMotionIfAny();
                transform.SetPositionAndRotation(position, rotation);
                Debug.Log($"[RESPAWN] Teleport via SimpleKCC.SetPosition(pos) | {before} -> {transform.position}");
                return;
            }

            Debug.LogWarning($"[RESPAWN] Found SimpleKCC but no known teleport method. Type={t.FullName}");
        }

        transform.SetPositionAndRotation(position, rotation);
        ResetRigidbodyMotionIfAny();
        Debug.Log($"[RESPAWN] Teleport via transform.SetPositionAndRotation | {before} -> {transform.position}");
    }

    private void ApplyTeleportLocally(Vector3 position, Quaternion rotation)
    {
        Vector3 before = transform.position;
        // Método “best effort” para que el cliente con InputAuthority no se quede prediciendo la posición vieja.
        if (fusionController != null)
        {
            fusionController.Teleport(position, rotation);
            ResetRigidbodyMotionIfAny();
            transform.SetPositionAndRotation(position, rotation);
            Debug.Log($"[RESPAWN] ApplyTeleportLocally NCC | {before} -> {transform.position}");
            return;
        }

        // Si hay SimpleKCC, intenta sus APIs; si no, transform directo.
        Component[] components = GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null || c.GetType().Name != "SimpleKCC")
                continue;

            Type t = c.GetType();
            MethodInfo teleport = t.GetMethod("Teleport", new[] { typeof(Vector3), typeof(Quaternion) });
            if (teleport != null)
            {
                teleport.Invoke(c, new object[] { position, rotation });
                ResetRigidbodyMotionIfAny();
                transform.SetPositionAndRotation(position, rotation);
                Debug.Log($"[RESPAWN] ApplyTeleportLocally SimpleKCC.Teleport(pos,rot) | {before} -> {transform.position}");
                return;
            }

            MethodInfo setPosRot = t.GetMethod("SetPositionAndRotation", new[] { typeof(Vector3), typeof(Quaternion) });
            if (setPosRot != null)
            {
                setPosRot.Invoke(c, new object[] { position, rotation });
                ResetRigidbodyMotionIfAny();
                transform.SetPositionAndRotation(position, rotation);
                Debug.Log($"[RESPAWN] ApplyTeleportLocally SimpleKCC.SetPositionAndRotation | {before} -> {transform.position}");
                return;
            }

            MethodInfo setPos = t.GetMethod("SetPosition", new[] { typeof(Vector3) });
            if (setPos != null)
            {
                setPos.Invoke(c, new object[] { position });
                transform.rotation = rotation;
                ResetRigidbodyMotionIfAny();
                transform.SetPositionAndRotation(position, rotation);
                Debug.Log($"[RESPAWN] ApplyTeleportLocally SimpleKCC.SetPosition(pos) | {before} -> {transform.position}");
                return;
            }
        }

        transform.SetPositionAndRotation(position, rotation);
        ResetRigidbodyMotionIfAny();
        Debug.Log($"[RESPAWN] ApplyTeleportLocally transform | {before} -> {transform.position}");
    }

    private void UpdateRespawnUI()
    {
        if (!HasInputAuthority)
            return;

        bool show = PendingRespawn;
        if (PhotonManager.Instance == null)
            return;

        float seconds = respawnDelay;
        try
        {
            var remaining = RespawnTimer.RemainingTime(Runner);
            if (remaining.HasValue)
                seconds = remaining.Value;
        }
        catch
        {
            // Si la versión de Fusion no expone RemainingTime, al menos mostramos delay fijo.
        }

        int display = Mathf.Max(0, Mathf.CeilToInt(seconds));
        PhotonManager.Instance.SetRespawnUI(show, display);
    }

    private void CacheSimpleKcc()
    {
        simpleKccComponent = null;
        Component[] components = GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null) continue;
            if (c.GetType().Name == "SimpleKCC")
            {
                simpleKccComponent = c;
                break;
            }
        }
    }

    private void SetLocomotionEnabled(bool enabled)
    {
        if (player != null && player.enabled != enabled)
            player.enabled = enabled;

        if (gravity != null && gravity.enabled != enabled)
            gravity.enabled = enabled;
    }

    private void ResetRigidbodyMotionIfAny()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        if (!rb.isKinematic)
        {
            // Unity 6 usa linearVelocity; dejamos fallback a velocity por compatibilidad via reflection
            try
            {
                rb.linearVelocity = Vector3.zero;
            }
            catch
            {
                // older Unity fallback
                rb.linearVelocity = Vector3.zero;
            }
            rb.angularVelocity = Vector3.zero;
        }
        rb.Sleep();
    }
}