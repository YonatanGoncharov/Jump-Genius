using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMovementController))]
public class AgentController : MonoBehaviour
{
    // ───── public read-only state ─────
    public bool IsDead { get; private set; }
    public int MoveCount { get; private set; }
    public int Falls { get; private set; }
    public float BestPlatformHeight { get; private set; }
    public int BestStageReached { get; private set; }

    // timers exposed to FitnessEvaluator
    private float wallPushTime; public float WallPushTime => wallPushTime;
    private float groundIdle; public float GroundIdleTime => groundIdle;
    private float airTime; public float AirTime => airTime;
    private float walkGroundTime; public float WalkGroundTime => walkGroundTime;

    // ───── refs & internals ─────
    private PlayerMovementController movement;
    private NeuralNetwork network;
    public Genome Genome { get; private set; }

    private int movesLeft;
    private bool moveSpent, moveThisFrame, groundedLastFrame = true;
    private Vector2 lastPos;
    private float stuckTimer;

    // anti-AFK / slow walk
    private float afkTimer;
    private const float AFK_LIMIT = 1f;
    private const float XVEL_EPS = 0.20f;

    private const float CHECKPOINT_X_LIMIT = 2f;

    // deferred kill
    private bool killPending;
    private string pendingReason;

    // ★ replay flag
    private bool isReplay = false;
    public void SetReplayMode(bool replay) => isReplay = replay;

    // ───── init ─────
    public void Init(Genome g, int allowedMoves)
    {
        Genome = g;
        network = new NeuralNetwork(g);
        movement = GetComponent<PlayerMovementController>();
        movesLeft = allowedMoves;
        lastPos = transform.position;
    }

    // ───── update loop ─────
    private void Update()
    {
        if (IsDead || network == null || movement == null) return;

        moveThisFrame = false;

        HandleInputs();
        DetectJumpingOffGround();
        TrackProgress();
        CheckStuck();
        AntiAfk();
    }

    // ───── inputs / move spend ─────
    private void HandleInputs()
    {
        float lookDir = Mathf.Sign(movement.rb.linearVelocity.x);
        if (Math.Abs(lookDir) < 0.1f) lookDir = transform.localScale.x >= 0 ? 1 : -1;

        bool frontBlocked = Physics2D.Raycast(transform.position, new Vector2(lookDir, 0), 0.3f, LayerMask.GetMask("Ground"));

        float[] outs = network.FeedForward(new float[]
        {
            movement.rb.linearVelocity.y,
            movement.GetCurrentWindForce(),
            movement.GetCurrentWindDirection(),
            movement.IsGrounded() ? 1 : 0,
            frontBlocked ? 1 : 0
        });

        float cmdX = Mathf.Clamp(outs[0], -1f, 1f);
        float cmdJ = Mathf.Clamp01(outs[1]);

        movement.SetAIInput(cmdX, cmdJ);

        // wall-push accumulation
        if (movement.IsGrounded() && Math.Abs(cmdX) > 0.3f && Math.Abs(movement.rb.linearVelocity.x) < 0.05f)
            wallPushTime += Time.deltaTime;

        if (!isReplay && !moveSpent && !moveThisFrame && (Math.Abs(cmdX) > 0.2f || cmdJ > 0.1f))
        {
            SpendMove();
            moveSpent = true;
            moveThisFrame = true;
        }

        if (Math.Abs(cmdX) < 0.05f && cmdJ < 0.05f)
        {
            moveSpent = false;
            wallPushTime = 0f;
        }
    }

    private void DetectJumpingOffGround()
    {
        bool groundedNow = movement.IsGrounded();
        if (!isReplay && groundedLastFrame && !groundedNow && !moveThisFrame)
        {
            SpendMove();
            moveThisFrame = true;
        }
        groundedLastFrame = groundedNow;
    }

    private void SpendMove()
    {
        if (isReplay) return;           // infinite moves in replay
        MoveCount++;
        movesLeft--;
        afkTimer = 0f;
        if (movesLeft <= 0) RequestKill("out of moves");
    }

    // ───── landing / checkpoint / metrics ─────
    private void TrackProgress()
    {
        bool grounded = movement.IsGrounded();

        // timers
        if (grounded)
        {
            groundIdle += Time.deltaTime;
            if (Math.Abs(movement.rb.linearVelocity.x) > XVEL_EPS)
                walkGroundTime += Time.deltaTime;
        }
        else
        {
            groundIdle = 0f;
            airTime += Time.deltaTime;
        }
        if (!grounded) return;               // only care when actually on a platform
        if (killPending) { Kill(pendingReason); return; }

        Platform p = movement.CurrentPlatform();
        if (p == null) return;

        float platformTopY = p.GetComponent<Collider2D>().bounds.max.y;
        float feetY = transform.position.y - GetComponent<Collider2D>().bounds.extents.y;

        // Ignore side / head bumps: require feet ≈ platform top (±3 cm)
        if (Math.Abs(feetY - platformTopY) > 0.03f) return;

        // update best-height now that we know we're standing on it
            
        if (platformTopY > BestPlatformHeight)
        {
            BestPlatformHeight = platformTopY;
            print(BestPlatformHeight);
        }
            

        p.MarkAsDiscovered();
        int stage = LevelManager.instance.GetStageIndexByY(platformTopY);

        // checkpoint first
        if (stage > GameManager.instance.neatManager.HighestReachedStage)
        {
            Vector2 landing = new Vector2(transform.position.x, platformTopY + 0.1f);
            GameManager.instance.neatManager.RegisterCheckpoint(stage, landing, Genome);
        }

        // then stage-progress bookkeeping
        if (stage > BestStageReached)
        {
            BestStageReached = stage;
            GameManager.instance.neatManager.OnStageChanged(stage);
        }
    }


    // ───── kill helpers ─────
    private void CheckStuck()
    {
        if (isReplay) return;
        stuckTimer += Time.deltaTime;
        if (stuckTimer < 1.5f) return;

        if (Vector2.Distance(transform.position, lastPos) < 0.5f)
            RequestKill("stuck");
        lastPos = transform.position;
        stuckTimer = 0f;
    }

    private void AntiAfk()
    {
        if (isReplay) return;
        bool grounded = movement.IsGrounded();
        float xVel = Math.Abs(movement.rb.linearVelocity.x);

        if (!grounded || xVel > XVEL_EPS) { afkTimer = 0f; return; }

        afkTimer += Time.deltaTime;
        if (afkTimer >= AFK_LIMIT) RequestKill("AFK/slow");
    }

    private void RequestKill(string reason)
    {
        if (isReplay || IsDead) return;
        if (movement != null && movement.IsGrounded())
            Kill(reason);
        else
        {
            killPending = true;
            pendingReason = reason;
        }
    }

    private void Kill(string reason)
    {
        if (IsDead) return;
        IsDead = true;
        killPending = false;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.color = Color.red;

        if (movement?.rb != null) movement.rb.simulated = false;
    }

    public void AddFall() => Falls++;
}
