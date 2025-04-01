using UnityEngine;

public class WindParticleController : MonoBehaviour
{
    private ParticleSystem windParticles;
    private PlayerMovementController playerMovement;
    private Vector3 originalPosition;

    void Start()
    {
        windParticles = GetComponent<ParticleSystem>();
        playerMovement = FindFirstObjectByType<PlayerMovementController>();
        if (playerMovement == null)
        {
            Debug.LogError("PlayerMovementController not found!");
        }
        originalPosition = transform.localPosition; // Store the original local position
    }

    void Update()
    {
        if (playerMovement != null)
        {
            UpdateWindParticles();
        }
    }

    void UpdateWindParticles()
    {
        ParticleSystem.ShapeModule shape = windParticles.shape;
        float windDirection = playerMovement.GetCurrentWindDirection();

        if (windDirection < 0) // Wind blowing right
        {
            shape.rotation = new Vector3(0, 0, 0);
            transform.localPosition = originalPosition; // Use the original position
        }
        else if (windDirection > 0) // Wind blowing left
        {
            shape.rotation = new Vector3(0, 180, 0);
            transform.localPosition = new Vector3(-originalPosition.x, originalPosition.y, originalPosition.z); // Flip the X position
        }

        ParticleSystem.MainModule main = windParticles.main;
        main.startSpeed = playerMovement.GetCurrentWindForce();
    }
}
