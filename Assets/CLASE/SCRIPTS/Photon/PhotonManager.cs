using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using TMPro;

public class PhotonManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static PhotonManager Instance;

    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private UnityEvent onPlayerJoined;
    [SerializeField] private Transform spawnPoint;
    [Header("Spawns")]
    [Tooltip("Spawns por equipo. Índice 0 = equipo 0, índice 1 = equipo 1.")]
    [SerializeField] private Transform[] teamSpawns = new Transform[2];

    [Header("Legacy / fallback")]
    [Tooltip("Fallback si no asignas teamSpawns. Se elige por playerId % checkpoints.Length.")]
    [SerializeField] private Transform[] checkpoints;

    [Header("UI Respawn (global)")]
    [Tooltip("Panel/imagen que se activa mientras el jugador local está muerto.")]
    [SerializeField] private GameObject respawnOverlay;
    [Tooltip("TMP text para mostrar segundos restantes.")]
    [SerializeField] private TMP_Text respawnCountdownText;

    [Header("UI Victoria (global)")]
    [Tooltip("Panel/imagen que se activa cuando hay ganador.")]
    [SerializeField] private GameObject winOverlay;
    [Tooltip("TMP text para mostrar el equipo ganador.")]
    [SerializeField] private TMP_Text winText;

    private NetworkRunner runner;
    private readonly Dictionary<PlayerRef, NetworkObject> _playerObjects = new();

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        runner = gameObject.GetComponent<NetworkRunner>();
        if (respawnOverlay != null) respawnOverlay.SetActive(false);
        if (winOverlay != null) winOverlay.SetActive(false);
    }
    private void Start()
    {
        SceneManager.LoadScene(1,LoadSceneMode.Additive);
    }

    public void JoinGame()
    {
        StartGame(GameMode.Client);
        SceneManager.UnloadSceneAsync(1);
    }

    public void StartGame()
    {
        StartGame(GameMode.Host);
        SceneManager.UnloadSceneAsync(1);
    }


    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        onPlayerJoined?.Invoke();

        if (runner.IsServer)
        {
            if (_playerObjects.TryGetValue(player, out var existing) && existing != null && existing.IsValid)
            {
                Debug.LogWarning($"[RESPAWN] OnPlayerJoined: ya existe playerObject para Player:{player.PlayerId}, ignorando Spawn duplicado");
                return;
            }
            Transform spawn = GetCheckpointFor(player);
            if (spawn == null)
                spawn = spawnPoint;

            NetworkObject obj = runner.Spawn(playerPrefab, spawn.position, spawn.rotation, player);
            _playerObjects[player] = obj;
        }
    }


    public Transform GetCheckpointFor(PlayerRef player)
    {
        int teamId = CombatTeamHelper.GetTeamId(player);
        if (teamSpawns != null && teamSpawns.Length >= 2 && teamSpawns[teamId] != null)
        {
            Debug.Log($"[RESPAWN] GetCheckpointFor | playerId={player.PlayerId} team={teamId} -> teamSpawns[{teamId}]={teamSpawns[teamId].name}");
            return teamSpawns[teamId];
        }

        if (checkpoints != null && checkpoints.Length > 0)
        {
            int index = Mathf.Abs(player.PlayerId) % checkpoints.Length;
            if (checkpoints[index] != null)
            {
                Debug.Log($"[RESPAWN] GetCheckpointFor | playerId={player.PlayerId} team={teamId} -> checkpoints[{index}]={checkpoints[index].name}");
                return checkpoints[index];
            }
        }

        Debug.LogWarning($"[RESPAWN] GetCheckpointFor | playerId={player.PlayerId} team={teamId} -> FALLBACK spawnPoint={(spawnPoint != null ? spawnPoint.name : "NULL")}");
        return spawnPoint;
    }

    public void SetRespawnUI(bool visible, int secondsRemaining)
    {
        if (respawnOverlay != null && respawnOverlay.activeSelf != visible)
            respawnOverlay.SetActive(visible);

        if (!visible)
            return;

        if (respawnCountdownText != null)
            respawnCountdownText.text = Mathf.Max(0, secondsRemaining).ToString();
    }

    public void ShowWinScreen(int winningTeam)
    {
        if (winOverlay != null && !winOverlay.activeSelf)
            winOverlay.SetActive(true);

        if (winText != null)
            winText.text = $"Gano el equipo {winningTeam}";
    }

    /// <summary>
    /// Respawn robusto: despawnea el objeto actual y spawnea uno nuevo en el spawn del equipo.
    /// Debe llamarse solo desde servidor/state authority.
    /// </summary>
    public void RespawnPlayer(PlayerRef player, NetworkObject oldObject, float freezeSeconds)
    {
        if (runner == null || !runner.IsServer)
            return;

        Transform spawn = GetCheckpointFor(player);
        if (spawn == null)
            spawn = spawnPoint;

        Debug.Log($"[RESPAWN] RespawnPlayer(server) | playerId={player.PlayerId} despawnOld={(oldObject != null)} spawn={(spawn != null ? spawn.name : "NULL")} pos={(spawn != null ? spawn.position.ToString() : "n/a")}");

        if (oldObject != null && oldObject.IsValid)
            runner.Despawn(oldObject);

        NetworkObject newObj = runner.Spawn(playerPrefab, spawn.position, spawn.rotation, player);
        _playerObjects[player] = newObj;

        // Congelar al respawnear (contador) y luego habilitar movimiento al terminar.
        Health health = newObj != null ? newObj.GetComponent<Health>() : null;
        if (health != null)
        {
            health.Server_BeginRespawnFreeze(freezeSeconds);
        }
        else
        {
            Debug.LogWarning("[RESPAWN] RespawnPlayer(server): el prefab de Player no tiene componente Health");
        }
    }


    public void GetInput(NetworkRunner runner, NetworkInput input)
    {
        if (InputManager.Instance == null)
            return;

        InputInfo inputInfo = new InputInfo()
        {
            playerPos = InputManager.Instance.GetMoveInput(),
            lookDirection = InputManager.Instance.GetMouseDelta(),

            isMoving = InputManager.Instance.IsMoveInputPressed(),
            isMovingBackwards = InputManager.Instance.IsMovingBackwards(),
            isMovingOnXAxis = InputManager.Instance.IsMovingOnXAxis(),
            isRunInputPressed = InputManager.Instance.WasRunInputPressed(),
            fire = InputManager.Instance != null && InputManager.Instance.IsFiring(),
            carryingBox = InputManager.Instance != null && InputManager.Instance.IsCarryingBox()
        };

        input.Set(inputInfo);
    }
    public void OnConnectedToServer(NetworkRunner runner)
    {
      
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
       
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
       
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
      
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (InputManager.Instance == null)
            return;

        InputInfo inputInfo = new InputInfo()
        {
            playerPos = InputManager.Instance.GetMoveInput(),
            lookDirection = InputManager.Instance.GetMouseDelta(),

            isMoving = InputManager.Instance.IsMoveInputPressed(),
            isMovingBackwards = InputManager.Instance.IsMovingBackwards(),
            isMovingOnXAxis = InputManager.Instance.IsMovingOnXAxis(),
            isRunInputPressed = InputManager.Instance.WasRunInputPressed(),
            fire = InputManager.Instance != null && InputManager.Instance.IsFiring(),
            carryingBox = InputManager.Instance != null && InputManager.Instance.IsCarryingBox()
        };

        input.Set(inputInfo);
    }


    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
       
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
       
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
       
    }


    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        if (_playerObjects.TryGetValue(player, out var obj) && obj != null && obj.IsValid)
        {
            runner.Despawn(obj);
        }
        _playerObjects.Remove(player);
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        
    }


    private async Task StartGame(GameMode mode)
    {
        Cursor.visible = false;
        runner.ProvideInput = true;

        var scene = SceneRef.FromIndex(0);
        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);

        Debug.Log("Iniciando partida en modo: " + mode);

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "MiPartida",
            Scene = sceneInfo,
            PlayerCount = 4,
        });
    }
}
