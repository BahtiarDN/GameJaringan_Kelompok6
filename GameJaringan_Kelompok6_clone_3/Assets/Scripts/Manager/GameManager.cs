using System;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    public enum State
    {
        WaitingForPlayers,
        Countdown,
        Gameplay,
        GameOver
    }
    // NetworkVariable untuk menyinkronkan status game saat ini ke semua Client
    public NetworkVariable<State> gameState = new NetworkVariable<State>(
    State.WaitingForPlayers,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );
    [Header("Match Settings")]
    [SerializeField] private float countdownTimer = 3f;
    public event Action<State> OnGameStateChanged;
    public event Action<ulong> OnGameOver; // Mengirimkan ClientId pemenang

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        gameState.OnValueChanged += (oldState, newState) =>
        {
            OnGameStateChanged?.Invoke(newState);
        };
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Jika sudah ada 2 player terhubung, mulai hitung mundur
        if (IsServer && NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
        {
            gameState.Value = State.Countdown;
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        switch (gameState.Value)
        {
            case State.Countdown:
                countdownTimer -= Time.deltaTime;
                if (countdownTimer <= 0f)
                {
                    gameState.Value = State.Gameplay;
                }
                break;
            case State.Gameplay:
                // Logika pemantauan gameplay aktif
                break;
            case State.GameOver:
                break;
        }
    }

    // Method dipanggil oleh Server saat ada player yang tereliminasi
    public void CheckWinConditionOnPlayerDeath(ulong deadPlayerClientId)
    {
        if (!IsServer || gameState.Value != State.Gameplay) return;
        // Cari pemenang (player lain yang bukan deadPlayerClientId)
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != deadPlayerClientId)
            {
                ulong winnerClientId = client.ClientId;
                gameState.Value = State.GameOver;
                NotifyGameOverClientRpc(winnerClientId);
                break;
            }
        }
    }

    [ClientRpc]
    private void NotifyGameOverClientRpc(ulong winnerClientId)
    {
        OnGameOver?.Invoke(winnerClientId);
    }
}
