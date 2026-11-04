using Unity.Netcode;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private int damageAmount = 10;
    [SerializeField] private float destroyTime = 3f;
    [SerializeField] private int scorePerHit = 10;

    // Diisi server saat peluru dibuat, supaya tidak mengenai penembaknya sendiri
    [HideInInspector] public ulong shooterClientId;

    // Diisi EnemyController saat enemy menembak, supaya peluru enemy
    // tidak melukai enemy lain / dirinya sendiri
    [HideInInspector] public EnemyController enemyShooter;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            Invoke(nameof(DestroyBullet), destroyTime);
        }
    }

    private void Update()
    {
        transform.Translate(Vector2.up * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        // Peluru bisa melukai Enemy (checked dulu agar tidak fallback ke player)
        if (other.TryGetComponent(out EnemyController enemy))
        {
            // Peluru berasal dari enemy tidak boleh melukai enemy (termasuk diri sendiri)
            if (enemyShooter != null) return;
            if (enemy.IsDead) return;

            enemy.TakeDamage(damageAmount, shooterClientId); // skor diberikan otomatis saat enemy mati
            DestroyBullet();
            return;
        }

        if (!other.TryGetComponent(out PlayerController target)) return;

        if (target.OwnerClientId == shooterClientId) return; // abaikan penembak
        if (target.health.Value <= 0) return;                // target sudah mati

        target.TakeDamage(damageAmount);

        // Cari pemain penembak di server, lalu beri poin
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterClientId, out var shooterClient)
            && shooterClient.PlayerObject != null
            && shooterClient.PlayerObject.TryGetComponent(out PlayerController shooter))
        {
            shooter.AddScore(scorePerHit);
        }

        DestroyBullet();
    }

    private void DestroyBullet()
    {
        if (IsServer && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(); // sudah otomatis menghancurkan objeknya
        }
    }
}