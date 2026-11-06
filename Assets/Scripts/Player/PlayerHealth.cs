using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float regenerationCooldown = 2f;
    [SerializeField] private float cooldownTimer = 0f;
    [SerializeField] private float regenerationRate = 0.25f;
    
    public HealthStatus HpStatus { get; private set; }

    public float CurrentHealth { get; private set; }

    public enum HealthStatus
    {
        Full,
        Fight,
        Regenerating,
        Dead
    }

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    private void Update()
    {
        if(CurrentHealth < maxHealth)
        {
            cooldownTimer += Time.deltaTime;
            if(cooldownTimer >= regenerationCooldown){
                HpStatus = HealthStatus.Regenerating;
                StartCoroutine(Regenerate());
            }
        } else
        {
            HpStatus = HealthStatus.Full;
        }
    }

    public void TakeDamage(float damage)
    {
        CurrentHealth -= damage;
        cooldownTimer = 0f;
        HpStatus = HealthStatus.Fight;

        CurrentHealth = Mathf.Clamp(
            CurrentHealth,
            0f,
            maxHealth
        );

        Debug.Log(
            "Player HP: " + CurrentHealth
        );

        if (CurrentHealth <= 0f)
        {
            Debug.Log("Player Dead");
            HpStatus = HealthStatus.Dead;
        }
    }

    IEnumerator Regenerate()
    {
        CurrentHealth += regenerationRate;

        CurrentHealth = Mathf.Clamp(
            CurrentHealth,
            0f,
            maxHealth
        );

        yield return new WaitForSeconds(1f);
    }
}
