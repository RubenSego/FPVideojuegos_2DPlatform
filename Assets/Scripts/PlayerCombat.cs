using System;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Represents the type of weapon the player is using.
/// </summary>
public enum WeaponType
{
    None,
    Bow,
    Sword
}

public class PlayerCombat : MonoBehaviour
{
    [Header("Bow Settings")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float shootCooldown = 0.5f; // Cooldown time in seconds.

    [Header("Sword Settings")]
    [SerializeField] private float swordAttackRange = 1f; // Radius of the sword attack area.
    [SerializeField] private LayerMask enemyLayer; // Layers that the sword can hit.
    [SerializeField] private float swordCooldown = 0.5f; // Cooldown time in seconds for sword attacks.

    [Header("Animations (one controller per weapon)")]
    [SerializeField] private RuntimeAnimatorController unarmedAC;
    [SerializeField] private RuntimeAnimatorController bowAC;
    [SerializeField] private RuntimeAnimatorController swordAC;

    [Header("Starting Inventory (for testing)")]
    [SerializeField] private bool startWithSword = false;
    [SerializeField] private int startWithArrows = 0;

    Transform arrowSpawnPoint;
    Transform swordAttackPoint;

    // Raised when the inventory changes (the HUD listens to this event to update the UI)
    public event Action InventoryChanged; // Event to notify when inventory changes.
    public bool HasSword {get; private set;}
    public int Arrows {get; private set;}
    public WeaponType CurrentWeapon {get; private set; } = WeaponType.None;

    Animator animator;
    float lastShootTime = -999f; // Track the last time the player shot an arrow.

    void Start()
    {
        int selectedCharacterIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        animator = transform.GetChild(0).GetChild(selectedCharacterIndex).GetComponent<Animator>();
        arrowSpawnPoint = transform.GetChild(1).GetComponent<Transform>();
        swordAttackPoint = transform.GetChild(2).GetComponent<Transform>();

        if (startWithSword) AddSword();
        if (startWithArrows > 0) AddArrows(startWithArrows);

        InventoryChanged?.Invoke(); // Notify listeners that the inventory has changed at the start.
    }

    /// <summary>
    /// Adds a specified number of arrows to the player's inventory. If the player currently has no weapon equiped, it will automatically equipo the bow.
    /// </summary>
    public void AddArrows(int startWithArrows)
    {
        Arrows += startWithArrows;
        if (CurrentWeapon == WeaponType.None)
        {
            EquipWeapon(WeaponType.Bow);
        }
        InventoryChanged?.Invoke(); // Notify listeners that the inventory has changed.
    }

    /// <summary>
    /// Adds a sword to the player's forever. Equip if no weapon is active.
    /// </summary>
    public void AddSword()
    {
        HasSword = true;
        if (CurrentWeapon == WeaponType.None)
        {
            EquipWeapon(WeaponType.Sword);
        }
        InventoryChanged?.Invoke(); // Notify listeners that the inventory has changed.
    }

    /// <summary>
    /// Equips the specified weapon type. If the player already has that weapon equipped, it does nothing. 
    /// It also updates the animator controller to match the equipped weapon.
    /// </summary>
    private void EquipWeapon(WeaponType weapon)
    {
        if (weapon == CurrentWeapon) return; // No need to equip the same weapon.
        
        CurrentWeapon = weapon;
        UpdateAnimationController();
        InventoryChanged?.Invoke();
    }

    /// <summary>
    /// Updates the animator controller based on the currently equipped weapon. 
    /// If the player is in an attack state when switching weapons, it resets to the Idle state to avoid animation glitches. 
    /// It also preserves the current state and normalized time when switching controllers, except when switching from an attack state.
    /// </summary>
    private void UpdateAnimationController()
    {
        RuntimeAnimatorController controller;
        switch (CurrentWeapon)
        {
            case WeaponType.Bow:
                controller = bowAC;
                break;
            case WeaponType.Sword:
                controller = swordAC;
                break;
            default:
                controller = unarmedAC;
                break;
        }
        // No need to change if it's already set.
        if (animator.runtimeAnimatorController == controller) return; 
        if (!animator.isInitialized)
        {
            animator.runtimeAnimatorController = controller;
            return;
        }
        
        // If the animator is currently in an attack state, we want to reset it the Idle before changing the controller.
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        animator.runtimeAnimatorController = controller;

        if (currentState.IsName("Attack"))
        {
            animator.Play("Idle", 0, 0f); // Reset to Idle if we were attacking.
        }
        else
        {
            // Preserve the current state and normalized time when switching controllers.
            animator.Play(currentState.fullPathHash, 0, currentState.normalizedTime % 1f); 
        }
    }

    // Draw the sword attack range in the editor for visualization.
    void OawGizmosSelected()
    {
        if (swordAttackPoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(swordAttackPoint.position, swordAttackRange);       
    }

    void OnAttack(InputValue value)
    {
        if(!value.isPressed) return; // Only trigger on button press, not release.
        if (animator.GetBool("isClimbing")) return; // Prevent shooting while climbing.

        if (Time.time < lastShootTime + shootCooldown) return; // Prevent shooting too quickly.
        lastShootTime = Time.time;

        animator.SetTrigger("Attack");
    }

    /// <summary>
    /// This method is called from the animation event in the "Shoot" animation clip. 
    /// It handles shooting an arrow in the direction the player is facing. 
    /// The arrow is instantiated at the arrowSpawnPoint and its direction is set based on the player's facing direction.
    /// </summary>
    public void ShootArrow()
    {
        if (Arrows <= 0) return; // No arrows left to shoot.

        GameObject arrow = Instantiate(arrowPrefab, arrowSpawnPoint.position, Quaternion.identity);
        arrow.GetComponent<ArrowProjectile>().SetDirection(Math.Sign(transform.localScale.x));

        Arrows--;
        if (Arrows == 0)
        {
            EquipWeapon(HasSword ? WeaponType.Sword : WeaponType.None); // Switch to unarmed if no arrows left.
        }

        InventoryChanged?.Invoke(); // Notify listeners that the inventory has changed.
    }

    /// <summary>
    /// This method is called from the animation event in the "SwordAttack" animation clip.
    /// It handles the sowrd attack logic, checking for enemies within the sword's attack range and applying damage to them.
    /// The attack is performed in the direction the player is facing, and it uses a physics overlap check to find
    /// enemies in the specified layer mask.
    /// </summary>
    public void SwordAttack()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(swordAttackPoint.position, swordAttackRange, enemyLayer);
        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.CompareTag("Enemy"))
            {
                Destroy(enemy.gameObject);
            }
        }
    }

    /// <summary>
    /// This method is called when the player presses the "Previous" KEY 1.
    /// </summary>
    void OnPrevious(InputValue value)
    {
        if (!value.isPressed) return;

        if (HasSword)
        {
            EquipWeapon(WeaponType.Sword);
        }
    }

    /// <summary>
    /// This method is called when the player presses the "Next" KEY 2.
    /// </summary>
    void OnNext(InputValue value)
    {
        if (!value.isPressed) return;

        if (Arrows > 0)
        {
            EquipWeapon(WeaponType.Bow);
        }
    }
}
