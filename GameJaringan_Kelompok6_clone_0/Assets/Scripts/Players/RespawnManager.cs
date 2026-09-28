using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [SerializeField] private NetworkObject playerPrefab; // drag PlayerPrefabs ke sini
    [SerializeField] private float respawnDelay = 3f;

    private void Awake()
    {
        Instance = this;
    }

    // Hanya dipanggil di Server
    public void HandleDeath(ulong clientId, int savedScore, NetworkObject deadPlayer)
    {
        deadPlayer.Despawn(); // otomatis menghancurkan objeknya juga
        StartCoroutine(RespawnRoutine(clientId, savedScore));
    }

    private IEnumerator RespawnRoutine(ulong clientId, int savedScore)
    {
        yield return new WaitForSeconds(respawnDelay);

        var nm = NetworkManager.Singleton;
        // Batalkan kalau client sudah keluar saat menunggu
        if (nm == null || !nm.IsServer || !nm.ConnectedClients.ContainsKey(clientId)) yield break;

        NetworkObject newPlayer = Instantiate(playerPrefab);
        newPlayer.SpawnAsPlayerObject(clientId, true);

        // Skor tidak hilang karena mati
        newPlayer.GetComponent<PlayerController>().score.Value = savedScore;
    }
}