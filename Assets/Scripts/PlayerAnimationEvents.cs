using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    PlayerCombat playerCombat;

    void Awake()
    {
        playerCombat = GetComponentInParent<PlayerCombat>();
    }

    // This method is called from the animation event in the "Shoot" animation clip.
    public void ShootArrow()
    {
        playerCombat.ShootArrow();
    }

    public void SwordAttack()
    {
        playerCombat.SwordAttack();
    }
}
