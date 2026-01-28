/// <summary>
/// Base interface for all states in the state machine.
/// States should be stateless - all per-instance data lives in the context.
/// </summary>
/// <typeparam name="TContext">The context type containing shared data for the state machine.</typeparam>
public interface IState<TContext> where TContext : class
{
    /// <summary>
    /// Called once when entering this state.
    /// Use for initialization, starting animations, enabling components, etc.
    /// </summary>
    /// <param name="context">The shared context data.</param>
    void Enter(TContext context);

    /// <summary>
    /// Called every frame while in this state.
    /// Use for decision-making and triggering state transitions.
    /// </summary>
    /// <param name="context">The shared context data.</param>
    void Update(TContext context);

    /// <summary>
    /// Called at fixed intervals while in this state.
    /// Use for physics-related updates.
    /// </summary>
    /// <param name="context">The shared context data.</param>
    void FixedUpdate(TContext context);

    /// <summary>
    /// Called once when exiting this state.
    /// Use for cleanup, stopping animations, disabling components, etc.
    /// </summary>
    /// <param name="context">The shared context data.</param>
    void Exit(TContext context);
}
