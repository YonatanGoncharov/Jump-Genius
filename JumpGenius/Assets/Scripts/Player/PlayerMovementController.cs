using UnityEngine;
using System.Collections;

public class PlayerMovementController : MonoBehaviour
{
    public Rigidbody2D rb;
    public Camera mainCamera;
    public PlayerMovementModel movementModel;
    private LayerMask windyLayer;
    private Animator animator;

    private float inputX;
    private bool isGrounded;
    private float jumpValue = 3f;
    public float chargeSpeed = 5f;
    public float maxJumpValue = 15f;
    private float walkSpeed = 4f;
    private bool canJump = true;
    private bool chargingJump = false;
    private bool isFacingRight = false;

    private float feetYPosition;
    private float headYPosition;

    private bool isOnSlipperySurface = false;
    private bool isOnSnowPlatform = false;


    // Wind variables
    public float windForce = 8f;
    public float windCycleDuration = 20f; // Time for one full wind cycle
    private float windTimer = 0f;
    private float currentWindDirection = 1f; // 1 for right, -1 for left

    void Awake()
    {
        if (movementModel == null)
        {
            movementModel = new PlayerMovementModel();
        }

        windyLayer = LayerMask.GetMask("Windy");
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        FlipSprite();
        UpdateGroundStatus();
        HandleMovement();
        ChargeJump();
        PerformJump();
        UpdateFeetAndHeadPositions();
        CheckLevelTransition();
        ApplyWindIfNeeded();
    }

    void UpdateGroundStatus()
    {
        Collider2D playerCollider = GetComponent<Collider2D>();
        isGrounded = movementModel.IsGrounded(transform, playerCollider);
        rb.sharedMaterial = isGrounded ? movementModel.GetNormalMat() : movementModel.GetBounceMat();
        if (isGrounded)
        {
            animator.SetBool("isJumping", false);
        }
        else
        {
            animator.SetBool("isJumping", true);
        }
    }

    void HandleMovement()
    {
        if (!chargingJump && !isOnSlipperySurface && isGrounded) // Only allow movement if grounded
        {
            inputX = Input.GetAxisRaw("Horizontal");

        }

        if (isGrounded && !isOnSlipperySurface)
        {
            rb.linearVelocity = new Vector2(inputX * walkSpeed, rb.linearVelocity.y);
            animator.SetFloat("xvelocity", Mathf.Abs(rb.linearVelocity.x));
        }
   
    }


    void ChargeJump()
    {
        if (Input.GetKey("space") && isGrounded && canJump && !isOnSlipperySurface)
        {
            rb.linearVelocity = new Vector2(0.0f, rb.linearVelocity.y);
            jumpValue += chargeSpeed * 5f * Time.deltaTime; // Charge faster
            jumpValue = Mathf.Min(jumpValue, maxJumpValue); // Prevent overcharging
            chargingJump = true;
            animator.SetBool("isCharging", true);
        }
        else
        {
            animator.SetBool("isCharging", false);
        }
    }


    void PerformJump()
    {
        if (jumpValue >= maxJumpValue && isGrounded && !isOnSlipperySurface)
        {
            if (inputX > 0)
                inputX = 1;
            else if (inputX < 0)
                inputX = -1;
            animator.SetBool("isJumping", true);
            float jumpDirectionX = inputX * walkSpeed; // Preserve horizontal movement
            rb.linearVelocity = new Vector2(jumpDirectionX, jumpValue);
            ResetJump();
        }

        if (Input.GetKeyUp("space"))
        {
            canJump = true;
            chargingJump = false;

            if (isGrounded && !isOnSlipperySurface)
            {
                animator.SetBool("isJumping", true);
                float jumpDirectionX = inputX * walkSpeed; // Apply horizontal movement
                rb.linearVelocity = new Vector2(jumpDirectionX, jumpValue);
            }

            jumpValue = 3f; // Reset jump charge after jump
        }
    }


    void ResetJump()
    {
        canJump = false;
        jumpValue = 3f;
        chargingJump = false;
    }
    void FlipSprite()
    {
        if (inputX > 0 && !isFacingRight || inputX < 0 && isFacingRight)
        {
            isFacingRight = !isFacingRight;
            Vector3 theScale = transform.localScale;
            theScale.x *= -1;
            transform.localScale = theScale;
        }
    }

    void UpdateFeetAndHeadPositions()
    {
        feetYPosition = transform.position.y - (GetComponent<Collider2D>().bounds.extents.y);
        headYPosition = transform.position.y + (GetComponent<Collider2D>().bounds.extents.y);
    }

    void CheckLevelTransition()
    {
        LevelStage currentStage = LevelManager.instance.GetCurrentStage();
        if (currentStage == null)
            return;

        float stageStartYPosition = LevelManager.instance.GetCurrentLevelHeight();

        if (feetYPosition > stageStartYPosition + 2 * mainCamera.orthographicSize)
        {
            LevelManager.instance.GoToNextStage();
        }
        if (headYPosition < stageStartYPosition)
        {
            LevelManager.instance.GoToPreviousStage();
        }
    }

    void ApplyWindIfNeeded()
    {
        LevelStage currentStage = LevelManager.instance.GetCurrentStage();
        if (currentStage != null && IsInWindyLevel() )
        {
            ApplyWindForce();
        }
    }

    bool IsInWindyLevel()
    {
        LevelStage currentStage = LevelManager.instance.GetCurrentStage();
        return currentStage != null && currentStage.stageObject != null && ((1 << currentStage.stageObject.layer) & windyLayer) != 0;
    }

    void ApplyWindForce()
    {
        windTimer += Time.deltaTime;
        if (windTimer >= windCycleDuration)
        {
            windTimer = 0f;
            currentWindDirection *= -1f; // Reverse wind direction
        }

        float currentWindStrength = windForce;

        // Dampen wind force in air
        if (!isGrounded)
        {
            currentWindStrength *= 0.3f;
        }

        // Add extra force when jumping against the wind
        if (!isGrounded && rb.linearVelocity.y > 0) // Only add force if jumping
        {
            if (currentWindDirection > 0 && rb.linearVelocity.x < 0) // Wind right, jump left
            {
                currentWindStrength *= 0.1f; // Increase wind strength (adjust as needed)
            }
            else if (currentWindDirection < 0 && rb.linearVelocity.x > 0) // Wind left, jump right
            {
                currentWindStrength *= 0.1f; // Increase wind strength (adjust as needed)
            }
        }
        if (!isOnSnowPlatform)
        {
            Vector2 windVector = new Vector2(currentWindDirection * currentWindStrength, 0f);
            rb.AddForce(windVector);
            // Limit maximum horizontal velocity
            float maxHorizontalSpeed = 10f;
            if (Mathf.Abs(rb.linearVelocity.x) > maxHorizontalSpeed)
            {
                rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * maxHorizontalSpeed, rb.linearVelocity.y);
            }
        }

    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Door"))
        {
            // EndRun(); // Call the end function
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Slippery"))
        {
            isOnSlipperySurface = true;
        }
        else if (collision.gameObject.CompareTag("SnowPlatform"))
        {
            isOnSnowPlatform = true;
        }
        else if (collision.contacts.Length > 0)
        {
            ContactPoint2D contact = collision.contacts[0];
            Vector2 normal = contact.normal;

            // If hitting a wall (normal is mostly horizontal)
            if (Mathf.Abs(normal.x) > 0.8f)
            {
                float bounceForce = 5f; // Adjust bounce intensity as needed
                rb.linearVelocity = new Vector2(-normal.x * bounceForce, rb.linearVelocity.y);
            }
        }
    }


    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Slippery"))
        {
            isOnSlipperySurface = false;
        }
        else if (collision.gameObject.CompareTag("SnowPlatform"))
        {
            isOnSnowPlatform = false;
        }
    }
    public float GetCurrentWindDirection()
    {
        return currentWindDirection;
    }

    public float GetCurrentWindForce()
    {
        return windForce;
    }
}