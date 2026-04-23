using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class Tower : NetworkBehaviour, IDamagable
{
    [Header("Equipo")]
    [Tooltip("0 o 1. Debe coincidir con CombatTeamHelper (PlayerId % 2) del equipo que defiende esta torre.")]
    [SerializeField] private int teamIndex;

    [Header("UI")]
    [SerializeField] private Image healthBar;

    [Header("Stats")]
    [SerializeField] private byte life = 100;
    [Networked] private byte CurrentLife { get; set; }

    public int TeamIndex => teamIndex;

    public bool IsAllyTeam(int shooterTeamId) => teamIndex == shooterTeamId;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            CurrentLife = life;
        UpdateUI();
    }

    public void TakeDamage(byte damage)
    {
        if (Runner == null || Object == null || !Object.IsValid)
            return;

        Debug.Log($"[COMBAT] Tower.TakeDamage (RPC) | {gameObject.name} team={teamIndex} dmg={damage}");
        RPC_TakeDamage(damage);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(byte damage)
    {
        if (CurrentLife == 0)
        {
            Debug.Log("[COMBAT] Tower.RPC_TakeDamage: torre ya destruida, ignorar");
            return;
        }
        int prev = CurrentLife;
        int newLife = Mathf.Max(0, CurrentLife - damage);
        CurrentLife = (byte)newLife;
        Debug.Log($"[COMBAT] Tower.RPC_TakeDamage | team={teamIndex} | vida {prev} -> {CurrentLife} (dmg={damage})");

        if (CurrentLife == 0)
        {
            Debug.Log("[COMBAT] Torre cayo a 0 — ocultar y notificar");
            RPC_TowerDestroyedVisual(teamIndex);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TowerDestroyedVisual(int losingTeamIndex)
    {
        gameObject.SetActive(false);
        if (GameManager.Instance != null)
            GameManager.Instance.CashoutComplete(losingTeamIndex);
    }

    public override void Render()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (healthBar != null && life > 0)
            healthBar.fillAmount = (float)CurrentLife / life;
    }
}
