using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Button restartButton;
    private void Start()
    {
        gameOverPanel.SetActive(false);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
        restartButton.onClick.AddListener(() =>
        {
            // Shutdown network koneksi dan kembali ke awal
            NetworkManager.Singleton.Shutdown();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        });
    }
    private void HandleGameOver(ulong winnerClientId)
    {
        gameOverPanel.SetActive(true);
        if (NetworkManager.Singleton.LocalClientId == winnerClientId)
        {
            winnerText.text = "YOU WIN!";
            winnerText.color = Color.green;
        }
        else
        {
            winnerText.text = "YOU LOSE!";
            winnerText.color = Color.red;
        }
    }
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }
    }
}