using UnityEngine;
using TMPro; // bisa disesuaikan menggunakan library yang lain jika gagal
public class LeaderboardItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    public void SetData(int rank, string username, int score)
    {
        if (rankText != null) rankText.text = $"#{rank}";
        if (usernameText != null) usernameText.text = username;
        if (scoreText != null) scoreText.text = $"{score} Pts";
    }
}