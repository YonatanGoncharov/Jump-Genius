// Assets/Scripts/Ai/Evaluation/AgentController.cs

using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerMovementController))]
public class AgentController : MonoBehaviour
{
    // ───── public read-only state ─────
    /// <summary>Indicates whether the agent is currently dead.</summary>
    public bool IsDead { get; private set; }
    /// <summary>Indicates whether the agent has successfully exited the level.</summary>
    public bool HasExited { get; private set; }
    /// <summary>The total number of significant moves the agent has made.</summary>
    public int MoveCount { get; private set; }
    /// <summary>The number of times the agent has fallen back to a previous stage.</summary>
    public int Falls { get; private set; }
    /// <summary>The highest vertical position of a platform the agent has stood on.</summary>
    public float BestPlatformHeight { get; private set; }
    /// <summary>The highest stage index the agent has reached in the level.</summary>
    public int BestStageReached { get; private set; }
    /// <summary>The maximum vertical distance the agent has achieved from its starting position.</summary>
    public float BestYDistance { get; private set; }
    /// <summary>The number of unique platforms the agent has visited.</summary>
    public int PlatformsVisitedCount => visitedPlatforms.Count;
    /// <summary>The total time the agent has spent clinging to a wall.</summary>
    public float SideClingTime { get; private set; }

    // ───── timers ─────
    /// <summary>The amount of time the agent has spent pushing against a wall.</summary>
    public float WallPushTime { get; private set; }
    /// <summary>The amount of time the agent has spent idle while grounded.</summary>
    public float GroundIdleTime { get; private set; }
    /// <summary>The total time the agent has spent in the air.</summary>
    public float AirTime { get; private set; }

    // ───── internals ─────
    private PlayerMovementController movement;
    private NeuralNetwork network;
    /// <summary>The genetic blueprint (Genome) associated with this agent.</summary>
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

    /// <summary>Sets whether the agent is in replay mode, which disables move spending.</summary>
    /// <param name="replay">True if in replay mode, false otherwise.</param>
    public void SetReplayMode(bool replay) => isReplay = replay;

    /// <summary>Initializes the agent with a given Genome and the number of allowed moves.</summary>
    /// <param name="g">The Genome representing the agent's neural network.</param>
    /// <param name="allowedMoves">The maximum number of significant moves the agent can make.</param>
    public void Init(Genome g, int allowedMoves)
    {
        Genome = g;
        network = new NeuralNetwork(g);
        movement = GetComponent<PlayerMovementController>();
        movesLeft = allowedMoves;

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
        SideClingTime = 0f;
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

        // Track the best vertical distance achieved
        float dy = transform.position.y - initialY;
        if (dy > BestYDistance) BestYDistance = dy;

        HandleInputs();
        TrackLanding();
        TrackHorizontalStop();
        TrackProgress();
        CheckStuck();
        AntiAfk();
    }

    /// <summary>Handles the input to the neural network and applies the resulting commands to the movement controller.</summary>
    private void HandleInputs()
    {
        Vector2 pos = transform.position;
        float halfH = GetComponent<Collider2D>().bounds.extents.y;
        // Determine the current horizontal direction of movement or facing
        float dir = Mathf.Sign(
            movement.rb.linearVelocity.x != 0 ? movement.rb.linearVelocity.x : transform.localScale.x
        );

        // Get the position of the exit door
        Vector2 doorPos = LevelManager.instance.GetExitPosition();
        // Normalize the horizontal distance to the door
        float dxDoor = Mathf.Clamp((doorPos.x - pos.x) / 20f, -1f, 1f);
        // Normalize the vertical distance to the door (only positive)
        float dyDoor = Mathf.Clamp((doorPos.y - pos.y) / 20f, 0f, 1f);

        // Prepare the input array for the neural network
        float[] inputs = {
            movement.rb.linearVelocity.y / 10f, // Vertical velocity
            movement.GetCurrentWindForce() / 10f, // Current wind force
            movement.GetCurrentWindDirection(), // Current wind direction (-1, 0, or 1)
            movement.IsGrounded() ? 1f : 0f, // Is the agent grounded?
            dxDoor, // Normalized horizontal distance to the exit
            dyDoor // Normalized vertical distance to the exit
        };

        // Feed the inputs through the neural network to get the outputs
        float[] outs = network.FeedForward(inputs);
        // Interpret the first output as the horizontal movement command (-1 to 1)
        float cmdX = Mathf.Clamp(outs[0], -1f, 1f);
        // Interpret the second output as the jump command (0 to 1, where > 0.5 might indicate a jump)
        float cmdJ = Mathf.Clamp01(outs[1]);

        // Apply the AI-controlled input to the player movement controller
        movement.SetAIInput(cmdX, cmdJ);
        lastCmdX = cmdX;
        lastCmdJ = cmdJ;
    }

    /// <summary>Tracks when the agent lands on the ground and spends a move accordingly.</summary>
    private void TrackLanding()
    {
        bool groundedNow = movement.IsGrounded();
        // If not in replay mode and the agent was not grounded last frame but is now, spend a move
        if (!isReplay && !groundedLastFrame && groundedNow)
            SpendMove();
        groundedLastFrame = groundedNow;
    }

    /// <summary>Tracks when the agent stops horizontal movement and spends a move accordingly.</summary>
    private void TrackHorizontalStop()
    {
        bool movingHoriz = Math.Abs(lastCmdX) > 0.1f;
        // If not in replay mode and the agent was moving horizontally last frame but isn't now, spend a move
        if (!isReplay && wasMovingHorizLastFrame && !movingHoriz)
            SpendMove();
        wasMovingHorizLastFrame = movingHoriz;
    }

    /// <summary>Decrements the number of remaining moves and potentially triggers agent death if no moves are left.</summary>
    private void SpendMove()
    {
        if (isReplay) return; // Don't spend moves in replay mode
        MoveCount++;
        movesLeft--;
        afkTimer = 0f; // Reset the AFK timer on a significant move
        if (movesLeft <= 0)
            RequestKill("out of moves");
    }

    /// <summary>Tracks the agent's progress through the level, including time spent grounded/in air, platform visits, and stage reached.</summary>
    private void TrackProgress()
    {
        bool grounded = movement.IsGrounded();
        // Increment timers for time spent grounded or in the air
        if (grounded) GroundIdleTime += Time.deltaTime;
        else AirTime += Time.deltaTime;

        if (!grounded) return; // Only track platform visits and stage progress when grounded
        if (killPending) { Kill(pendingReason); return; } // Kill the agent if a kill was pending

        Platform p = movement.CurrentPlatform();
        if (p == null) return;

        // Get the top Y position of the current platform and the agent's feet Y position
        float topY = p.GetComponent<Collider2D>().bounds.max.y;
        float feetY = transform.position.y - GetComponent<Collider2D>().bounds.extents.y;

        // Check if the agent is standing on top of the platform
        if (Mathf.Abs(feetY - topY) < 0.05f)
        {
            // Update the best platform height if the current platform is higher
            if (topY > BestPlatformHeight)
                BestPlatformHeight = topY;

            // Mark the platform as visited and update the count if it's a new platform
            if (visitedPlatforms.Add(p))
                p.MarkAsDiscovered();

            // Determine the current stage based on the platform's Y position
            int newStage = LevelManager.instance.GetStageIndexByY(topY);
            // If a new higher stage is reached, update the best stage and notify the NEAT manager
            if (newStage > BestStageReached)
            {
                BestStageReached = newStage;
                GameManager.instance.neatManager.OnStageChanged(newStage);
               
            }
            // If the agent somehow moves back to a previous stage, increment the fall count and move the level back
            else if (newStage < LevelManager.instance.CurrentStageIndex)
            {
                LevelManager.instance.GoToPreviousStage();
                AddFall();
            }
        }
        else
        {
            // If not standing directly on top of a platform, it might be clinging to the side
            SideClingTime += Time.deltaTime;
        }
    }

    /// <summary>Checks if the agent is stuck (not moving significantly for a period) and kills it if so.</summary>
    private void CheckStuck()
    {
        if (isReplay) return; // Don't kill for being stuck in replay mode
        stuckTimer += Time.deltaTime;
        if (stuckTimer < 1.5f) return; // Only check after a short delay
        // If the agent's position hasn't changed much in the stuck time, consider it stuck
        if (Vector2.Distance(transform.position, lastPos) < 0.5f)
            Kill("stuck");
        lastPos = transform.position;
        stuckTimer = 0f; // Reset the stuck timer
    }

    /// <summary>Implements an anti-AFK mechanism to kill agents that remain idle for too long.</summary>
    private void AntiAfk()
    {
        if (isReplay) return; // Don't kill for being AFK in replay mode
        bool grounded = movement.IsGrounded();
        float xVel = Math.Abs(movement.rb.linearVelocity.x);
        // Reset the AFK timer if the agent is not grounded or is moving horizontally
        if (!grounded || xVel > XVEL_EPS) afkTimer = 0f;
        // If the agent is grounded and has been idle (low horizontal velocity) for too long, kill it
        else if ((afkTimer += Time.deltaTime) >= AFK_LIMIT)
            Kill("AFK/slow");
    }

    /// <summary>Requests that the agent be killed, potentially delaying if the agent is in the air.</summary>
    /// <param name="reason">The reason for the kill request.</param>
    private void RequestKill(string reason)
    {
        if (isReplay || IsDead) return; // Don't request kill in replay mode or if already dead
        // If the agent is grounded, kill it immediately
        if (movement.IsGrounded()) Kill(reason);
        // Otherwise, set a flag to kill the agent when it next touches the ground
        else { killPending = true; pendingReason = reason; }
    }

    /// <summary>Immediately kills the agent, marking it as dead and disabling its movement.</summary>
    /// <param name="reason">The reason for killing the agent.</param>
    private void Kill(string reason)
    {
        if (IsDead) return; // Don't kill if already dead
        IsDead = true;
        killPending = false; // Reset the pending kill flag
        // Change the color of the agent's sprites to red to visually indicate death
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.color = Color.red;
        // Disable the agent's physics simulation to stop movement
        if (movement?.rb != null)
            movement.rb.simulated = false;
    }

    /// <summary>Increments the fall counter.</summary>
    public void AddFall() => Falls++;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check for collision with the exit door
        if (other.CompareTag("Door"))
        {
            HasExited = true;
            Kill("exit"); // Kill the agent upon exiting the level
        }
    }


}