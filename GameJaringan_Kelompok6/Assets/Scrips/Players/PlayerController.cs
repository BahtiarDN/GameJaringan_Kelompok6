using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Player Lokal: Biru
            GetComponent<Renderer>().material.color = Color.blue; 

            // Atur posisi spawn acak untuk player yang baru masuk (3D)
            float randomX = Random.Range(-3f, 3f);
            float randomZ = Random.Range(-3f, 3f);
            transform.position = new Vector3(randomX, 0.5f, randomZ);
        }
        else
        {
            // Player Lain: Merah
            GetComponent<Renderer>().material.color = Color.red; 
        }
    }

    private void Update()
    {
        // Jalankan input & pergerakan HANYA jika objek ini milik pemain lokal
        if (!IsOwner) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        // Membaca input dari keyboard (WASD / arah panah)
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (moveDirection.magnitude >= 0.1f)
        {
            // Memindahkan posisi karakter
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
        }
    }
}       
