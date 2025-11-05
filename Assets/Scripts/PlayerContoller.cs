using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 7f;

    [Header("Jump")]
    public float jumpForce = 12f;
    public float coyoteTime = 0.1f;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundMask;
    public Transform groundCheck;

    Rigidbody2D rb;
    float coyoteCounter;
    bool jumpQueued;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // queue jump on key down (runs in Update so button presses aren't missed)
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpQueued = true;
#else
        if (Input.GetButtonDown("Jump"))
            jumpQueued = true;
#endif

        // coyote timer
        bool grounded = groundCheck && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
        if (grounded) coyoteCounter = coyoteTime;
        else coyoteCounter -= Time.deltaTime;

        // horizontal input
        float x = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
        }
#else
        x = Input.GetAxisRaw("Horizontal");
#endif

        // apply horizontal velocity (keep current vertical velocity so gravity works)
        Vector2 v = rb.linearVelocity;
        v.x = x * moveSpeed;
        rb.linearVelocity = v;

        // jump
        if (jumpQueued && coyoteCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            coyoteCounter = 0f;
        }
        jumpQueued = false;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
