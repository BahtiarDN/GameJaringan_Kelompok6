using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
	[SerializeField] private float maxHealth = 100f;
	[SerializeField] private float currentHealth = 100f;

	public event Action<float, float> OnHealthChanged;

	private void Awake()
	{
		SanitizeMaxHealth();
		SetCurrentHealth(maxHealth);
	}

	private void OnValidate()
	{
		SanitizeMaxHealth();

		if (!IsFinite(currentHealth))
		{
			currentHealth = 0f;
		}

		currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
	}

	public void TakeDamage(float amount)
	{
		if (!IsValidAmount(amount))
		{
			return;
		}

		SetCurrentHealth(currentHealth - amount);
	}

	public void Heal(float amount)
	{
		if (!IsValidAmount(amount))
		{
			return;
		}

		SetCurrentHealth(currentHealth + amount);
	}


private void SetCurrentHealth(float value)
{
    if (!IsFinite(value))
        return;

    float clampedHealth = Mathf.Clamp(value, 0f, maxHealth);

    if (clampedHealth == currentHealth)
        return;

    currentHealth = clampedHealth;
    OnHealthChanged?.Invoke(currentHealth, maxHealth);
}


	private void SanitizeMaxHealth()
	{
		if (!IsFinite(maxHealth) || maxHealth < 0f)
		{
			maxHealth = 0f;
		}
	}

	private static bool IsValidAmount(float amount)
	{
		return IsFinite(amount) && amount > 0f;
	}

	private static bool IsFinite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}
}
    