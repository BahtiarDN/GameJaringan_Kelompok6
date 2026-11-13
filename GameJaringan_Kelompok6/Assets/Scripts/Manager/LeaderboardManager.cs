using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;
public class LeaderboardManager : MonoBehaviour
{
    [Header("API Config")]
    [SerializeField] private string baseURL = "http://localhost:5000/api";
    [Header("UI References")]
    [SerializeField] private Transform contentParent; // Drag GameObject 'Content' dari ScrollView
    [SerializeField] private GameObject leaderboardItemPrefab; // Drag 'LeaderboardItemPrefab' dari Assets
    [SerializeField] private Button refreshButton;

    private void Start()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(FetchLeaderboard);
        }
    }
    private void OnEnable()
    {
        // Otomatis memuat data saat panel dibuka
        FetchLeaderboard();
    }
    public void FetchLeaderboard()
    {
        StartCoroutine(GetLeaderboardRoutine());
    }
    private IEnumerator GetLeaderboardRoutine()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(baseURL + "/leaderboard"))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                LeaderboardResponse response =
               JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text);

                if (response != null && response.data != null)
                {
                    PopulateScrollView(response.data);
                }
            }
            else
            {
                Debug.LogError($"[LeaderboardManager] Gagal memuat leaderboard: { request.error}");
            }
        }
    }

    private void PopulateScrollView(System.Collections.Generic.List<LeaderboardEntry>
   entryList)
    {
        // 1. Bersihkan baris lama yang ada di ScrollView
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
        // 2. Instansiasi baris baru sesuai data dari server
        for (int i = 0; i < entryList.Count; i++)
        {
            GameObject newRow = Instantiate(leaderboardItemPrefab, contentParent);
            LeaderboardItemUI itemUI = newRow.GetComponent<LeaderboardItemUI>();
            if (itemUI != null)
            {
                int rank = i + 1;
                itemUI.SetData(rank, entryList[i].username, entryList[i].highScore);
            }
        }
        Debug.Log($"[LeaderboardManager] Berhasil memuat {entryList.Count} data leaderboard.");
    }
}