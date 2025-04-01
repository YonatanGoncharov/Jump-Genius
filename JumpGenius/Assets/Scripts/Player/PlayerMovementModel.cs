using UnityEngine;

public class PlayerMovementModel
{
    public float minJumpForce = 5f;
    public float maxJumpForce = 15f;
    public float chargeTime = 1f;

    private PhysicsMaterial2D iceMaterial;
    private PhysicsMaterial2D slipperyMaterial;
    private PhysicsMaterial2D bounceMat, normalMat;

    public PlayerMovementModel()
    {
        iceMaterial = Resources.Load<PhysicsMaterial2D>("Materials/IceMat");
        slipperyMaterial = Resources.Load<PhysicsMaterial2D>("Materials/SlipperyMat");
        bounceMat = Resources.Load<PhysicsMaterial2D>("Materials/PlayerBounce");
        normalMat = Resources.Load<PhysicsMaterial2D>("Materials/PlayerMat");
    }

    public bool IsGrounded(Transform playerTransform, Collider2D playerCollider)
    {
        float halfWidth = playerCollider.bounds.extents.x;
        float rayLength = 0.12f;

        Vector2 originCenter = (Vector2)playerTransform.position - new Vector2(0, playerCollider.bounds.extents.y);
        Vector2 originLeft = originCenter + Vector2.left * halfWidth;
        Vector2 originRight = originCenter + Vector2.right * halfWidth;

        RaycastHit2D hitCenter = Physics2D.Raycast(originCenter, Vector2.down, rayLength, LayerMask.GetMask("Ground"));
        RaycastHit2D hitLeft = Physics2D.Raycast(originLeft, Vector2.down, rayLength, LayerMask.GetMask("Ground"));
        RaycastHit2D hitRight = Physics2D.Raycast(originRight, Vector2.down, rayLength, LayerMask.GetMask("Ground"));

        Debug.DrawRay(originCenter, Vector2.down * rayLength, Color.red);
        Debug.DrawRay(originLeft, Vector2.down * rayLength, Color.blue);
        Debug.DrawRay(originRight, Vector2.down * rayLength, Color.green);

        bool isGrounded = hitCenter.collider != null || hitLeft.collider != null || hitRight.collider != null;

        return isGrounded;
    }

    public PhysicsMaterial2D GetBounceMat() => bounceMat;

    public PhysicsMaterial2D GetNormalMat() => normalMat;

    public PhysicsMaterial2D GetIceMat() => iceMaterial;

    public PhysicsMaterial2D GetSlipperyMat() => slipperyMaterial;
}
