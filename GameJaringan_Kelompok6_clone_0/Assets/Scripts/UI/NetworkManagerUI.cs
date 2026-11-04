using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NetworkManagerUI: tombol Host/Client + teks status koneksi.
///
/// Perilaku status (biar tidak menutupi area gameplay):
/// - Saat menekan tombol -> status muncul ("Connecting as CLIENT..." dll).
/// - Saat BERHASIL terhubung -> status muncul sekilas lalu AUTO-HIDE
///   (gameObject.SetActive(false)) selama game berjalan.
/// - Saat terputus -> status muncul kembali ("Status: Disconnected").
///
/// Setup di Inspector:
/// - Drag tombol Host/Client dan TMP status (semua sudah ter-link di scene).
/// - Posisi & ukuran teks status diatur di RectTransform TMP-nya
///   (lihat panduan anchoring di bawah komentar class ini).
/// </summary>
public class NetworkManagerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Status Settings")]
    [Tooltip("Berapa lama status 'Connected' tampil sebelum disembunyikan (detik).")]
    [SerializeField] private float statusHideDelay = 2f;

    private void Awake()
    {
        // Menambahkan listener event pada tombol UI
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
    }

    private void Start()
    {
        // Event jaringan untuk auto-hide / auto-show status
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void OnDestroy()
    {
        // SELALU cabut listener saat dimusnahkan (pola yang sama dengan GameOverUI)
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void OnHostButtonClicked()
    {
        // Menjalankan fungsi StartHost dari NetworkManager NGO
        if (NetworkManager.Singleton.StartHost())
        {
            UpdateUIStatus("Status: Connected as HOST");
        }
        else
        {
            UpdateUIStatus("Status: Failed to Start Host");
        }
    }

    private void OnClientButtonClicked()
    {
        // Menjalankan fungsi StartClient dari NetworkManager NGO
        if (NetworkManager.Singleton.StartClient())
        {
            UpdateUIStatus("Status: Connecting as CLIENT...");
        }
        else
        {
            UpdateUIStatus("Status: Failed to Start Client");
        }
    }

    private void UpdateUIStatus(string message)
    {
        if (statusText != null)
        {
            // Pastikan teks aktif kembali (mis. sebelumnya disembunyikan)
            statusText.gameObject.SetActive(true);
            statusText.text = message;
        }

        // Menyembunyikan tombol pilihan setelah role dipilih
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
    }

    // ==================================================
    // AUTO-HIDE STATUS SAAT GAME SUDAH TERHUBUNG
    // ==================================================

    private void HandleClientConnected(ulong clientId)
    {
        UpdateUIStatus("Status: Connected!");

        // Tampilkan sebentar, lalu sembunyikan agar pandangan bebas
        StartCoroutine(HideStatusAfterSeconds(statusHideDelay));
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        // Munculkan kembali pesan ketika koneksi terputus
        UpdateUIStatus("Status: Disconnected");
    }

    private IEnumerator HideStatusAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }
    }
}
