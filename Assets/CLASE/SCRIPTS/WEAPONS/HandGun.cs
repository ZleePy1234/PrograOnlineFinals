using Fusion;
using UnityEngine;

public class Handgun : Weapon
{
    public override void RigidbodyShoot()
    {
        GetShootData(out Vector3 origin, out Vector3 direction);

        if (Object.HasStateAuthority)
        {
            SpawnBullet(origin, direction);
        }
        else
        {
            RPC_Shoot(origin, direction);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Shoot(Vector3 origin, Vector3 direction)
    {
        SpawnBullet(origin, direction);
    }

    private void SpawnBullet(Vector3 origin, Vector3 direction)
    {
        NetworkObject bullet = Runner.Spawn(
            proyectil,
            origin,
            Quaternion.LookRotation(direction),
            Object.InputAuthority
        );

        if (bullet == null)
        {
            Debug.LogError("[COMBAT] Spawn falló");
            return;
        }

        Debug.Log($"[COMBAT] Bala creada en SERVER | id={bullet.Id}");

        Projectile projectile = bullet.GetComponent<Projectile>();

        if (projectile != null)
            projectile.Initialize(direction, bulletForce, Object.InputAuthority);
    }

  

    public override void Reload()
    {
        base.Reload();
    }
}