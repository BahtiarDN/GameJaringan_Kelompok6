using Unity.Netcode;
using UnityEngine;
public class Bullet : NetworkBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private int damageAmount = 10;
    [SerializeField] private float destroyTime = 3f;
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Hancurkan peluru otomatis di Server setelah beberapa detik jika tidak mengenai apapun
    if (IsServer)
        {
            Invoke(nameof(DestroyBullet), destroyTime);
        }
    }
    private void Update()
    {
        // Gerakkan peluru maju setiap frame
        transform.Translate(Vector2.up * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Hanya Server yang berhak memproses Hit Detection (Authoritative)
        if (!IsServer) return;
        // Cek apakah peluru mengenai Player lain
        if (other.TryGetComponent<PlayerController>(out PlayerController targetPlayer))
        {
            // Panggil method pengurangan HP pada target
            targetPlayer.TakeDamage(damageAmount);
            DestroyBullet();
        }
    }
    private void DestroyBullet()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            // Despawn dari jaringan dan hancurkan objek di Server
            NetworkObject.Despawn();
            Destroy(gameObject);
        }
    }
}