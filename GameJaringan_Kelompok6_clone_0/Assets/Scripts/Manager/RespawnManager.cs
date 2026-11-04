using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// RespawnManager: me-respawn player yang mati tanpa menghanguskan skornya.
/// Hanya dieksekusi di Server.
///
/// FIX WARNING "NetworkVariable is written to, but doesn't know its
/// NetworkBehaviour yet":
/// Sebelumnya skor ditulis langsung setelah SpawnAsPlayerObject. NGO hanya
/// mengenali NetworkVariable milik NetworkBehaviour SETELAH proses spawn
/// selesai, jadi penulisan langsung bisa memicu warning tersebut.
///
/// Pola yang benar (sesuai panduan modul):
/// 1. Simpan skor pending di sini lalu jadwalkan respawn.
/// 2. Pemain baru (PlayerController) MENULIS sendiri NetworkVariable-nya
///    di dalam OnNetworkSpawn() miliknya - saat itulah NetworkBehaviour
///    sudah terdaftar di server.
/// </summary>
public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [SerializeField] private NetworkObject playerPrefab; // drag PlayerPrefabs ke sini
    [SerializeField] private float respawnDelay = 3f;

    // Skor menunggu dipasang ke player hasil respawn (server-side only)
    private readonly Dictionary<ulong, int> pendingScoresByClientId = new Dictionary<ulong, int>();

    private void Awake()
    {
        Instance = this;
    }

    // Hanya dipanggil di Server
    public void HandleDeath(ulong clientId, int savedScore, NetworkObject deadPlayer)
    {
        // Skor TIDAK hilang karena mati: simpan dulu nilai skornya
        pendingScoresByClientId[clientId] = savedScore;

        deadPlayer.Despawn(); // otomatis menghancurkan objeknya juga
        StartCoroutine(RespawnRoutine(clientId));
    }

    private IEnumerator RespawnRoutine(ulong clientId)
    {
        yield return new WaitForSeconds(respawnDelay);

        var nm = NetworkManager.Singleton;
        // Batalkan kalau client sudah keluar saat menunggu
        if (nm == null || !nm.IsServer || !nm.ConnectedClients.ContainsKey(clientId))
        {
            pendingScoresByClientId.Remove(clientId); // jangan tumpuk sampah data
            yield break;
        }

        NetworkObject newPlayer = Instantiate(playerPrefab);
        newPlayer.SpawnAsPlayerObject(clientId, true);

        // HARUS TIDAK menulis score.Value di sini!
        // PlayerController sendiri yang membaca skor pending saat
        // OnNetworkSpawn() berjalan (lihat PlayerController.cs).
    }

    /// <summary>
    /// Dipanggil oleh PlayerController di DALAM OnNetworkSpawn() (server side).
    /// Mengeluarkan skor pending untuk client ini dan menghapusnya dari buffer
    /// (sekali pakai). Return false kalau ini spawn pertama (belum pernah mati)
    /// dan tidak ada skor pending.
    /// </summary>
    public bool TryConsumePendingScore(ulong clientId, out int savedScore)
    {
        return pendingScoresByClientId.TryGetValue(clientId, out savedScore)
            && pendingScoresByClientId.Remove(clientId);
    }
}
