using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float groundAcceleration = 35f;
    [SerializeField] private float groundDeceleration = 45f;
    [SerializeField] private float airAcceleration = 12f;
    [SerializeField] private float airDeceleration = 2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Artist Mode")]
    [SerializeField] private bool artistModeEnabled = true;

    private Rigidbody2D rb;

    private bool isGrounded;
    private bool isDead;
    private bool artistMode;

    private float coyoteTimer;
    private float jumpBufferTimer;

    private float originalGravityScale;


    // ==================== PUBLIC PROPERTIES ====================

    // Returns whether Artist Mode is currently active.
    public bool IsArtistMode => artistMode;

    // Returns whether ComicMan is dead.
    public bool IsDead => isDead;

    // Returns whether ComicMan is currently touching the ground.
    public bool IsGrounded => isGrounded;

    // ===================== PUBLIC PROPERTIES END =====================


    // ==================== UNITY START ====================

    // Gets required components and stores the original gravity.
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        originalGravityScale = rb.gravityScale;
    }


    // Reads player input every frame.
    private void Update()
    {
        if (isDead)
            return;

        CheckGrounded();

        UpdateTimers();

        HandleArtistModeToggle();

        if (artistMode)
        {
            return;
        }

        HandleJumpInput();
    }


    // Handles physics-based movement.
    private void FixedUpdate()
    {
        if (isDead)
            return;

        if (artistMode)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        HandleMovement();

        HandleJump();
    }

    // ===================== UNITY END =====================


    // ==================== GROUND CHECK ====================

    // Checks whether ComicMan is standing on a Ground-layer object.
    private void CheckGrounded()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        ) != null;

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
    }

    // ===================== GROUND CHECK END =====================


    // ==================== TIMERS ====================

    // Updates the coyote and jump-buffer timers.
    private void UpdateTimers()
    {
        if (!isGrounded)
        {
            coyoteTimer -= Time.deltaTime;
        }

        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
        }
    }

    // ===================== TIMERS END =====================


    // ==================== MOVEMENT ====================

    // Reads horizontal input and smoothly moves ComicMan.
    private void HandleMovement()
    {
        if (Keyboard.current == null)
            return;

        float input = 0f;

        if (Keyboard.current.aKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed)
        {
            input -= 1f;
        }

        if (Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
        {
            input += 1f;
        }

        float targetSpeed = input * moveSpeed;

        float acceleration;

        if (isGrounded)
        {
            acceleration =
                Mathf.Abs(input) > 0.01f
                    ? groundAcceleration
                    : groundDeceleration;
        }
        else
        {
            acceleration =
                Mathf.Abs(input) > 0.01f
                    ? airAcceleration
                    : airDeceleration;
        }

        float newVelocityX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetSpeed,
            acceleration * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector2(
            newVelocityX,
            rb.linearVelocity.y
        );
    }

    // ===================== MOVEMENT END =====================


    // ==================== JUMP ====================

    // Detects jump input and stores it briefly for jump buffering.
    private void HandleJumpInput()
    {
        if (Keyboard.current == null)
            return;

        bool jumpPressed =
            Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame ||
            Keyboard.current.spaceKey.wasPressedThisFrame;

        if (jumpPressed)
        {
            jumpBufferTimer = jumpBufferTime;
        }
    }


    // Performs the jump when the jump conditions are satisfied.
    private void HandleJump()
    {
        if (jumpBufferTimer <= 0f)
            return;

        if (coyoteTimer <= 0f)
            return;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            jumpForce
        );

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
    }

    // ===================== JUMP END =====================


    // ==================== ARTIST MODE ====================

    // Toggles Artist Mode using Q.
    private void HandleArtistModeToggle()
    {
        if (!artistModeEnabled)
            return;

        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.qKey.wasPressedThisFrame)
            return;

        SetArtistMode(!artistMode);
    }


    // Enables or disables Artist Mode.
    public void SetArtistMode(bool enabled)
    {
        artistMode = enabled;

        if (artistMode)
        {
            // Stop horizontal movement while entering Artist Mode.
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }
    }

    // ===================== ARTIST MODE END =====================


    // ==================== DEATH ====================

    // Stops ComicMan after death.
    public void HandleDeath()
    {
        if (isDead)
            return;

        isDead = true;

        artistMode = false;

        rb.linearVelocity = Vector2.zero;

        // Keep the Rigidbody active but stop normal gravity movement.
        rb.gravityScale = 0f;

        // ==================== DEBUG START ====================

        Debug.Log(
            "[PlayerController] ComicMan has died."
        );

        // ===================== DEBUG END =====================
    }

    // ===================== DEATH END =====================


    // ==================== DEBUG ====================

    // Draws the ground-check area in the Scene view.
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }

    // ===================== DEBUG END =====================
}