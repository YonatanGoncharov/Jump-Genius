using UnityEngine;

/// <summary>
/// Controls a wind <see cref="ParticleSystem"/> so that its orientation, position,
/// and emission speed always match the wind direction and strength reported by the
/// active <see cref="PlayerMovementController"/>.
/// Attach this component to the same GameObject that hosts the wind particle system.
/// </summary>
public class WindParticleController : MonoBehaviour
{
    /// <summary>
    /// Cached reference to the <see cref="ParticleSystem"/> that visually represents wind.
    /// Fetched once in <see cref="Start"/> to avoid repeated <c>GetComponent</c> calls.
    /// </summary>
    private ParticleSystem windParticles;

    /// <summary>
    /// Reference to the scene’s single <see cref="PlayerMovementController"/>, used to
    /// query real?time wind parameters. Located once at startup.
    /// </summary>
    private PlayerMovementController playerMovement;

    /// <summary>
    /// Original <see cref="Transform.localPosition"/> of the particle system when the
    /// scene loads. Mirrored on the X?axis when wind direction changes so the emitter
    /// always starts on the correct side.
    /// </summary>
    private Vector3 originalPosition;

    // -------------------------------------------------------------------------
    // MonoBehaviour life?cycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Locate and cache components. These reflection?based calls are relatively
        // expensive, so doing them once here avoids per?frame overhead.
        windParticles = GetComponent<ParticleSystem>();
        playerMovement = FindFirstObjectByType<PlayerMovementController>();

        // Fail fast if the player controller cannot be found—the particles would
        // have nothing to synchronise with.
        if (playerMovement == null)
        {
            Debug.LogError("PlayerMovementController not found in the scene!");
        }

        // Store the emitter’s starting position so we can restore or mirror it later
        // without accumulating floating?point drift.
        originalPosition = transform.localPosition;
    }

    private void Update()
    {
        // Drive the particle system only if we successfully located a player controller.
        if (playerMovement != null)
        {
            UpdateWindParticles();
        }
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Aligns the particle emitter’s rotation, position and speed with the current
    /// wind conditions supplied by <see cref="PlayerMovementController"/>.
    /// </summary>
    private void UpdateWindParticles()
    {
        // ShapeModule controls where and in which direction particles are spawned.
        ParticleSystem.ShapeModule shape = windParticles.shape;

        // Convention used by PlayerMovementController:
        //   windDirection < 0 ? wind blows to the right  (positive X world direction)
        //   windDirection > 0 ? wind blows to the left   (negative X world direction)
        float windDirection = playerMovement.GetCurrentWindDirection();

        if (windDirection < 0f) // Wind blowing to the right
        {
            // Keep default rotation so particles emit along +X and restore offset.
            shape.rotation = new Vector3(0f, 0f, 0f);
            transform.localPosition = originalPosition;
        }
        else if (windDirection > 0f) // Wind blowing to the left
        {
            // Rotate 180° around Y so particles emit towards ?X, then mirror the
            // local X position so the emitter visually sits on the opposite side.
            shape.rotation = new Vector3(0f, 180f, 0f);
            transform.localPosition = new Vector3(-originalPosition.x, originalPosition.y, originalPosition.z);
        }

        // Match particle start speed to wind strength so faster winds move particles
        // more aggressively.
        ParticleSystem.MainModule main = windParticles.main;
        main.startSpeed = playerMovement.GetCurrentWindForce();
    }
}
