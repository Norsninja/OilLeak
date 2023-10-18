using UnityEngine;

public class EnemyBase : MonoBehaviour, IDamageable
{
    public EnemyData enemyData;

    private float health;

    void Start()
    {
        health = enemyData.health;
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        // Implement enemy death logic here
    }
}
