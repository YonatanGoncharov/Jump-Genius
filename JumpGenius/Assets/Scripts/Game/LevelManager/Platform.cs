using UnityEngine;

public class Platform : MonoBehaviour
{
    private BoxCollider2D boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
    }

    // Check if the player is inside the platform's collider
    public bool IsPlayerOnPlatform(Vector2 playerPosition, float feetPositionY)
    {
        // Get platform bounds
        Bounds bounds = boxCollider.bounds;

        // Only check the top surface of the platform
        bool isAbove = playerPosition.y >= bounds.min.y && feetPositionY <= bounds.max.y + 0.2f; // Allow small tolerance
        bool isInsideX = playerPosition.x >= bounds.min.x && playerPosition.x <= bounds.max.x;

        return isAbove && isInsideX;
    }

}