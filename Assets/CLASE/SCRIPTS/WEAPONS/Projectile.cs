using Fusion;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    private Rigidbody rb;

    [SerializeField] private byte damage;
    private PlayerRef shooter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void Spawned()
    {
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public void Initialize(Vector3 direction, float force, PlayerRef shooterRef)
    {
        shooter = shooterRef;

        if (rb != null)
            rb.linearVelocity = direction * force;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Health health = collision.collider.GetComponentInParent<Health>();
        Tower tower = collision.collider.GetComponentInParent<Tower>();

        if (health != null)
        {
            // No daño a uno mismo ni a aliados
            if (health.Object != null && health.Object.IsValid)
            {
                if (health.Object.InputAuthority == shooter)
                    return;

                int shooterTeamId = CombatTeamHelper.GetTeamId(shooter);
                int targetTeamId = CombatTeamHelper.GetTeamId(health.Object.InputAuthority);
                if (shooterTeamId == targetTeamId)
                    return;
            }

            health.TakeDamage(damage);
        }
        else if (tower != null)
        {
            int shooterTeamId = CombatTeamHelper.GetTeamId(shooter);
            if (!tower.IsAllyTeam(shooterTeamId))
                tower.TakeDamage(damage);
        }

        Runner.Despawn(Object);
    }
}