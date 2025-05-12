using UnityEngine;

/// <summary>
/// Controls a wind <see cref="ParticleSystem"/> so that its orientation, position,
/// and emission speed always match the wind direction and strength reported by the
/// active <see cref="PlayerMovementController"/>.
/// Attach this component to the same GameObject that hosts the wind particle system.
/// </summary>
public class WindParticleController : MonoBehaviour
{
    private ParticleSystem windParticles;
    private PlayerMovementController playerMovement;
    private Vector3 originalPosition;

    private void Start()
    {
        windParticles = GetComponent<ParticleSystem>();
        originalPosition = transform.localPosition;
    }

    private void Update()
    {
        // Try assigning the controller once when available
        if (playerMovement == null && NEATManager.CurrentAgentMovement != null)
        {
            playerMovement = NEATManager.CurrentAgentMovement;
        }

        if (playerMovement != null)
        {
            UpdateWindParticles();
        }
    }

    private void UpdateWindParticles()
    {
        ParticleSystem.ShapeModule shape = windParticles.shape;
        float windDirection = playerMovement.GetCurrentWindDirection();

        if (windDirection < 0f) // Wind blowing to the right
        {
            shape.rotation = new Vector3(0f, 0f, 0f);
            transform.localPosition = originalPosition;
        }
        else if (windDirection > 0f) // Wind blowing to the left
        {
            shape.rotation = new Vector3(0f, 180f, 0f);
            transform.localPosition = new Vector3(-originalPosition.x, originalPosition.y, originalPosition.z);
        }

        ParticleSystem.MainModule main = windParticles.main;
        main.startSpeed = playerMovement.GetCurrentWindForce();
    }
}
