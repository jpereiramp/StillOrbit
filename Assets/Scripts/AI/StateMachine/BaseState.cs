/// <summary>
/// Abstract base class for states with default empty implementations.
/// Inherit from this to only override the methods you need.
/// </summary>
/// <typeparam name="TContext">The context type containing shared data for the state machine.</typeparam>
/// <example>
/// <code>
/// public class IdleState : BaseState&lt;EnemyContext&gt;
/// {
///     public override void Enter(EnemyContext ctx)
///     {
///         ctx.Animator?.SetBool("IsIdle", true);
///     }
///
///     public override void Update(EnemyContext ctx)
///     {
///         if (ctx.HasTarget)
///         {
///             ctx.Controller.RequestStateChange(EnemyState.Chase);
///         }
///     }
/// }
/// </code>
/// </example>
public abstract class BaseState<TContext> : IState<TContext> where TContext : class
{
    /// <inheritdoc />
    public virtual void Enter(TContext context) { }

    /// <inheritdoc />
    public virtual void Update(TContext context) { }

    /// <inheritdoc />
    public virtual void FixedUpdate(TContext context) { }

    /// <inheritdoc />
    public virtual void Exit(TContext context) { }
}
