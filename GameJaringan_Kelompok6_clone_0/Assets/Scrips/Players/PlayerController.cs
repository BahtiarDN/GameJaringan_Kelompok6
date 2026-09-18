using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Memberi warna berbeda pada karakter milik sendiri agar mudah dikenali saat testing
        if (IsOwner)
        {
            GetComponent<Renderer>().material.color = Color.blue; //Player Lokal: Biru

            // Atur posisi spawn acak untuk player yang baru masuk
            float randomX = Random.Range(-5f, 5f);
            float randomY = Random.Range(-5f, 5f);
            transform.position = new Vector2(randomX, randomY);
        }
        else
        {
            GetComponent<Renderer>().material.color = Color.red; //Player Lokal: merah
        }
    }

    private void Update()
    {
        // KUNCI UTAMA MULTIPLAYER
        // Jalankan input & pergerakan HANYA jika objek ini milik pemain lokal

        if (!IsOwner) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        //Membaca input dari keyboard (WASD / arah panah)
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector2 moveDirection = new Vector2(horizontal, vertical).normalized;

        if (moveDirection.magnitude >= 0.1f)
        {
            // Memindahkan posisi karakter
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
        }
    }
}
