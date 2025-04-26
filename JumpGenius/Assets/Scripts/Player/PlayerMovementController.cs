using UnityEngine;

/// <summary>
/// Handles player or agent movement including walking, jumping, wind effects, and stage transitions.
/// Now fully cleaned to work with NEAT agents.
/// </summary>
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

    private bool isOnSlipperySurface = false;
    private bool isOnSnowPlatform = false;

    private bool isControlledByAI = false;
    private float aiInputX = 0f;
    private float aiJumpAnalog = 0f;
    private bool _isGrounded;
    private Platform groundPlatform;      // holds last detected platform

    public bool IsGrounded() => _isGrounded;
    public Platform CurrentPlatform() => groundPlatform;

    // Wind
    public float windForce = 8f;
    public float windCycleDuration = 20f;
    private float windTimer = 0f;
    private float currentWindDirection = 1f;

    private float feetYPosition;
    private float headYPosition;

    void Awake()
    {
        if (movementModel == null)
            movementModel = new PlayerMovementModel();

        if (mainCamera == null)
            mainCamera = Camera.main;

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

    void FixedUpdate()
    {
        _isGrounded = movementModel.IsGrounded(transform,
                                        GetComponent<Collider2D>(),
                                        out groundPlatform);
    }

    private void UpdateGroundStatus()
    {
        // value was set in FixedUpdate
        isGrounded = _isGrounded;

        rb.sharedMaterial = isGrounded
            ? movementModel.GetNormalMat()
            : movementModel.GetBounceMat();

        animator.SetBool("isJumping", !isGrounded);
    }


    private void HandleMovement()
    {
        if (!chargingJump && !isOnSlipperySurface && isGrounded)
        {
            inputX = isControlledByAI ? aiInputX : Input.GetAxisRaw("Horizontal");
        }

        if (isGrounded && !isOnSlipperySurface)
        {
            rb.linearVelocity = new Vector2(inputX * walkSpeed, rb.linearVelocity.y);
            animator.SetFloat("xvelocity", Mathf.Abs(rb.linearVelocity.x));
        }
    }

    private void ChargeJump()
    {
        float jumpInput = isControlledByAI ? aiJumpAnalog : (Input.GetKey("space") ? 1f : 0f);

        if (jumpInput > 0.05f && isGrounded && canJump && !isOnSlipperySurface)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            jumpValue += chargeSpeed * jumpInput * 5f * Time.deltaTime;
            jumpValue = Mathf.Min(jumpValue, maxJumpValue);
            chargingJump = true;
            animator.SetBool("isCharging", true);
        }
        else
        {
            animator.SetBool("isCharging", false);
        }
    }

    private void PerformJump()
    {
        bool jumpReleased = isControlledByAI ? aiJumpAnalog <= 0.05f : Input.GetKeyUp("space");

        if (jumpValue >= maxJumpValue && isGrounded && !isOnSlipperySurface)
        {
            if (inputX > 0) inputX = 1;
            else if (inputX < 0) inputX = -1;

            animator.SetBool("isJumping", true);
            rb.linearVelocity = new Vector2(inputX * walkSpeed, jumpValue);
            ResetJump();
        }

        if (jumpReleased)
        {
            canJump = true;
            chargingJump = false;

            if (isGrounded && !isOnSlipperySurface)
            {
                animator.SetBool("isJumping", true);
                rb.linearVelocity = new Vector2(inputX * walkSpeed, jumpValue);
            }

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
        feetYPosition = transform.position.y - (GetComponent<Collider2D>().bounds.extents.y);
        headYPosition = transform.position.y + (GetComponent<Collider2D>().bounds.extents.y);
    }

    private void CheckLevelTransition()
    {
        if (LevelManager.instance == null)
            return;

        LevelStage currentStage = LevelManager.instance.GetCurrentStage();
        if (currentStage == null) return;

        float stageStartY = LevelManager.instance.GetCurrentLevelHeight();

        if (feetYPosition > stageStartY + 2 * mainCamera.orthographicSize)
        {
            LevelManager.instance.GoToNextStage();
            GameManager.instance.neatManager.OnStageChanged(LevelManager.instance.GetCurrentStageIndex(transform));
        }
        else if (headYPosition < stageStartY)
        {
            LevelManager.instance.GoToPreviousStage();

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
        if (currentStage != null && IsInWindyLevel())
        {
            ApplyWindForce();
        }
    }

    private bool IsInWindyLevel()
    {
        LevelStage currentStage = LevelManager.instance.GetCurrentStage();
        return currentStage != null &&
               currentStage.stageObject != null &&
               ((1 << currentStage.stageObject.layer) & windyLayer) != 0;
    }

    private void ApplyWindForce()
    {
        windTimer += Time.deltaTime;

        if (windTimer >= windCycleDuration)
        {
            windTimer = 0f;
            currentWindDirection *= -1f;
        }

        float currentWindStrength = windForce;

        if (!isGrounded)
        {
            currentWindStrength *= 0.3f;

            if (rb.linearVelocity.y > 0)
            {
                if ((currentWindDirection > 0 && rb.linearVelocity.x < 0) ||
                    (currentWindDirection < 0 && rb.linearVelocity.x > 0))
                {
                    currentWindStrength *= 0.1f;
                }
            }
        }

        if (!isOnSnowPlatform)
        {
            Vector2 windVector = new Vector2(currentWindDirection * currentWindStrength, 0f);
            rb.AddForce(windVector);

            float maxHorizontalSpeed = 10f;
            if (Mathf.Abs(rb.linearVelocity.x) > maxHorizontalSpeed)
            {
                rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * maxHorizontalSpeed, rb.linearVelocity.y);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Slippery"))
            isOnSlipperySurface = true;
        else if (collision.gameObject.CompareTag("SnowPlatform"))
            isOnSnowPlatform = true;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Slippery"))
            isOnSlipperySurface = false;
        else if (collision.gameObject.CompareTag("SnowPlatform"))
            isOnSnowPlatform = false;
    }

    // ===================== AI CONTROL =====================

    public void SetAIInput(float moveX, float jumpAnalog)
    {
        isControlledByAI = true;
        aiInputX = moveX;
        aiJumpAnalog = jumpAnalog;
    }

    public void SetPlayerControlled()
    {
        isControlledByAI = false;
    }

    // Useful for the agent
    public float GetCurrentWindDirection() => currentWindDirection;
    public float GetCurrentWindForce() => windForce;
    public bool IsOnSlipperySurface() => isOnSlipperySurface;
    public bool IsOnSnowPlatform() => isOnSnowPlatform;
    public float GetJumpValue() => jumpValue;

    public Platform GetCurrentGroundedPlatform()
    {
        Vector2 origin = transform.position + new Vector3(0, -0.1f, 0);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.2f, LayerMask.GetMask("Ground"));
        if (hit.collider != null)
            return hit.collider.GetComponent<Platform>();

        return null;
    }
}
