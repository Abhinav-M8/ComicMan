using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private Vector2 attackOffset = new Vector2(0.7f, 0f);

    [Header("Enemy Detection")]
    [SerializeField] private LayerMask enemyLayer;

    // Current facing direction.
    // true  = right
    // false = left
    private bool facingRight = true;

    private PlayerController playerController;


    // ==================== UNITY START ====================

    // Gets references when the player is created.
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }


    // Checks for the attack button every frame.
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (playerController != null && playerController.IsDead)
            return;

        // Don't attack while Artist Mode is active.
        if (playerController != null && playerController.IsArtistMode)
            return;

        // Update facing based on movement.
        UpdateFacingDirection();

        // E = attack.
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Attack();
        }
    }

    // ===================== UNITY END =====================


    // ==================== FACING ====================

    // Changes the direction of the character based on horizontal movement.
    private void UpdateFacingDirection()
    {
        if (playerController == null)
            return;

        Rigidbody2D rb = playerController.GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        // Moving right.
        if (rb.linearVelocity.x > 0.05f)
        {
            facingRight = true;
        }

        // Moving left.
        else if (rb.linearVelocity.x < -0.05f)
        {
            facingRight = false;
        }
    }

    // ===================== FACING END =====================


    // ==================== ATTACK ====================

    // Performs the player's attack in the direction they are facing.
    private void Attack()
    {
        Vector2 attackPosition = GetAttackPosition();

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPosition,
            attackRange,
            enemyLayer
        );

        foreach (Collider2D hit in hits)
        {
            CharacterHealth enemyHealth =
                hit.GetComponentInParent<CharacterHealth>();

            if (enemyHealth == null)
                continue;

            enemyHealth.TakeDamage(attackDamage);

            // ==================== DEBUG START ====================

            Debug.Log(
                "[PlayerCombat] Hit " +
                hit.gameObject.name +
                " for " +
                attackDamage +
                " damage."
            );

            // ===================== DEBUG END =====================
        }
    }


    // Calculates the attack position based on facing direction.
    private Vector2 GetAttackPosition()
    {
        Vector2 offset = attackOffset;

        // Flip the X offset when facing left.
        if (!facingRight)
        {
            offset.x *= -1f;
        }

        return (Vector2)transform.position + offset;
    }

    // ===================== ATTACK END =====================


    // ==================== GIZMOS ====================

    // Draws the attack range in the Scene view for debugging.
    private void OnDrawGizmosSelected()
    {
        Vector2 attackPosition = GetAttackPosition();

        Gizmos.DrawWireSphere(
            attackPosition,
            attackRange
        );
    }

    // ===================== GIZMOS END =====================
}