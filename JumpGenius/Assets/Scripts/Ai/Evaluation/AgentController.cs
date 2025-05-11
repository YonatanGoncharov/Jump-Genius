// Assets/Scripts/Ai/Evaluation/AgentController.cs

using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerMovementController))]
public class AgentController : MonoBehaviour
{
    // ───── public read-only state ─────
    public bool IsDead { get; private set; }
    public bool HasExited { get; private set; }
    public int MoveCount { get; private set; }
    public int Falls { get; private set; }
    public float BestPlatformHeight { get; private set; }
    public int BestStageReached { get; private set; }
    public float BestYDistance { get; private set; }
    public int PlatformsVisitedCount => visitedPlatforms.Count;

    // ───── timers ─────
    public float WallPushTime { get; private set; }
    public float GroundIdleTime { get; private set; }
    public float AirTime { get; private set; }

    // ───── internals ─────
    private PlayerMovementController movement;
    private NeuralNetwork network;
    public Genome Genome { get; private set; }

    private Vector2 lastPos;
    private float initialY;
    private int movesLeft;
    private bool groundedLastFrame;
    private bool wasMovingHorizLastFrame;
    private float lastCmdX, lastCmdJ;
    private float stuckTimer, afkTimer;
    private const float AFK_LIMIT = 1f, XVEL_EPS = 0.2f;
    private bool killPending;
    private string pendingReason;
    private bool isReplay;

    private HashSet<Platform> visitedPlatforms = new HashSet<Platform>();

    public void SetReplayMode(bool replay) => isReplay = replay;

    public void Init(Genome g, int allowedMoves)
    {
        Genome = g;
        network = new NeuralNetwork(g);
        movement = GetComponent<PlayerMovementController>();
        movesLeft = allowedMoves;

        // reset all state
        IsDead = false;
        HasExited = false;
        MoveCount = 0;
        Falls = 0;
        BestPlatformHeight = 0f;
        BestStageReached = 0;
        BestYDistance = 0f;
        WallPushTime = 0f;
        GroundIdleTime = 0f;
        AirTime = 0f;
        visitedPlatforms.Clear();

        lastPos = transform.position;
        initialY = transform.position.y;
        groundedLastFrame = movement.IsGrounded();
        wasMovingHorizLastFrame = false;
        lastCmdX = lastCmdJ = 0f;
        stuckTimer = afkTimer = 0f;
        killPending = false;
        pendingReason = null;
        isReplay = false;
    }

    private void Update()
    {
        if (IsDead || network == null || movement == null)
            return;

        // track highest Y
        float dy = transform.position.y - initialY;
        if (dy > BestYDistance) BestYDistance = dy;

        HandleInputs();
        TrackLanding();
        TrackHorizontalStop();
        TrackProgress();
        CheckStuck();
        AntiAfk();
    }

    private void HandleInputs()
    {
        // ── wall sensors ──
        Vector2 pos = transform.position;
        float halfH = GetComponent<Collider2D>().bounds.extents.y;
        float dir = Mathf.Sign(
                          movement.rb.linearVelocity.x != 0
                          ? movement.rb.linearVelocity.x
                          : transform.localScale.x
                       );

        bool frontLow = Physics2D.Raycast(pos, Vector2.right * dir, 0.3f, LayerMask.GetMask("Ground"));
        bool frontMid = Physics2D.Raycast(pos + Vector2.up * (halfH * 0.5f),
                                           Vector2.right * dir, 0.3f, LayerMask.GetMask("Ground"));
        bool frontHigh = Physics2D.Raycast(pos + Vector2.up * halfH,
                                           Vector2.right * dir, 0.3f, LayerMask.GetMask("Ground"));

        // ── distance to next platform ──
        ComputeDeltaToNextPlatform(out float dxPlat, out float dyPlat);
        dxPlat = Mathf.Clamp(dxPlat / 20f, -1f, 1f);
        dyPlat = Mathf.Clamp(dyPlat / 20f, 0f, 1f);

        // ── distance to exit door ──
        Vector2 doorPos = LevelManager.instance.GetExitPosition();
        float dxDoor = Mathf.Clamp((doorPos.x - pos.x) / 20f, -1f, 1f);
        float dyDoor = Mathf.Clamp((doorPos.y - pos.y) / 20f, 0f, 1f);

        // ── build inputs (11 floats) ──
        float[] inputs = {
            movement.rb.linearVelocity.y / 10f,
            movement.GetCurrentWindForce() / 10f,
            movement.GetCurrentWindDirection(),
            movement.IsGrounded() ? 1f : 0f,
            frontLow  ? 1f : 0f,
            frontMid  ? 1f : 0f,
            frontHigh ? 1f : 0f,
            dxPlat, dyPlat,
            dxDoor, dyDoor
        };

        // ── analog outputs: [0]=moveX, [1]=jump strength ──
        float[] outs = network.FeedForward(inputs);
        float cmdX = Mathf.Clamp(outs[0], -1f, 1f);
        float cmdJ = Mathf.Clamp01(outs[1]);

        movement.SetAIInput(cmdX, cmdJ);

        // for move‐tracking
        lastCmdX = cmdX;
        lastCmdJ = cmdJ;
    }

    private void TrackLanding()
    {
        bool groundedNow = movement.IsGrounded();
        if (!isReplay && !groundedLastFrame && groundedNow)
            SpendMove();
        groundedLastFrame = groundedNow;
    }

    private void TrackHorizontalStop()
    {
        bool movingHoriz = Math.Abs(lastCmdX) > 0.1f;
        if (!isReplay && wasMovingHorizLastFrame && !movingHoriz)
            SpendMove();
        wasMovingHorizLastFrame = movingHoriz;
    }

    private void SpendMove()
    {
        if (isReplay) return;
        MoveCount++;
        movesLeft--;
        afkTimer = 0f;
        if (movesLeft <= 0)
            RequestKill("out of moves");
    }

    private void TrackProgress()
    {
        bool grounded = movement.IsGrounded();
        if (grounded) GroundIdleTime += Time.deltaTime;
        else AirTime += Time.deltaTime;

        if (!grounded) return;
        if (killPending) { Kill(pendingReason); return; }

        Platform p = movement.CurrentPlatform();
        if (p == null) return;

        float topY = p.GetComponent<Collider2D>().bounds.max.y;
        float feetY = transform.position.y - GetComponent<Collider2D>().bounds.extents.y;
        if (Math.Abs(feetY - topY) > 0.03f) return;

        if (topY > BestPlatformHeight)
            BestPlatformHeight = topY;

        if (visitedPlatforms.Add(p))
            p.MarkAsDiscovered();

        int newStage = LevelManager.instance.GetStageIndexByY(topY);
        if (newStage > BestStageReached)
        {
            BestStageReached = newStage;
            GameManager.instance.neatManager.OnStageChanged(newStage);
        }
        else if (newStage < LevelManager.instance.CurrentStageIndex)
        {
            LevelManager.instance.GoToPreviousStage();
            AddFall();
        }
    }

    private void CheckStuck()
    {
        if (isReplay) return;
        stuckTimer += Time.deltaTime;
        if (stuckTimer < 1.5f) return;
        if (Vector2.Distance(transform.position, lastPos) < 0.5f)
            Kill("stuck");
        lastPos = transform.position;
        stuckTimer = 0f;
    }

    private void AntiAfk()
    {
        if (isReplay) return;
        bool grounded = movement.IsGrounded();
        float xVel = Math.Abs(movement.rb.linearVelocity.x);
        if (!grounded || xVel > XVEL_EPS) afkTimer = 0f;
        else if ((afkTimer += Time.deltaTime) >= AFK_LIMIT)
            Kill("AFK/slow");
    }

    private void RequestKill(string reason)
    {
        if (isReplay || IsDead) return;
        if (movement.IsGrounded()) Kill(reason);
        else { killPending = true; pendingReason = reason; }
    }

    private void Kill(string reason)
    {
        if (IsDead) return;
        IsDead = true;
        killPending = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.color = Color.red;
        if (movement?.rb != null)
            movement.rb.simulated = false;
    }

    public void AddFall() => Falls++;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Door"))
        {
            HasExited = true;
            Kill("exit");
        }
    }

    /// <summary>
    /// Finds the nearest platform above and returns Δx/Δy to its edge.
    /// </summary>
    private void ComputeDeltaToNextPlatform(out float dx, out float dy)
    {
        dx = dy = 0f;
        float myY = transform.position.y;
        float bestPlatY = float.MaxValue;
        Platform best = null;

        foreach (var stage in LevelManager.instance.levels)
            foreach (var p in stage.platforms)
            {
                float topY = p.GetComponent<Collider2D>().bounds.max.y;
                if (topY > myY && topY < bestPlatY)
                {
                    bestPlatY = topY;
                    best = p;
                }
            }

        if (best != null)
        {
            var b = best.GetComponent<Collider2D>().bounds;
            float x = transform.position.x;
            if (x < b.min.x) dx = b.min.x - x;
            else if (x > b.max.x) dx = x - b.max.x;
            dy = bestPlatY - myY;
        }
    }
}
