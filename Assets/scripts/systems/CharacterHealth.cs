using UnityEngine;

public class CharacterHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("UI")]
    [SerializeField] private HealthBar healthBar;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0;


    // ==================== UNITY START ====================

    // Initializes health when the character is created.
    private void Awake()
    {
        currentHealth = maxHealth;

        UpdateHealthBar();
    }

    // ===================== UNITY END =====================


    // ==================== DAMAGE ====================

    // Removes health and updates the health bar.
    public void TakeDamage(int damage)
    {
        if (IsDead)
            return;

        if (damage <= 0)
            return;

        currentHealth -= damage;

        // Prevent health from going below zero.
        currentHealth = Mathf.Max(currentHealth, 0);

        UpdateHealthBar();

        // ==================== DEBUG START ====================

        Debug.Log(
            "[CharacterHealth] " +
            gameObject.name +
            " HP: " +
            currentHealth +
            "/" +
            maxHealth
        );

        // ===================== DEBUG END =====================

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // ===================== DAMAGE END =====================


    // ==================== HEAL ====================

    // Restores health and updates the health bar.
    public void Heal(int amount)
    {
        if (IsDead)
            return;

        if (amount <= 0)
            return;

        currentHealth += amount;

        currentHealth = Mathf.Min(currentHealth, maxHealth);

        UpdateHealthBar();
    }

    // ===================== HEAL END =====================


    // ==================== HEALTH BAR ====================

    // Sends the current health percentage to the HealthBar.
    private void UpdateHealthBar()
    {
        if (healthBar == null)
        {
            Debug.LogWarning(
                "[CharacterHealth] No HealthBar assigned to " +
                gameObject.name
            );

            return;
        }

        healthBar.SetHealth(currentHealth, maxHealth);
    }

    // ===================== HEALTH BAR END =====================


    // ==================== DEATH ====================

    // Handles character death.
    // ==================== DEATH ====================

    // Handles character death.
    private void Die()
    {
        // Hide the health bar first.
        if (healthBar != null)
        {
            healthBar.Hide();
        }

        // Check whether this character is an enemy.
        EnemyController enemy = GetComponent<EnemyController>();

        if (enemy != null)
        {
            enemy.HandleDeath();
            return;
        }

        // Check whether this character is ComicMan.
        PlayerController player = GetComponent<PlayerController>();

        if (player != null)
        {
            player.HandleDeath();
            return;
        }

        // ==================== DEBUG START ====================

        Debug.LogWarning(
            "[CharacterHealth] " +
            gameObject.name +
            " died but has no EnemyController or PlayerController."
        );

        // ===================== DEBUG END =====================
    }

    // ===================== DEATH END =====================

    // ===================== DEATH END =====================
}