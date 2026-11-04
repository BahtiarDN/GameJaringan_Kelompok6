using System;
using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// ScoreManager: skor CO-OP BERSAMA untuk seluruh tim.
///
/// - Skor naik saat musuh mati: EnemyController mem-broadcast event statis
///   OnEnemyDeath(scorePerKill) di Server, dan ScoreManager yang me-subscribe
///   event itu menambahkan skor (fungsi AddScore).
/// - skor disimpan dalam NetworkVariable sehingga semua client melihat skor
///   yang SAMA dan update otomatis.
/// - High Score "dua lapis":
///   1. Database (backend Express+SQLite): skor akhir dikirim via
///      POST /api/score/save saat GameOver, high score ditarik dari
///      GET /api/score/highscore/:userId saat game start.
///   2. PlayerPrefs lokal sebagai fallback (kalau backend mati) dan tetap
///      disimpan tiap rekor baru tercapai, saat GameOver, dan saat quit.
///
/// Setup di Inspector:
/// - GameObject "ScoreManager" di scene HARUS punya NetworkObject (sudah dipasang).
/// - Drag TextMeshProUGUI "Score" dan "High Score" ke slot yang tersedia.
/// </summary>
public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI highScoreText;

    [Header("Storage Settings")]
    [Tooltip("Key PlayerPrefs untuk High Score lokal (fallback kalau backend mati).")]
    [SerializeField] private string highScoreKey = "HighScore_Kelompok6";

    [Header("Backend Settings (Database)")]
    [Tooltip("URL GameBackend (Express + SQLite). Harus sesuai port server.js (default 5000).")]
    [SerializeField] private string backendBaseUrl = "http://localhost:5000";
    [Tooltip("Key PlayerPrefs untuk ID pemain lokal yang dikirim ke database.")]
    [SerializeField] private string userIdKey = "UserId_Kelompok6";

    private int localUserId;
    private bool hasSentFinalScore; // agar skor akhir tidak terkirim 2x

    // Skor co-op bersama: Server yang menulis, semua Client membaca
    public NetworkVariable<int> currentScore = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // --- API publik ---
    public int HighScore { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<int> OnHighScoreChanged;

    private void Awake()
    {
        // Pola singleton yang sama dengan GameManager / RespawnManager
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Muat High Score yang tersimpan di perangkat ini (fallback lokal)
        HighScore = PlayerPrefs.GetInt(highScoreKey, 0);

        // Ambil/Scan userId lokal yang persisten untuk dikirim ke database
        EnsureLocalUserId();

        // High Score asli diambil dari DATABASE (backend). Kalau backend mati,
        // nilai PlayerPrefs di atas tetap dipakai sebagai fallback.
        ScoreBackend.GetHighScore(backendBaseUrl, localUserId, backendHighScore =>
        {
            if (backendHighScore < 0) return; // gagal -> biarkan nilai lokal

            if (backendHighScore > HighScore)
            {
                HighScore = backendHighScore;
                PlayerPrefs.SetInt(highScoreKey, HighScore);
                PlayerPrefs.Save();
                UpdateHighScoreUI(HighScore);
                OnHighScoreChanged?.Invoke(HighScore);
            }
            Debug.Log($"[ScoreManager] High score dari database: {backendHighScore}");
        });
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Semua client (termasuk server) ikut meng-update UI saat skor tersinkron
        currentScore.OnValueChanged += HandleScoreChanged;

        if (IsServer)
        {
            // FIX SKOR: subscribe event kematian musuh -> tambahkan skor
            EnemyController.OnEnemyDeath += HandleEnemyDeath;
        }

        // Inisialisasi tampilan UI awal
        UpdateScoreUI(currentScore.Value);
        UpdateHighScoreUI(HighScore);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        currentScore.OnValueChanged -= HandleScoreChanged;

        if (IsServer)
        {
            EnemyController.OnEnemyDeath -= HandleEnemyDeath;
        }
    }

    // ==================================================
    // SKOR (PANGGILAN DARI EVENT KEMATIAN MUSUH)
    // ==================================================

    private void HandleEnemyDeath(int scoreToAdd)
    {
        AddScore(scoreToAdd);
    }

    private void HandleScoreChanged(int previousValue, int newValue)
    {
        UpdateScoreUI(newValue);
        OnScoreChanged?.Invoke(newValue);
        CheckHighScore(newValue);
    }

    /// <summary>Fungsi penambahan skor dipanggil di Server (oleh event enemy death).</summary>
    public void AddScore(int amount)
    {
        if (!IsServer) return;
        if (amount <= 0) return;

        // NetworkVariable otomatis: pada server langsung trigger HandleScoreChanged,
        // pada client ter-trigger saat nilai tersinkron dari server.
        currentScore.Value += amount;
    }

    private void CheckHighScore(int value)
    {
        if (value > HighScore)
        {
            HighScore = value;
            UpdateHighScoreUI(HighScore);
            OnHighScoreChanged?.Invoke(HighScore);
            SaveHighScore(); // langsung simpan tiap rekor baru tercapai
        }
    }

    // ==================================================
    // PENYIMPANAN & UI
    // ==================================================

    /// <summary>Simpan High Score ke penyimpanan lokal (PlayerPrefs).</summary>
    public void SaveHighScore()
    {
        PlayerPrefs.SetInt(highScoreKey, HighScore);
        PlayerPrefs.Save();
    }

    /// <summary>Reset High Score (untuk tombol debug di Inspector via ContextMenu).</summary>
    [ContextMenu("Reset High Score")]
    public void ResetHighScore()
    {
        HighScore = 0;
        PlayerPrefs.DeleteKey(highScoreKey);
        PlayerPrefs.Save();
        UpdateHighScoreUI(HighScore);
    }

    private void HandleGameOver(ulong winnerClientId)
    {
        // PENYIMPANAN LOKAL DIJALANKAN DULU dan dijamin sukses - inilah fallback
        // yang diminta modul: kalau backend mati/tidak terjangkau, High Score
        // lokal (PlayerPrefs) TIDAK ikut gagal dan game tidak freeze.
        SaveHighScore();

        // KIRIM SKOR PERTANDINGAN KE DATABASE (sekali per pemain per game).
        // Skor co-op bersama dikirim oleh tiap mesin dengan userId masing-masing,
        // sehingga tabel scores per user tetap rapi di backend.
        if (!hasSentFinalScore)
        {
            hasSentFinalScore = true;
            int finalScore = currentScore.Value;
            Debug.Log($"[ScoreManager] Game over - kirim skor akhir {finalScore} ke backend (userId={localUserId})...");
            // Tidak ada try/catch dan tidak perlu: SaveScore 100% callback,
            // kegagalan jaringan hanya menghasilkan log warning, bukan error/crash.
            ScoreBackend.SaveScore(backendBaseUrl, localUserId, finalScore, (ok, message) =>
            {
                if (!ok)
                {
                    // Sudah dijelaskan ScoreBackend: skor lokal tetap aman.
                    Debug.LogWarning($"[ScoreManager] Kirim skor ke backend gagal - High Score lokal tetap dipakai.");
                }
            });
        }
    }

    /// <summary>
    /// Ambil userId lokal dari PlayerPrefs; dibuat sekali lalu dipakai selamanya.
    /// (Sistem login belum diimplementasikan di backend - register/login baru stub,
    /// jadi untuk sekarang ID acak yang persisten dipakai sebagai identitas pemain.)
    /// </summary>
    private void EnsureLocalUserId()
    {
        localUserId = PlayerPrefs.GetInt(userIdKey, 0);
        if (localUserId <= 0)
        {
            // ID acak 1000 - 999999, disimpan permanen di perangkat ini
            localUserId = UnityEngine.Random.Range(1000, 1000000);
            PlayerPrefs.SetInt(userIdKey, localUserId);
            PlayerPrefs.Save();
            Debug.Log($"[ScoreManager] userId baru dibuat: {localUserId}");
        }
    }

    /// <summary>Menampilkan Score saat ini ke UI Text/TextMeshPro.</summary>
    private void UpdateScoreUI(int value)
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + value;
        }
    }

    /// <summary>Menampilkan High Score ke UI Text/TextMeshPro.</summary>
    private void UpdateHighScoreUI(int value)
    {
        if (highScoreText != null)
        {
            highScoreText.text = "High Score: " + value;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }

        // Jaga-jaga: pastikan High Score tersimpan saat objek dimusnahkan
        SaveHighScore();
    }
}
