
using UnityEngine;

public class HealthSystemTest : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;

    private void OnEnable()
    {
        if (healthSystem != null)
        {
            healthSystem.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void Start()
    {
        if (healthSystem == null)
        {
            Debug.LogError("HealthSystem belum dihubungkan!");
            return;
        }

        Debug.Log("=== MULAI TEST HEALTH SYSTEM ===");

        Debug.Log("TEST 1: Damage 25");
        healthSystem.TakeDamage(25f);

        Debug.Log("TEST 2: Heal 10");
        healthSystem.Heal(10f);

        Debug.Log("TEST 3: Damage 200");
        healthSystem.TakeDamage(200f);

        Debug.Log("TEST 4: Heal 500");
        healthSystem.Heal(500f);

        Debug.Log("=== TEST SELESAI ===");
    }

    private void HandleHealthChanged(float current, float max)
    {
        Debug.LogFormat("HP berubah: {0}/{1}", current, max);
    }

    private void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.OnHealthChanged -= HandleHealthChanged;
        }
    }
}
