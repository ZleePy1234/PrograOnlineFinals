using Fusion;
using UnityEngine;

public static class CombatTeamHelper
{
    public static int GetTeamId(PlayerRef player)
    {
        if (player == PlayerRef.None)
            return 0;
        return Mathf.Abs(player.PlayerId) % 2;
    }
}
