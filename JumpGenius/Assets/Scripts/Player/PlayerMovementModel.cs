using UnityEngine;

/// <summary>
/// Holds reusable physics-related configurations for player movement,
/// such as jump parameters, ground detection, and surface materials.
/// Used by PlayerMovementController for physical behavior.
/// </summary>
public class PlayerMovementModel
{
    // ===================== JUMP CONFIGURATION =====================

    public float minJumpForce = 5f;     // Not currently used directly — could define min jump charge
    public float maxJumpForce = 15f;    // Max amount of force applied during jump
    public float chargeTime = 1f;       // Could be used to determine how quickly jump charges

    // ===================== PHYSICS MATERIALS =====================

    private PhysicsMaterial2D slipperyMaterial; // Optional surface type (alternative to ice)
    private PhysicsMaterial2D bounceMat;        // Used when airborne (adds bounciness)
    private PhysicsMaterial2D normalMat;        // Used when grounded (no bounce)

    // ===================== CONSTRUCTOR =====================

    /// <summary>
    /// Loads the physics materials from Unity Resources folder.
    /// These materials must be placed under a folder named "Resources/Materials".
    /// </summary>
    public PlayerMovementModel()
    {
        // Load from Resources/Materials
        slipperyMaterial = Resources.Load<PhysicsMaterial2D>("Materials/SlipperyMat");
        bounceMat = Resources.Load<PhysicsMaterial2D>("Materials/PlayerBounce");
        normalMat = Resources.Load<PhysicsMaterial2D>("Materials/PlayerMat");
    }

    // ===================== GROUND CHECK =====================

    /// <summary>
    /// Uses raycasting to detect whether the player is standing on the ground.
    /// Three rays are cast from the bottom center, left, and right of the collider.
    /// </summary>
    public bool IsGrounded(Transform playerTransform,
                           Collider2D playerCollider,
                           out Platform platformHit)
    {
        platformHit = null;

        // ── calculate ray origins ──
        float halfWidth = playerCollider.bounds.extents.x;
        float rayLength = 0.12f;

        Vector2 originCenter = new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.min.y);
        Vector2 originLeft = originCenter + Vector2.left * halfWidth;
        Vector2 originRight = originCenter + Vector2.right * halfWidth;

        // ── cast three downward rays ──
        LayerMask groundMask = LayerMask.GetMask("Ground");

        RaycastHit2D hitCenter = Physics2D.Raycast(originCenter, Vector2.down, rayLength, groundMask);
        RaycastHit2D hitLeft = Physics2D.Raycast(originLeft, Vector2.down, rayLength, groundMask);
        RaycastHit2D hitRight = Physics2D.Raycast(originRight, Vector2.down, rayLength, groundMask);

        // debug rays
        Debug.DrawRay(originCenter, Vector2.down * rayLength, Color.red);
        Debug.DrawRay(originLeft, Vector2.down * rayLength, Color.blue);
        Debug.DrawRay(originRight, Vector2.down * rayLength, Color.green);

        // ── determine grounded state ──
        bool grounded = hitCenter.collider || hitLeft.collider || hitRight.collider;

        // pick the first hit that has a Platform script
        if (grounded)
        {
            if (hitCenter.collider)
                platformHit = hitCenter.collider.GetComponent<Platform>();
            else if (hitLeft.collider)
                platformHit = hitLeft.collider.GetComponent<Platform>();
            else if (hitRight.collider)
                platformHit = hitRight.collider.GetComponent<Platform>();
        }

        return grounded;
    }

    // ===================== MATERIAL GETTERS =====================

    /// <summary>
    /// Returns the material to use when the player is airborne or bouncing.
    /// </summary>
    public PhysicsMaterial2D GetBounceMat() => bounceMat;

    /// <summary>
    /// Returns the default frictionless material used while grounded.
    /// </summary>
    public PhysicsMaterial2D GetNormalMat() => normalMat;

    /// <summary>
    /// Returns a generic slippery material — can be used to vary surface effects.
    /// </summary>
    public PhysicsMaterial2D GetSlipperyMat() => slipperyMaterial;
}
