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
<<<<<<< HEAD
        if (!IsOwner)
=======
        if (IsOwner)
>>>>>>> Yar
        {
            GetComponent<Renderer>().material.color = Color.blue; //Player Lokal: Biru

            // Atur posisi spawn acak untuk player yang baru masuk
<<<<<<< HEAD
            float randomX = Random.Range(-3f, 3f);
            float randomZ = Random.Range(-3f, 3f);
            transform.position = new Vector3(randomX, 0.5f, randomZ);
        }

        if (!IsOwner)
=======
            float randomX = Random.Range(-5f, 5f);
            float randomY = Random.Range(-5f, 5f);
            transform.position = new Vector2(randomX, randomY);
        }
        else
>>>>>>> Yar
        {
            GetComponent<Renderer>().material.color = Color.red; //Player Lokal: merah
        }
    }

    private void Update()
    {
        // KUNCI UTAMA MULTIPLAYER
<<<<<<< HEAD
        // Jalankan input & pergerakan HANYA jika objek ini milik pemain lokal (IsOwner)
=======
        // Jalankan input & pergerakan HANYA jika objek ini milik pemain lokal
>>>>>>> Yar

        if (!IsOwner) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        //Membaca input dari keyboard (WASD / arah panah)
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

<<<<<<< HEAD
        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
=======
        Vector2 moveDirection = new Vector2(horizontal, vertical).normalized;
>>>>>>> Yar

        if (moveDirection.magnitude >= 0.1f)
        {
            // Memindahkan posisi karakter
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
        }
    }
}
