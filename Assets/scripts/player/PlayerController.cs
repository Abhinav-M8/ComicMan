using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls ComicMan's movement, jumping, death state,
/// and Artist Mode toggle.
/// 
/// NORMAL MODE:
/// A/D or Left/Right = movement
/// W/Up/Space = jump
/// E = attack
/// Q = Artist Mode
/// 
/// ARTIST MODE:
/// Player movement is disabled.
/// PanelManager handles WASD/Arrow/E.
/// Q exits Artist Mode.
/// </summary>
public class PlayerController : MonoBehaviour
{
    // =========================================================
    // MOVEMENT SETTINGS
    // =========================================================

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float groundAcceleration = 35f;
    [SerializeField] private float groundDeceleration = 45f;
    [SerializeField] private float airAcceleration = 12f;
    [SerializeField] private float airDeceleration = 2f;

    // =========================================================
    // JUMP SETTINGS
    // =========================================================

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    // =========================================================
    // GROUND CHECK
    // =========================================================

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private LayerMask groundLayer;

    // =========================================================
    // ARTIST MODE
    // =========================================================

    [Header("Artist Mode")]
    [SerializeField] private bool artistModeEnabled = true;

    // Direct reference to PanelManager.
    // This prevents us from relying only on the singleton.
    [SerializeField] private PanelManager panelManager;

    // =========================================================
    // INTERNAL VARIABLES
    // =========================================================

    private Rigidbody2D rb;

    private bool isGrounded;
    private bool isDead;
    private bool artistMode;

    private float coyoteTimer;
    private float jumpBufferTimer;

    private float originalGravityScale;

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public bool IsArtistMode => artistMode;
    public bool IsDead => isDead;
    public bool IsGrounded => isGrounded;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        originalGravityScale = rb.gravityScale;

        // If PanelManager was not manually assigned,
        // try to find it automatically.
        if (panelManager == null)
        {
            panelManager = FindFirstObjectByType<PanelManager>();
        }

        if (panelManager != null)
        {
            Debug.Log("PLAYER CONTROLLER: PanelManager found.");
        }
        else
        {
            Debug.LogWarning(
                "PLAYER CONTROLLER: PanelManager reference is currently NULL."
            );
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // Dead players do not process input.
        if (isDead)
            return;

        // Q always handles Artist Mode.
        HandleArtistModeToggle();

        // Artist Mode disables normal player controls.
        if (artistMode)
            return;

        HandleJumpInput();
    }

    // =========================================================
    // FIXED UPDATE
    // =========================================================

    private void FixedUpdate()
    {
        if (isDead)
            return;

        // Do not move ComicMan while Artist Mode is active.
        if (artistMode)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        CheckGround();

        HandleMovement();
        HandleJump();
    }

    // =========================================================
    // ARTIST MODE TOGGLE
    // Q = ENTER / EXIT ARTIST MODE
    // =========================================================

    private void HandleArtistModeToggle()
    {
        if (!artistModeEnabled)
            return;

        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.qKey.wasPressedThisFrame)
            return;

        Debug.Log("=================================================");
        Debug.Log("PLAYER CONTROLLER: Q PRESSED");
        Debug.Log("=================================================");

        artistMode = !artistMode;

        Debug.Log("Artist Mode = " + artistMode);

        // Stop ComicMan while Artist Mode is active.
        if (artistMode)
        {
            rb.linearVelocity = Vector2.zero;

            Debug.Log(
                "PLAYER CONTROLLER: Sending Artist Mode TRUE to PanelManager."
            );
        }
        else
        {
            Debug.Log(
                "PLAYER CONTROLLER: Sending Artist Mode FALSE to PanelManager."
            );
        }

        // -----------------------------------------------------
        // Make sure we have a PanelManager.
        // -----------------------------------------------------

        if (panelManager == null)
        {
            panelManager = FindFirstObjectByType<PanelManager>();
        }

        if (panelManager == null)
        {
            Debug.LogError(
                "PLAYER CONTROLLER: Could not find PanelManager!"
            );

            // Don't leave ComicMan stuck in Artist Mode
            // if PanelManager doesn't exist.
            artistMode = false;

            return;
        }

        // Tell PanelManager about the state change.
        panelManager.SetArtistMode(artistMode);
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

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

        if (Mathf.Abs(input) > 0.01f)
        {
            acceleration = isGrounded
                ? groundAcceleration
                : airAcceleration;
        }
        else
        {
            acceleration = isGrounded
                ? groundDeceleration
                : airDeceleration;
        }

        float newX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetSpeed,
            acceleration * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector2(
            newX,
            rb.linearVelocity.y
        );
    }

    // =========================================================
    // JUMP INPUT
    // =========================================================

    private void HandleJumpInput()
    {
        if (Keyboard.current == null)
            return;

        if (
            Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame ||
            Keyboard.current.spaceKey.wasPressedThisFrame
        )
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }
    }

    // =========================================================
    // JUMP
    // =========================================================

    private void HandleJump()
    {
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                jumpForce
            );

            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }
    }

    // =========================================================
    // GROUND CHECK
    // =========================================================

    private void CheckGround()
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
    }

    // =========================================================
    // DEATH
    // =========================================================

    public void HandleDeath()
    {
        if (isDead)
            return;

        isDead = true;

        artistMode = false;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;

        Debug.Log("PLAYER CONTROLLER: ComicMan died.");
    }

    // =========================================================
    // DEBUG GIZMO
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}