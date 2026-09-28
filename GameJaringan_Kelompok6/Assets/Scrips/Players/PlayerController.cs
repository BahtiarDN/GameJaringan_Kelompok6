using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI; // Wajib untuk UI Slider
using TMPro;          // Wajib untuk TextMeshPro

public class PlayerController : NetworkBehaviour
{
    [Header("Network Sync Variables")]
    public NetworkVariable<int> health = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("UI References")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 1. Mendaftarkan Event Listener (panggilan otomatis saat nilai berubah)
        health.OnValueChanged += OnHealthChanged;
        score.OnValueChanged += OnScoreChanged;

        // 2. Inisialisasi Tampilan UI Awal saat karakter muncul
        UpdateHealthUI(health.Value);
        UpdateScoreUI(score.Value);

        // 3. Warna karakter berdasarkan status Ownership
        if (IsOwner)
        {
            GetComponent<Renderer>().material.color = Color.blue;

            // Spawn posisi acak 3D
            float randomX = Random.Range(-3f, 3f);
            float randomZ = Random.Range(-3f, 3f);
            transform.position = new Vector3(randomX, 0.5f, randomZ);
        }
        else
        {
            GetComponent<Renderer>().material.color = Color.red;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // SELALU cabut listener untuk mencegah memory leak saat objek dihancurkan
        health.OnValueChanged -= OnHealthChanged;
        score.OnValueChanged -= OnScoreChanged;
    }

    // Callback saat health berubah dari jaringan
    private void OnHealthChanged(int previousValue, int newValue)
    {
        UpdateHealthUI(newValue);
    }

    // Callback saat score berubah dari jaringan
    private void OnScoreChanged(int previousValue, int newValue)
    {
        UpdateScoreUI(newValue);
    }

    private void UpdateHealthUI(int currentHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth;
        }
    }

    private void UpdateScoreUI(int currentScore)
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Gerakan Player
        HandleMovement();

        // Testing Input Game (K = Kurang HP, L = Tambah Score)
        if (Input.GetKeyDown(KeyCode.K))
        {
            TakeDamageServerRpc(10);
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            AddScoreServerRpc(5);
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (moveDirection.magnitude >= 0.1f)
        {
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
        }
    }

    // --- LOGIKA MULTIPLAYER SERVER-AUTHORITATIVE ---
    // Karena WritePermission diset ke Server, Client TIDAK BISA mengubah health.Value langsung.
    // Client meminta Server mengubah nilainya via ServerRpc!

    [ServerRpc]
    private void TakeDamageServerRpc(int damageAmount)
    {
        health.Value = Mathf.Max(0, health.Value - damageAmount);
    }

    [ServerRpc]
    private void AddScoreServerRpc(int scoreAmount)
    {
        score.Value += scoreAmount;
    }
}