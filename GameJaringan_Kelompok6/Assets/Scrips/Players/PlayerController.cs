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

    [Header("Combat Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private ParticleSystem muzzleFlashVFX;

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
            float randomY = Random.Range(-3f, 3f);
            transform.position = new Vector3(randomX, randomY);
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

        // Pemicuan aksi tembak menggunakan Klik Kiri / Tombol Space
        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space))
        {
            // Kirim perintah dari Client ke Server
            ShootServerRpc();
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector2(horizontal, vertical).normalized;

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

    // 1. SERVER RPC: Dipanggil oleh Client, dieksekusi di Server
    [ServerRpc]
    private void ShootServerRpc()
    {
        if (bulletPrefab == null || firePoint == null) return;
        // Server melakukan Spawn peluru nyata di dunia permainan
        GameObject bulletInstance = Instantiate(bulletPrefab, firePoint.position,
       firePoint.rotation);

        // Daftarkan peluru ke dalam jaringan NGO
        NetworkObject bulletNetworkObject = bulletInstance.GetComponent<NetworkObject>();
        bulletNetworkObject.Spawn();
        // Minta semua Client untuk memutar efek visual & suara tembakan
        PlayShootEffectsClientRpc();
    }
    // 2. CLIENT RPC: Dipanggil oleh Server, dieksekusi di seluruh Client
    [ClientRpc]
    private void PlayShootEffectsClientRpc()
    {
        // Putar efek partikel di lokasi tembakan jika ada
        if (muzzleFlashVFX != null)
        {
            muzzleFlashVFX.Play();
        }
        // Opsional: Tambahkan efek suara tembakan (AudioSource.PlayClipAtPoint)
    }
    // Method publik untuk menerima damage (dieksekusi di Server)
    public void TakeDamage(int amount)
    {
        if (!IsServer) return;
        // Kurangi nilai NetworkVariable health
        health.Value = Mathf.Max(0, health.Value - amount);
    }
}