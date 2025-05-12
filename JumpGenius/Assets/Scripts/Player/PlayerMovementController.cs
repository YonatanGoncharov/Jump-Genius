using UnityEngine;

/// <summary>
/// Handles player or agent movement including walking, jumping, wind effects, and stage transitions.
/// Designed to work seamlessly with both human input and NEAT AI control.
/// </summary>
public class PlayerMovementController : MonoBehaviour
{
    [Tooltip("The Rigidbody2D component used for physics-based movement.")]
    public Rigidbody2D rb;
    [Tooltip("The main camera in the scene, used for determining viewport boundaries for level transitions.")]
    public Camera mainCamera;
    [Tooltip("Data model containing movement-related parameters and physics materials.")]
    public PlayerMovementModel movementModel;

    private LayerMask windyLayer;
    private Animator animator;

    private float inputX;
    private bool isGrounded;

    [Tooltip("Initial jump force applied.")]
    private float jumpValue = 3f;
    [Tooltip("Speed at which the jump force charges when the jump button is held.")]
    public float chargeSpeed = 5f;
    [Tooltip("Maximum value the jump force can reach.")]
    public float maxJumpValue = 15f;
    [Tooltip("Base walking speed of the character.")]
    private float walkSpeed = 4f;
    private bool canJump = true;
    private bool chargingJump = false;
    private bool isFacingRight = false;

    private bool isOnSlipperySurface = false;
    private bool isOnSnowPlatform = false;

    private bool isControlledByAI = false;
    private float aiInputX = 0f;
    private float aiJumpAnalog = 0f;
    private bool _isGrounded;
    private Platform groundPlatform;        // holds the last detected platform the character was grounded on

    /// <summary>Returns true if the character is currently grounded.</summary>
    public bool IsGrounded() => _isGrounded;
    /// <summary>Returns the Platform component of the platform the character is currently grounded on (if any).</summary>
    public Platform CurrentPlatform() => groundPlatform;

    // Wind parameters
    [Tooltip("Base force of the wind effect.")]
    public float windForce = 8f;
    [Tooltip("Duration of one full cycle of wind direction change in seconds.")]
    public float windCycleDuration = 20f;
    private float windTimer = 0f;
    private float currentWindDirection = 1f; // 1 for right, -1 for left

    private float feetYPosition;
    private float headYPosition;

    void Awake()
    {
        // Ensure required components are assigned or create default instances
        if (movementModel == null)
            movementModel = new PlayerMovementModel();

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Get the layer mask for objects that are affected by wind
        windyLayer = LayerMask.GetMask("Windy");
        // Get the Animator component for sprite animations
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Update sprite facing direction based on horizontal input
        FlipSprite();
        // Update the grounded status of the character
        UpdateGroundStatus();
        // Handle horizontal movement based on input or AI control
        HandleMovement();
        // Handle the charging of the jump force when the jump button/analog is held
        ChargeJump();
        // Perform the jump when the jump button/analog is released or maximum charge is reached
        PerformJump();
        // Update the Y positions of the character's feet and head for level transition checks
        UpdateFeetAndHeadPositions();
        // Check if the character has moved to the next or previous level stage
        CheckLevelTransition();
        // Apply wind force to the character if in a windy level
        ApplyWindIfNeeded();
    }

    void FixedUpdate()
    {
        // Determine if the character is grounded using a physics overlap check
        _isGrounded = movementModel.IsGrounded(transform,
                                                GetComponent<Collider2D>(),
                                                out groundPlatform);
    }

    private void UpdateGroundStatus()
    {
        // Update the public isGrounded variable from the FixedUpdate result
        isGrounded = _isGrounded;
        // Change the physics material based on whether the character is grounded or rising after a wall hit
        if (isGrounded)
        {
            rb.sharedMaterial = movementModel.GetNormalMat(); // Use a material with no bounce when grounded
        }
        else if (rb.linearVelocity.y > 0f)
        {
            rb.sharedMaterial = movementModel.GetBounceMat(); // Use a bouncy material when rising (e.g., after hitting a wall)
        }

        // Update the animator to reflect the jumping state
        animator.SetBool("isJumping", !isGrounded);
    }


    private void HandleMovement()
    {
        // Get horizontal input, either from human input or AI control
        if (!chargingJump && !isOnSlipperySurface && isGrounded)
        {
            inputX = isControlledByAI ? aiInputX : Input.GetAxisRaw("Horizontal");
        }

        // Apply horizontal velocity if grounded and not on a slippery surface
        if (isGrounded && !isOnSlipperySurface)
        {
            rb.linearVelocity = new Vector2(inputX * walkSpeed, rb.linearVelocity.y);
            // Update the animator with the absolute horizontal velocity for movement animations
            animator.SetFloat("xvelocity", Mathf.Abs(rb.linearVelocity.x));
        }
    }

    private void ChargeJump()
    {
        // Get jump input, either from space key or AI analog output
        float jumpInput = isControlledByAI ? aiJumpAnalog : (Input.GetKey("space") ? 1f : 0f);

        // If jump input is pressed, character is grounded, can jump, and not on a slippery surface
        if (jumpInput > 0.05f && isGrounded && canJump && !isOnSlipperySurface)
        {
            // Stop horizontal movement while charging
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            // Increase the jump force based on charge speed and input duration
            jumpValue += chargeSpeed * jumpInput * 5f * Time.deltaTime;
            // Clamp the jump force to the maximum allowed value
            jumpValue = Mathf.Min(jumpValue, maxJumpValue);
            chargingJump = true;
            // Update the animator to indicate charging state
            animator.SetBool("isCharging", true);
        }
        else
        {
            // If jump input is not pressed or conditions are not met, stop charging animation
            animator.SetBool("isCharging", false);
        }
    }

    private void PerformJump()
    {
        // Determine if the jump has been released based on input method
        bool jumpReleased = isControlledByAI ? aiJumpAnalog <= 0.05f : Input.GetKeyUp("space");

        // If maximum jump charge is reached, character is grounded, and not slippery
        if (jumpValue >= maxJumpValue && isGrounded && !isOnSlipperySurface)
        {
            // Ensure full horizontal movement if at max charge
            if (inputX > 0) inputX = 1;
            else if (inputX < 0) inputX = -1;

            animator.SetBool("isJumping", true);
            // Apply the jump force
            rb.linearVelocity = new Vector2(inputX * walkSpeed, jumpValue);
            // Reset jump parameters
            ResetJump();
        }

        // If the jump button/analog is released
        if (jumpReleased)
        {
            canJump = true;
            chargingJump = false;

            // Perform a normal jump if grounded and not slippery
            if (isGrounded && !isOnSlipperySurface)
            {
                animator.SetBool("isJumping", true);
                rb.linearVelocity = new Vector2(inputX * walkSpeed, jumpValue);
            }

            // Reset the jump charge value
            jumpValue = 3f;
        }
    }

    private void ResetJump()
    {
        canJump = false;
        jumpValue = 3f;
        chargingJump = false;
    }

    private void FlipSprite()
    {
        // Flip the sprite horizontally based on the horizontal input direction
        if ((inputX > 0 && !isFacingRight) || (inputX < 0 && isFacingRight))
        {
            isFacingRight = !isFacingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
    }

    private void UpdateFeetAndHeadPositions()
    {
        // Calculate the Y position of the character's feet and head based on collider bounds
        feetYPosition = transform.position.y - (GetComponent<Collider2D>().bounds.extents.y);
        headYPosition = transform.position.y + (GetComponent<Collider2D>().bounds.extents.y);
    }

    private void CheckLevelTransition()
    {
        if (LevelManager.instance == null)
            return;

        LevelStage currentStage = LevelManager.instance.GetCurrentStage();
        if (currentStage == null) return;

        // Get the starting Y position of the current level stage
        float stageStartY = LevelManager.instance.GetCurrentLevelHeight();

        // If the character's feet are significantly above the current stage, move to the next stage
        if (feetYPosition > stageStartY + 2 * mainCamera.orthographicSize)
        {
            LevelManager.instance.GoToNextStage();
            // Notify the NEAT manager about the stage change, passing the agent's current Y position for stage determination
            GameManager.instance.neatManager.OnStageChanged(LevelManager.instance.GetCurrentStageIndex(transform));
        }
        // If the character's head is below the start of the current stage, move to the previous stage
        else if (headYPosition < stageStartY)
        {
            LevelManager.instance.GoToPreviousStage();

            // If this controller belongs to an AgentController, increment the fall count
            AgentController agent = GetComponent<AgentController>();
            if (agent != null)
            {
                agent.AddFall();
            }
        }
    }

    private void ApplyWindIfNeeded()
    {
        LevelStage currentStage = LevelManager.instance.GetCurrentStage();
        // Apply wind force only if a current stage exists and it's a windy level
        if (currentStage != null && IsInWindyLevel())
        {
            ApplyWindForce();
        }
    }

    private bool IsInWindyLevel()
    {
        LevelStage currentStage = LevelManager.instance.GetCurrentStage();
        // Check if the current stage object is on a layer that is marked as "Windy"
        return currentStage != null &&
               currentStage.stageObject != null &&
               ((1 << currentStage.stageObject.layer) & windyLayer) != 0;
    }

    private void ApplyWindForce()
    {
        windTimer += Time.deltaTime;

        // Change wind direction after the cycle duration
        if (windTimer >= windCycleDuration)
        {
            windTimer = 0f;
            currentWindDirection *= -1f;
        }

        float currentWindStrength = windForce;

        // Reduce wind strength in the air
        if (!isGrounded)
        {
            currentWindStrength *= 0.3f;

            // Further reduce wind strength if moving against the wind while rising
            if (rb.linearVelocity.y > 0)
            {
                if ((currentWindDirection > 0 && rb.linearVelocity.x < 0) ||
                    (currentWindDirection < 0 && rb.linearVelocity.x > 0))
                {
                    currentWindStrength *= 0.1f;
                }
            }
        }

        // Apply wind force only if not on a snow platform (snow platforms might negate wind)
        if (!isOnSnowPlatform)
        {
            Vector2 windVector = new Vector2(currentWindDirection * currentWindStrength, 0f);
            rb.AddForce(windVector);

            // Limit horizontal speed to prevent excessive wind influence
            float maxHorizontalSpeed = 10f;
            if (Mathf.Abs(rb.linearVelocity.x) > maxHorizontalSpeed)
            {
                rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * maxHorizontalSpeed, rb.linearVelocity.y);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check for collision with slippery and snow platforms
        if (collision.gameObject.CompareTag("Slippery"))
            isOnSlipperySurface = true;
        else if (collision.gameObject.CompareTag("SnowPlatform"))
            isOnSnowPlatform = true;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // Check for exit from slippery and snow platforms
        if (collision.gameObject.CompareTag("Slippery"))
            isOnSlipperySurface = false;
        else if (collision.gameObject.CompareTag("SnowPlatform"))
            isOnSnowPlatform = false;
    }

    // ===================== AI CONTROL =====================

    /// <summary>Sets the input values for AI control.</summary>
    /// <param name="moveX">Horizontal movement input (-1 to 1).</param>
    /// <param name="jumpAnalog">Analog value for jump (0 to 1).</param>
    public void SetAIInput(float moveX, float jumpAnalog)
    {
        isControlledByAI = true;
        aiInputX = moveX;
        aiJumpAnalog = jumpAnalog;
    }

    /// <summary>Sets the control back to human input.</summary>
    public void SetPlayerControlled()
    {
        isControlledByAI = false;
    }

    // Useful properties for the agent's sensory input
    /// <summary>Returns the current direction of the wind (1 for right, -1 for left).</summary>
    public float GetCurrentWindDirection() => currentWindDirection;
    /// <summary>Returns the current strength of the wind force.</summary>
    public float GetCurrentWindForce() => windForce;
    /// <summary>Returns true if the character is currently on a slippery surface.</summary>
    public bool IsOnSlipperySurface() => isOnSlipperySurface;
    /// <summary>Returns true if the character is currently on a snow platform.</summary>
    public bool IsOnSnowPlatform() => isOnSnowPlatform;
    /// <summary>Returns the current charge value of the jump.</summary>
    public float GetJumpValue() => jumpValue;

    /// <summary>Casts a short ray downwards to detect the platform the agent is currently standing on.</summary>
    /// <returns>The Platform component of the ground platform, or null if not grounded on a detectable platform.</returns>
    public Platform GetCurrentGroundedPlatform()
    {
        Vector2 origin = transform.position + new Vector3(0, -0.1f, 0);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.2f, LayerMask.GetMask("Ground"));
        if (hit.collider != null)
            return hit.collider.GetComponent<Platform>();

        return null;
    }
}