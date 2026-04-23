using Fusion;
using System.Collections;
using UnityEngine;

public abstract class Weapon : NetworkBehaviour
{
    public ShootMode shootMode = ShootMode.RigidBody;

    [Header("References")]
    [SerializeField] protected LayerMask Layers;
    [SerializeField] protected Transform shootPoint;
    [SerializeField] protected NetworkPrefabRef proyectil;

    [Header("RayCast")]
    [SerializeField] protected float raycastRange = 100f;

    [Header("Stats")]
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected float fireRate = 0.2f;
    [SerializeField] protected float bulletForce = 100f;

    [Header("Ammo")]
    [SerializeField] protected int actualAmmo;
    [SerializeField] protected int maxAmmoCapacity = 30;
    [SerializeField] protected int ammoInStock = 90;

    [Header("Reload")]
    [SerializeField] protected float reloadTime = 2f;

    protected bool onShootCooldown;
    protected bool isReloading;
    private CameraController cameraController;

    public override void Spawned()
    {
        if (HasInputAuthority)
            cameraController = FindObjectOfType<CameraController>();
        if (cameraController == null && Object != null)
            cameraController = Object.transform.GetComponentInChildren<CameraController>(true);
    }

    public override void FixedUpdateNetwork()
    {
        if (cameraController == null && HasInputAuthority)
            cameraController = FindObjectOfType<CameraController>();
        if (cameraController == null && Object != null)
            cameraController = Object.transform.GetComponentInChildren<CameraController>(true);

        // Solo el State Authority (suelo ser el host) simula el disparo, spawn de bala y dano.
        if (!Object.HasStateAuthority)
            return;

        if (GetInput(out InputInfo input) && input.fire)
            HandleShoot();
    }

    public virtual void HandleShoot()
    {
        if (onShootCooldown || isReloading)
        {
            if (HasInputAuthority)
                Debug.Log($"[COMBAT] HandleShoot bloqueado: cooldown={onShootCooldown} reloading={isReloading}");
            return;
        }
        if (actualAmmo <= 0)
        {
            if (HasInputAuthority)
                Debug.Log("[COMBAT] HandleShoot: sin municion, intentando recargar");
            Reload();
            return;
        }
        actualAmmo--;
        if (HasInputAuthority)
            Debug.Log($"[COMBAT] HandleShoot: disparo ejecutado | modo={shootMode} | ammo restante={actualAmmo}");
        if (shootMode == ShootMode.Raycast)
            ; // RaycastShoot();
        else if (shootMode == ShootMode.RigidBody)
            RigidbodyShoot();
        StartCoroutine(ShootCooldown());
    }

    public abstract void RigidbodyShoot();
    //public abstract void RaycastShoot();

    protected void GetShootData(out Vector3 origin, out Vector3 direction)
    {
        origin = shootPoint != null ? shootPoint.position : transform.position + transform.forward * 1.2f;

        if (cameraController == null && Object != null)
            cameraController = Object.transform.GetComponentInChildren<CameraController>(true);

        if (cameraController != null)
        {
            // En el host, AimDirection/CamPosition solo se actualizan en Render: usar simulacion.
            Vector3 aimDir = cameraController.GetSimulationAimDirection();
            Vector3 camPos = cameraController.GetSimulationCameraWorldPosition();
            Vector3 targetPoint = camPos + aimDir * raycastRange;

            if (Physics.Raycast(new Ray(camPos, aimDir), out RaycastHit hit, raycastRange, Layers))
                targetPoint = hit.point;

            direction = (targetPoint - origin).normalized;
            if (direction.sqrMagnitude < 0.0001f)
                direction = shootPoint != null ? shootPoint.forward : transform.forward;
            return;
        }

        direction = shootPoint != null ? shootPoint.forward : transform.forward;
    }

    public virtual void ChangeShootMode()
    {
        if (shootMode == ShootMode.Raycast)
            shootMode = ShootMode.RigidBody;
        else
            shootMode = ShootMode.Raycast;
    }

    public virtual IEnumerator ShootCooldown()
    {
        onShootCooldown = true;
        yield return new WaitForSeconds(fireRate);
        onShootCooldown = false;
    }

    public virtual void Reload()
    {
        if (isReloading) return;
        if (ammoInStock <= 0)
        {
            Debug.Log("No ammo in stock");
            return;
        }
        if (actualAmmo >= maxAmmoCapacity) return;
        StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        int ammoNeeded = maxAmmoCapacity - actualAmmo;
        if (ammoInStock >= ammoNeeded)
        {
            actualAmmo += ammoNeeded;
            ammoInStock -= ammoNeeded;
        }
        else
        {
            actualAmmo += ammoInStock;
            ammoInStock = 0;
        }
        isReloading = false;
    }
}

public enum ShootMode
{
    Raycast,
    RigidBody
}