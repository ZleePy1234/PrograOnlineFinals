using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponHandler : MonoBehaviour
{
    public Weapon equipedWeapon;

    private NetworkObject _networkObject;

    private void Awake()
    {
        _networkObject = GetComponentInParent<NetworkObject>();
    }

    private void Update()
    {
        // Solo procesar input del jugador que controlamos (evita que el host dispare todas las armas)
        if (_networkObject != null && !_networkObject.HasInputAuthority)
            return;

        if (equipedWeapon == null) return;
        if (InputManager.Instance == null) return;

        // El disparo se hace desde Weapon.FixedUpdateNetwork con input de red (para que el host ejecute Runner.Spawn del cliente)
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            equipedWeapon.Reload();
        }

        if (InputManager.Instance.ShootModeChange())
        {
            equipedWeapon.ChangeShootMode();
        }
    }
}