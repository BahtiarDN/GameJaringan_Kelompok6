using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// EnemySpawner: hanya berjalan di Server dan hanya mengerjakan 1 wave.
/// Dimulai otomatis saat GameManager masuk state Gameplay, dan berhenti
/// saat GameOver atau ketika kuota total 100 enemy sudah habis di-spawn.
///
/// Setup di Inspector:
/// - Pasang di satu GameObject di scene (di sisi Host/Server).
/// - Drag Enemy prefab (harus berisi NetworkObject) ke enemyPrefab.
/// - gameManager bisa dikosongkan (otomatis diambil dari singleton).
/// </summary>
public class EnemySpawner : NetworkBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [Header("Wave Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("Total enemy yang muncul untuk 1 wave (spesifikasi: 100).")]
    [SerializeField] private int totalEnemiesPerWave = 100;

    // Diatur dari Inspector: jeda kemunculan tiap musuh (spesifikasi modul)
    public float spawnDelay = 1.5f;

    [Tooltip("Batas jumlah musuh yang HIDUP bersamaan di arena. 0 = tanpa batas.")]
    [SerializeField] private int maxAliveEnemies = 20;

    [Header("Spawn Area Settings (Game Pesawat Linear)")]
    [Tooltip("Titik tengah area spawn. Kosongkan untuk pakai posisi spawner ini.")]
    [SerializeField] private Transform spawnCenter;
    [Tooltip("Sebaran horizontal spawn musuh (setengah lebar arena).")]
    [SerializeField] private float spawnRadius = 5f;
    [Tooltip("Jarak di ATAS layar tempat musuh muncul (ditambah ke Y posisi spawn center).")]
    [SerializeField] private float spawnTopOffset = 7f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    // --- API Publik ---
    public int SpawnedCount { get; private set; }
    public int AliveCount { get; private set; }
    public bool IsWaveRunning { get; private set; }
    public bool IsWaveCompleted => waveCompleted;

    // --- Event untuk integrasi (UI / SFX / ScoreManager) ---
    public event Action<EnemyController> OnEnemySpawned;
    public event Action<EnemyController> OnEnemyDied;
    public event Action OnWaveCompleted;

    private bool waveCompleted;

    private void Awake()
    {
        // Pola singleton yang sama dengan RespawnManager
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameStateChanged;

            // Jika spawner nyempil di scene saat gameplay sudah jalan (mis. rejoin)
            if (gameManager.gameState.Value == GameManager.State.Gameplay)
            {
                StartWave();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameManager.State newState)
    {
        if (!IsServer) return;

        if (newState == GameManager.State.Gameplay)
        {
            StartWave();
        }
        else if (newState == GameManager.State.GameOver)
        {
            StopWave();
        }
    }

    // ==================================================
    // WAVE CONTROL
    // ==================================================

    public void StartWave()
    {
        // Garansi "hanya 1 wave": tidak bisa diulang / dobel start
        if (!IsServer || waveCompleted || IsWaveRunning) return;
        if (enemyPrefab == null)
        {
            Debug.LogWarning("[EnemySpawner] enemyPrefab belum diisi di Inspector!");
            return;
        }

        IsWaveRunning = true;
        StartCoroutine(SpawnWave());
    }

    public void StopWave()
    {
        if (!IsServer) return;

        IsWaveRunning = false;
        StopAllCoroutines();
    }

    private IEnumerator SpawnWave()
    {
        // Spawn SATU-PER-SATU setiap spawnDelay detik sampai kuota total tercapai.
        // Tidak pernah me-spawn banyak musuh di satu waktu.
        for (int i = 0; i < totalEnemiesPerWave; i++)
        {
            if (!IsWaveRunning) yield break;

            // Batas jumlah musuh hidup bersamaan: tahan spawn sampai ada yang mati
            if (maxAliveEnemies > 0)
            {
                while (AliveCount >= maxAliveEnemies)
                {
                    if (!IsWaveRunning) yield break;
                    yield return null;
                }
            }

            SpawnEnemy();
            yield return new WaitForSeconds(spawnDelay);
        }

        // Kuota habis -> wave selesai, spawner berhenti permanen
        IsWaveRunning = false;
        waveCompleted = true;
        Debug.Log($"[EnemySpawner] Wave selesai! Total enemy di-spawn: {SpawnedCount}");
        OnWaveCompleted?.Invoke();
    }

    private void SpawnEnemy()
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;

        // Game pesawat linear: musuh SELALU muncul dari ARAH ATAS arena.
        // Posisi X diacak (Random.Range) dalam batas arena (± spawnRadius)
        // agar musuh tidak bertumpuk di satu titik (overlapping).
        float spawnX = center.x + UnityEngine.Random.Range(-spawnRadius, spawnRadius);
        float spawnY = center.y + spawnTopOffset;
        Vector3 spawnPosition = new Vector3(spawnX, spawnY, 0f);

        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

        NetworkObject networkObject = enemyInstance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogWarning("[EnemySpawner] enemyPrefab tidak punya NetworkObject, spawn dibatalkan!");
            Destroy(enemyInstance);
            return;
        }

        networkObject.Spawn();

        // Track statistik & relay event kematian/kepergian enemy
        // (DUA jalur: mati ditembak, ATAU lolos melewati bawah layar.
        //  Kalau hanya jalur "mati", musuh yang lolos tidak mengurangi
        //  AliveCount dan batas musuh aktif bisa macet.)
        if (enemyInstance.TryGetComponent(out EnemyController enemy))
        {
            enemy.OnDied += HandleEnemyRemoved;
            enemy.OnDespawnedWithoutDeath += HandleEnemyRemoved;
            AliveCount++;

            OnEnemySpawned?.Invoke(enemy);

            // TODO: Panggil SFX spawn enemy di sini lewat event OnEnemySpawned.
        }

        SpawnedCount++;
    }

    private void HandleEnemyRemoved(EnemyController enemy)
    {
        // Cegah dobel-decrement (mati -> despawn bisa dua-duanya sempat memicu)
        enemy.OnDied -= HandleEnemyRemoved;
        enemy.OnDespawnedWithoutDeath -= HandleEnemyRemoved;
        AliveCount = Mathf.Max(0, AliveCount - 1);

        OnEnemyDied?.Invoke(enemy);
    }
}
