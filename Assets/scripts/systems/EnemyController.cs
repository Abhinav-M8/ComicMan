using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    // ============================================================
    // MOVEMENT
    // ============================================================

    [Header("Movement")]

    // Enemy movement speed.
    [SerializeField] private float moveSpeed = 2f;

    // Platform the enemy is allowed to patrol on.
    [SerializeField] private BoxCollider2D patrolPlatform;

    // Distance kept away from the platform edges.
    [SerializeField] private float edgePadding = 0.2f;


    // ============================================================
    // CONTACT DAMAGE
    // ============================================================

    [Header("Contact Damage")]

    // Damage dealt to ComicMan on contact.
    [SerializeField] private int contactDamage = 10;

    // Minimum time between contact damage.
    [SerializeField] private float damageCooldown = 1f;


    // ============================================================
    // REFERENCES
    // ============================================================

    private Rigidbody2D rb;

    private CharacterHealth health;

    // Direction of movement.
    // 1 = right.
    // -1 = left.
    private int moveDirection = 1;

    // Timer preventing repeated damage.
    private float damageTimer;


    // ============================================================
    // AWAKE
    // Gets required references.
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        health =
            GetComponent<CharacterHealth>();
    }


    // ============================================================
    // UPDATE
    // Handles cooldown timers.
    // ============================================================

    private void Update()
    {
        // Count down contact damage cooldown.
        if (damageTimer > 0f)
        {
            damageTimer -= Time.deltaTime;
        }
    }


    // ============================================================
    // FIXED UPDATE
    // Handles physics-based patrol movement.
    // ============================================================

    private void FixedUpdate()
    {
        // Dead enemies should not move.
        if (health != null && health.IsDead)
            return;

        // No platform means there is nowhere to patrol.
        if (patrolPlatform == null)
            return;

        // Get platform bounds.
        Bounds bounds =
            patrolPlatform.bounds;

        // Calculate legal patrol edges.
        float leftEdge =
            bounds.min.x + edgePadding;

        float rightEdge =
            bounds.max.x - edgePadding;

        // Current position.
        Vector2 position =
            rb.position;

        // Reverse direction at the right edge.
        if (position.x >= rightEdge)
        {
            moveDirection = -1;
        }

        // Reverse direction at the left edge.
        if (position.x <= leftEdge)
        {
            moveDirection = 1;
        }

        // Calculate new velocity.
        Vector2 velocity =
            rb.linearVelocity;

        velocity.x =
            moveDirection * moveSpeed;

        rb.linearVelocity =
            velocity;
    }


    // ============================================================
    // COLLISION ENTER
    // Detects immediate player contact.
    // ============================================================

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(
            collision.collider
        );
    }


    // ============================================================
    // COLLISION STAY
    // Allows contact damage while touching.
    // ============================================================

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamagePlayer(
            collision.collider
        );
    }


    // ============================================================
    // TRIGGER ENTER
    // Supports trigger-based enemy/player collisions.
    // ============================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }


    // ============================================================
    // TRIGGER STAY
    // Allows trigger-based repeated contact damage.
    // ============================================================

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }


    // ============================================================
    // TRY DAMAGE PLAYER
    // Searches the collided object for player health.
    // ============================================================

    private void TryDamagePlayer(Collider2D other)
    {
        // Ignore invalid collider.
        if (other == null)
            return;

        // Only ComicMan can receive this damage.
        if (!other.CompareTag("Player") &&
            other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        // Respect damage cooldown.
        if (damageTimer > 0f)
            return;

        // Find player's health.
        CharacterHealth playerHealth =
            other.GetComponentInParent<CharacterHealth>();

        if (playerHealth == null)
            return;

        // Deal damage.
        playerHealth.TakeDamage(
            contactDamage
        );

        // Restart cooldown.
        damageTimer =
            damageCooldown;


        // ==================== DEBUG START ====================
        Debug.Log(
            $"[EnemyController] Damaged player for " +
            $"{contactDamage}."
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // TAKE DAMAGE
    // Allows external combat systems to damage this enemy.
    // ============================================================

    public void TakeDamage(int damage)
    {
        if (health == null)
            return;

        health.TakeDamage(damage);
    }


    // ============================================================
    // HANDLE DEATH
    // Called by CharacterHealth when HP reaches zero.
    // ============================================================

    public void HandleDeath()
    {
        // Stop movement.
        rb.linearVelocity = Vector2.zero;

        // Disable the enemy.
        gameObject.SetActive(false);


        // ==================== DEBUG START ====================
        Debug.Log(
            $"[EnemyController] {gameObject.name} died."
        );
        // ===================== DEBUG END =====================
    }


    // ============================================================
    // GIZMOS
    // Shows patrol area in Scene view.
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        if (patrolPlatform == null)
            return;

        Bounds bounds =
            patrolPlatform.bounds;

        Gizmos.color = Color.magenta;

        Gizmos.DrawWireCube(
            bounds.center,
            bounds.size
        );
    }
}