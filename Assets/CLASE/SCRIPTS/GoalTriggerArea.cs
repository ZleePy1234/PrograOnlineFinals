using Fusion;
using UnityEngine;

public class MetaTrigger : NetworkBehaviour
{
    public GameObject vfx;
    public float delayBorrado = 0.25f;

    [Networked]
    public NetworkBool metaLograda { get; set; }

    [Networked]
    public TickTimer timerFinal { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        if (Object.HasStateAuthority == false) return;

        if (metaLograda == false && other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();

            if (netObj != null)
            {
                metaLograda = true;
                RPC_SpawnEfecto(netObj.transform.position);
                timerFinal = TickTimer.CreateFromSeconds(Runner, delayBorrado);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            if (metaLograda && timerFinal.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnEfecto(Vector3 pos)
    {
        if (vfx != null)
        {
            GameObject instancia = Instantiate(vfx, pos, Quaternion.identity);

            ParticleSystem ps = instancia.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }

            Destroy(instancia, 10f);
        }
    }
}