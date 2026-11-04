using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Peluru enemy: bergerak LURUS ke BAWAH (menuju player) lalu hancur sendiri.
/// Server-authoritative (mirror dari Bullet.cs project ini).
///
/// Setup prefab EnemyProjectile di Inspector (wajib):
/// - NetworkObject        : pelletur di-spawn oleh server saat enemy menembak.
/// - NetworkTransform     : supaya pergerakan peluru tampil di semua Client.
/// - Rigidbody2D          : Body Type = Kinematic, Gravity Scale = 0.
/// - CircleCollider2D     : Is Trigger = ON (supaya jadi trigger vs collider player).
/// - SpriteRenderer       : visual peluru (warna beda dari peluru player agar jelas).
/// - Daftarkan prefab ini di NetworkPrefabsList (lihat EnemyPrefab.asset).
/// </summary>
public class EnemyProjectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private int damageAmount = 10;
    [Tooltip("Umur peluru sebelum di-despawn otomatis (detik).")]
    [SerializeField] private float destroyTime = 6f;
    [Tooltip("Posisi Y di mana peluru di-despawn saat keluar layar bawah.")]
    [SerializeField] private float despawnBelowY = -9f;

    // Diisi EnemyController saat menembak agar peluru tidak mengenai musuh itu sendiri
    [HideInInspector] public EnemyController shooter;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            Invoke(nameof(DestroyProjectile), destroyTime);
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        // Gerak lurus ke bawah menuju player
        transform.Translate(Vector3.down * speed * Time.deltaTime);

        // Jaring pengaman: keluar layar -> despawn
        if (transform.position.y <= despawnBelowY)
        {
            DestroyProjectile();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        // JANGAN kena musuh lain / musuh penembak sendiri
        if (other.TryGetComponent(out EnemyController enemy)) return;

        if (!other.TryGetComponent(out PlayerController target)) return;
        if (target.health.Value <= 0) return; // target sudah mati

        target.TakeDamage(damageAmount);

        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        if (!IsServer) return;
        CancelInvoke(nameof(DestroyProjectile));

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(); // sudah otomatis menghancurkan objek di semua Client
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
