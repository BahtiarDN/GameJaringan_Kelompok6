using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// ScoreBackend: client HTTP sederhana untuk GameBackend (Express + SQLite).
///
/// STATIS - tidak perlu dipasang di scene / di Inspector.
/// Endpoint yang dipakai (sesuai server.js):
/// - POST /api/score/save            body: {userId, score}        -> {status, message, scoreId}
/// - GET  /api/score/highscore/:userId                            -> {status, userId, highScore}
///
/// Cara kerja:
/// - Memakai UnityWebRequest + callback .completed (tidak butuh Coroutine/MonoBehaviour).
/// - Callback SELALU dijalankan di main thread Unity (aman untuk update UI).
/// - Kalau server mati/timeout, callback menerima `success = false` dan
///   game TETAP jalan (PlayerPrefs menjadi fallback penyimpanan skor).
///
/// Cara pakai:
///   ScoreBackend.SaveScore("http://localhost:5000", userId, 1200, (ok, msg) => Debug.Log(msg));
///   ScoreBackend.GetHighScore("http://localhost:5000", userId, hs => { ... });
/// </summary>
public static class ScoreBackend
{
    /// <summary>URL dasar GameBackend. Ubah di sini kalau server berpindah host/port.</summary>
    public const string DefaultBaseUrl = "http://localhost:5000";

    private const float TimeoutSeconds = 10f;

    // ==================================================
    // POST /api/score/save - SIMPAN SKOR PERTANDINGAN
    // ==================================================
    public static void SaveScore(string baseUrl, int userId, int score, Action<bool, string> onDone = null)
    {
        // Validasi sederhana sebelum mengirim
        if (score < 0 || userId <= 0)
        {
            onDone?.Invoke(false, "userId/score tidak valid - skor tidak dikirim ke server.");
            return;
        }

        var payload = new ScoreSaveRequest { userId = userId, score = score };
        byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        UnityWebRequest request = new UnityWebRequest(
            $"{baseUrl.TrimEnd('/')}/api/score/save", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = (int)TimeoutSeconds;

        request.SendWebRequest().completed += _ =>
        {
            bool success = request.result == UnityWebRequest.Result.Success;
            string message;

            if (success)
            {
                // Server membalas {status, message, scoreId}
                ScoreSaveResponse response = ParseJson<ScoreSaveResponse>(request.downloadHandler.text);
                message = response != null && !string.IsNullOrEmpty(response.message)
                    ? $"Skor tersimpan di database (scoreId={response.scoreId})."
                    : "Skor tersimpan di database.";

                Debug.Log($"[ScoreBackend] {message}");
            }
            else
            {
                // FALLBACK AMAN: kegagalan koneksi (server mati / timeout)
                // TIDAK error dan TIDAK freeze - hanya log kuning. High Score
                // sudah PASTI tersimpan di PlayerPrefs oleh ScoreManager
                // SEBELUM fungsi ini dipanggil, jadi flow game tidak terganggu.
                message = "Gagal kirim skor ke backend (server mati/tidak terjangkau). " +
                          "Skor lokal tetap aman di PlayerPrefs.";
                Debug.LogWarning($"[ScoreBackend] {message}");
            }

            onDone?.Invoke(success, message);
            request.Dispose();
        };
    }

    // ==================================================
    // GET /api/score/highscore/:userId - HIGH SCORE PERSONAL
    // ==================================================
    /// <summary>
    /// Ambil high score personal dari database.
    /// onDone menerima nilai high score (>= 0) saat sukses, atau -1 saat gagal.
    /// </summary>
    public static void GetHighScore(string baseUrl, int userId, Action<int> onDone)
    {
        if (userId <= 0)
        {
            onDone?.Invoke(-1);
            return;
        }

        UnityWebRequest request = UnityWebRequest.Get(
            $"{baseUrl.TrimEnd('/')}/api/score/highscore/{userId}");
        request.timeout = (int)TimeoutSeconds;

        request.SendWebRequest().completed += _ =>
        {
            int highScore = -1;

            if (request.result == UnityWebRequest.Result.Success)
            {
                HighScoreResponse response = ParseJson<HighScoreResponse>(request.downloadHandler.text);
                if (response != null && response.highScore >= 0)
                {
                    highScore = response.highScore;
                }
            }
            else
            {
                Debug.LogWarning($"[ScoreBackend] Gagal ambil high score dari backend: {request.error}");
            }

            onDone?.Invoke(highScore);
            request.Dispose();
        };
    }

    // --- Util: parsing JSON aman (JSON yang tidak valid -> null) ---
    private static T ParseJson<T>(string json) where T : class
    {
        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
