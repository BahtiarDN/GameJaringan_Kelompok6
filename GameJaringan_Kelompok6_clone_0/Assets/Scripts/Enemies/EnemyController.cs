using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Controller Enemy untuk game Pesawat Co-op Linear (top-down / vertical scrolling).
/// Server-authoritative (pola yang sama dengan Bullet/GameManager project ini).
///
/// Setup prefab Enemy di Inspector (wajib):
/// - NetworkObject     : sinkronisasi spawn/despawn via jaringan.
/// - NetworkTransform  : agar gerakan lurus ke bawah terlihat di semua Client.
/// - MeshFilter+MeshRenderer : 3D primitive Capsule (mesh Capsule bawaan Unity).
///   Material harus OPAQUE (gunakan URP/Lit, jangan Standard agar tidak pink).
/// - CircleCollider2D / BoxCollider2D : supaya peluru player bisa mengenai musuh
///   (peluru masih 2D fisik, jadi collidernya tetap yang 2D).
/// - Rigidbody2D opsional (Kinematic). Peluru player sudah punya Rigidbody2D,
///   jadi event trigger tetap jalan walau musuh tanpa Rigidbody2D.
/// - EnemyHealthBar    : UI health bar world-space di atas kepala (auto-create).
///
/// Alur state: Descend (turun lurus) -> Attack (turun + menembak ke bawah) -> Die.
/// Semua state hanya dijalankan di Server; Client hanya menerima hasil sync-nya.
/// </summary>
public class EnemyController : NetworkBehaviour
{
    // ==================================================
    // STATE MACHINE GAME LINER
    // ==================================================
    public enum EnemyState
    {
        Descend, // musuh baru masuk layar turun lurus, belum menembak
        Attack,  // musuh sudah masuk zona serangan: turun + menembak ke bawah
        Die      // HP habis, menunggu despawn
    }

    [Header("Status (Read Only)")]
    [SerializeField] private EnemyState currentState = EnemyState.Descend;

    [Header("Health Settings")]
    [Tooltip("HP maksimum musuh (sanitasi: minimal 1).")]
    [SerializeField] private int maxHitPoints = 100;

    // HP disinkronkan Server -> Client supaya EnemyHealthBar di tiap client update
    public NetworkVariable<int> hp = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Movement Settings")]
    [Tooltip("Kecepatan turun lurus (sumbu -Y).")]
    [SerializeField] private float moveSpeed = 2.5f;
    [Tooltip("Posisi Y dunia di mana musuh mulai menembak (akan masuk state Attack).")]
    [SerializeField] private float startShootingY = 4f;
    [Tooltip("Posisi Y dunia di mana musuh hilang jika lolos tanpa terbunuh.")]
    [SerializeField] private float despawnBelowY = -7f;

    [Header("Shooting Settings")]
    [Tooltip("Prefab EnemyProjectile (harus punya NetworkObject + EnemyProjectile).")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("Titik lahir peluru. Kosongkan untuk pakai posisi musuh.")]
    [SerializeField] private Transform firePoint;
    [Tooltip("Jeda antar tembakan (detik).")]
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackTimer = 0f;

    [Header("Reward Settings")]
    [Tooltip("Poin yang ditambahkan ke ScoreManager (skor co-op bersama) saat musuh mati.")]
    [SerializeField] private int scorePerKill = 10;

    [Header("Death Settings")]
    [Tooltip("Delay sebelum enemy di-destroy/di-despawn setelah mati (untuk SFX/VFX).")]
    [SerializeField] private float despawnDelay = 0.3f;

    [Header("SFX Settings")]
    [SerializeField] private AudioClip attackSfx;   // suara musuh menembak
    [SerializeField] private AudioClip hitSfx;      // suara musuh TERKENA HIT (modul wajib 3 SFX: tembak/hit/hancur)
    [SerializeField] private AudioClip deathSfx;    // suara musuh hancur

    // --- API publik ---
    public EnemyState CurrentState => currentState;
    public int MaxHp => maxHitPoints;
    public int CurrentHp => hp.Value;
    public bool IsDead => currentState == EnemyState.Die;

    // --- Event instance (Spawner & lainnya) ---
    public event Action<EnemyController> OnDied;
    public event Action<EnemyController> OnAttacked;
    public event Action<EnemyController> OnTookDamage;
    public event Action<EnemyController> OnDespawnedWithoutDeath; // lolos bawah layar (tanpa mati)

    // --- Event statis yang diminta modul: dipakai ScoreManager ---
    // Param = jumlah skor yang harus ditambahkan. Hanya DI-INVOKE DI SERVER.
    public static event Action<int> OnEnemyDeath;

    private float timeSinceAttack;
    private Collider2D enemyCollider;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        enemyCollider = GetComponent<Collider2D>();

        if (!IsServer) return;

        // Sanitasi HP saat spawn di server lalu sinkronkan ke semua client
        if (maxHitPoints < 1) maxHitPoints = 1;
        if (attackCooldown < 0.05f) attackCooldown = 0.05f;
        hp.Value = maxHitPoints;

        currentState = EnemyState.Descend;
        timeSinceAttack = 0f;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Amankan Invoke yang mungkin masih nunggu (despawnDelay)
        CancelInvoke(nameof(DespawnSelf));

        // Musuh di-despawn TANPA mati (lolos melewati bawah layar).
        // HANYA di Server: Spawner butuh notifikasi ini supaya AliveCount
        // ikut berkurang dan batas musuh aktif tidak pernah macet.
        if (IsServer && currentState != EnemyState.Die)
        {
            OnDespawnedWithoutDeath?.Invoke(this);
        }
    }

    private void Update()
    {
        // Semua logika state machine hanya di Server (server-authoritative)
        if (!IsServer || IsDead) return;

        timeSinceAttack += Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Descend:
                UpdateDescend();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
            case EnemyState.Die:
                break;
        }

        // Musuh lolos melewati bawah layar -> despawn tanpa skor
        if (transform.position.y <= despawnBelowY)
        {
            DespawnSelf();
        }
    }

    // ==================================================
    // STATE MACHINE: TURUN LURUS + MENEMBAK
    // ==================================================

    private void UpdateDescend()
    {
        // Game pesawat linear: gerak LURUS ke bawah (Vector3.down), tidak mengejar player
        transform.position += Vector3.down * moveSpeed * Time.deltaTime;

        // Masuk zona serangan -> transisi ke state Attack
        if (transform.position.y <= startShootingY)
        {
            currentState = EnemyState.Attack;
            timeSinceAttack = attackCooldown; // tembakan pertama boleh instan
        }
    }

    private void UpdateAttack()
    {
        // Tetap turun lurus sambil menembak (linear shooter)
        transform.position += Vector3.down * moveSpeed * Time.deltaTime;

        // State "Attack" = musuh sedang aktif menembak secara berkala
        if (timeSinceAttack >= attackCooldown)
        {
            timeSinceAttack = 0f;
            Shoot();
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null) return;

        Transform origin = firePoint != null ? firePoint : transform;
        GameObject projectile = Instantiate(bulletPrefab, origin.position, Quaternion.identity);

        if (projectile.TryGetComponent(out EnemyProjectile enemyProjectile))
        {
            // Catat penembak agar peluru tidak mengenai musuh yang menembak
            enemyProjectile.shooter = this;
        }

        if (projectile.TryGetComponent(out NetworkObject projectileNetworkObject))
        {
            projectileNetworkObject.Spawn();

            // SFX tembakan di semua Client
            BroadcastAttackSfxClientRpc(origin.position);
        }

        OnAttacked?.Invoke(this);
    }

    // ==================================================
    // DAMAGE & DEATH (FIX: musuh bisa mati + skor dihitung)
    // ==================================================

    /// <summary>Penerima damage utama (sesuai spesifikasi modul).</summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, 0);
    }

    /// <summary>Overload kompatibel dengan Bullet lama (membawa id penyerang).</summary>
    public void TakeDamage(int damageAmount, ulong attackerClientId)
    {
        if (!IsServer || IsDead) return;
        if (damageAmount <= 0) return;

        int newHp = Mathf.Max(0, hp.Value - damageAmount);
        hp.Value = newHp; // NetworkVariable -> health bar semua client ikut update

        OnTookDamage?.Invoke(this);

        // SFX "terkena hit" di semua Client (modul wajib ada fungsi pemanggil SFX hit)
        BroadcastHitSfxClientRpc(transform.position);

        if (newHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        currentState = EnemyState.Die;

        // Matikan collider supaya tidak kena peluru lagi saat "mati"
        if (enemyCollider != null) enemyCollider.enabled = false;

        // FIX SKOR: musuh mati -> tambahkan skor via event statis OnEnemyDeath
        // (ScoreManager me-subscribe event ini; hanya berjalan di Server)
        OnEnemyDeath?.Invoke(scorePerKill);

        // Broadcast kematian (Spawner memakai ini untuk tracking alive count)
        OnDied?.Invoke(this);

        // SFX kematian di semua Client
        BroadcastDeathSfxClientRpc(transform.position);

        if (despawnDelay <= 0f)
        {
            DespawnSelf();
        }
        else
        {
            Invoke(nameof(DespawnSelf), despawnDelay);
        }
    }

    /// <summary>Destroy/Despawn musuh dari jaringan (otomatis menghancurkan di semua Client).</summary>
    public void DespawnSelf()
    {
        if (!IsServer) return;
        CancelInvoke(nameof(DespawnSelf));

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
        else
        {
            // Fallback kalau objek tidak tersedia di jaringan
            Destroy(gameObject);
        }
    }

    // ==================================================
    // SFX (diputar lokal di setiap Client via SfxManager)
    // ==================================================

    [ClientRpc]
    private void BroadcastAttackSfxClientRpc(Vector3 position)
    {
        if (attackSfx != null && SfxManager.Instance != null)
        {
            SfxManager.Instance.PlayAtPoint(attackSfx, position);
        }
    }

    [ClientRpc]
    private void BroadcastHitSfxClientRpc(Vector3 position)
    {
        if (hitSfx != null && SfxManager.Instance != null)
        {
            SfxManager.Instance.PlayAtPoint(hitSfx, position);
        }
    }

    [ClientRpc]
    private void BroadcastDeathSfxClientRpc(Vector3 position)
    {
        if (deathSfx != null && SfxManager.Instance != null)
        {
            SfxManager.Instance.PlayAtPoint(deathSfx, position);
        }
    }
}
