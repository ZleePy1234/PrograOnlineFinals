using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameObject[] dontDestroyOnLoadObjs;

    [Header("Victoria")]
    [Tooltip("Se invoca con el índice de equipo ganador (0 o 1). Conecta UI aquí.")]
    [SerializeField] private UnityEvent<int> onTeamWon;

    private void Awake()
    {
        Instance = this;
        foreach (GameObject go in dontDestroyOnLoadObjs)
        {
            DontDestroyOnLoad(go);
        }
    }

    public void CashoutComplete(int destroyedTowerTeamIndex)
    {
        int winningTeam = destroyedTowerTeamIndex == 0 ? 1 : 0;
        onTeamWon?.Invoke(winningTeam);
        if (PhotonManager.Instance != null)
            PhotonManager.Instance.ShowWinScreen(winningTeam);
        Debug.Log($"Equipo {winningTeam} Deposito el Vault.");
    }
}