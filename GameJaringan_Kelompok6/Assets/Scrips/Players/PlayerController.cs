using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Combat Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private ParticleSystem muzzleFlashVFX;

    private NetworkVariable<int> health =
        new NetworkVariable<int>(100);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Player Lokal: Biru
            GetComponent<Renderer>().material.color = Color.blue;

            // Posisi spawn acak
            float randomX = Random.Range(-3f, 3f);
            float randomZ = Random.Range(-3f, 3f);
            transform.position =
                new Vector3(randomX, 0.5f, randomZ);
        }
        else
        {
            // Player Lain: Merah
            GetComponent<Renderer>().material.color = Color.red;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();

        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space))
        {
            ShootServerRpc();
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDirection =
            new Vector3(horizontal, 0f, vertical).normalized;

        if (moveDirection.magnitude >= 0.1f)
        {
            transform.Translate(
                moveDirection * moveSpeed * Time.deltaTime,
                Space.World
            );
        }
    }

    [ServerRpc]
    private void ShootServerRpc()
    {
        if (bulletPrefab == null || firePoint == null)
            return;

        GameObject bulletInstance =
            Instantiate(
                bulletPrefab,
                firePoint.position,
                firePoint.rotation
            );

        NetworkObject bulletNetworkObject =
            bulletInstance.GetComponent<NetworkObject>();

        if (bulletNetworkObject == null)
        {
            Debug.LogError(
                "Bullet prefab tidak memiliki NetworkObject!"
            );

            Destroy(bulletInstance);
            return;
        }

        bulletNetworkObject.Spawn();

        PlayShootEffectsClientRpc();
    }

    [ClientRpc]
    private void PlayShootEffectsClientRpc()
    {
        if (muzzleFlashVFX != null)
        {
            muzzleFlashVFX.Play();
        }
    }

    public void TakeDamage(int amount)
    {
        if (!IsServer) return;

        health.Value =
            Mathf.Max(0, health.Value - amount);
    }
}