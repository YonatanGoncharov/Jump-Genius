using UnityEngine;

public class Platform : MonoBehaviour
{
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer; // We'll use this to change color

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Call this when an agent lands here for the first time.
    /// </summary>
    public void MarkAsDiscovered()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.green;
        }
    }
}
