using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    [SerializeField] float moveSpeed = 2f; 
    [SerializeField] float wallCheckDistance = 0.5f; // Distance to check for walls.
    [SerializeField] float groundCheckDistance = 1f; // Distance to check for ground.
    [SerializeField] float groundCheckOffset = 0.1f; // Offset for ground check.
    [SerializeField] facing startDirection = facing.Right; // Facing on start.
    [SerializeField] bool enemyDetection; // Detect if collides with other enemies or not.

    Rigidbody2D rb;

    enum facing
    {
        Left,
        Right
    }

    int direction = 1; // 1 for right, -1 for left.

    void Start()
    {
        if (startDirection == facing.Left)
        {
            TurnAround();
        }
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (IsTouchingWall() || IsAtEdge())
        {
            TurnAround();
        }
        
        rb.linearVelocity = new Vector2(moveSpeed * direction, 0f);
    }

    private bool IsAtEdge()
    {
        Vector2 origin = rb.position + new Vector2 (groundCheckOffset * direction, 0f);

        Debug.DrawRay(origin, Vector2.down * groundCheckDistance, Color.blue);

        return !Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, LayerMask.GetMask("Ground"));
    }

    private bool IsTouchingWall()
    {
        Vector2 origin = rb.position;
        Vector2 rayDirection = Vector2.right * direction;

        Debug.DrawRay(origin, rayDirection * wallCheckDistance, Color.red);

        // Check for walls using a raycast.
        return Physics2D.Raycast(origin, rayDirection, wallCheckDistance, LayerMask.GetMask("Ground"));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") && enemyDetection)
        {
            TurnAround();
        }

        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerHealth>().TakeDamage();
        }
    }

    /// <summary>
    /// Flips the enemy's direction. Used for wall's collision or edges.
    /// </summary>
    private void TurnAround()
    {
        direction = -direction;
        transform.localScale = new Vector2(direction, 1f); // Flip the enemy's sprite.
    }
}
