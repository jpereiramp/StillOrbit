using UnityEngine;

/// <summary>
/// Enemy death handling.
/// </summary>
public class EnemyDeadState : BaseState<EnemyContext>
{
    private const float DeathAnimationDuration = 2f;
    private const float DestroyDelay = 5f;

    public override void Enter(EnemyContext ctx)
    {
        // Stop all movement
        ctx.NavAgent.isStopped = true;
        ctx.NavAgent.enabled = false;

        // Clear target
        ctx.CurrentTarget = null;

        // Play death animation
        if (ctx.Animator != null)
            ctx.Animator.SetTrigger("Die");

        // Disable colliders (optional - depends on ragdoll setup)
        var colliders = ctx.Controller.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
                col.enabled = false;
        }

        Debug.Log($"[EnemyDeadState] {ctx.Controller.name} died");

        // Schedule destruction
        ctx.Controller.StartCoroutine(DestroyAfterDelay(ctx));
    }

    private System.Collections.IEnumerator DestroyAfterDelay(EnemyContext ctx)
    {
        yield return new WaitForSeconds(DestroyDelay);

        if (ctx.Controller != null)
        {
            // Unregister from encounter director
            EncounterDirector.Instance?.UnregisterEnemy(ctx.Controller);

            // Destroy
            Object.Destroy(ctx.Controller.gameObject);
        }
    }
}