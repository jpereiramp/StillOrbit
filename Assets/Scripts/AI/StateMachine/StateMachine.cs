using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic state machine that can be used by any agent type.
/// Provides validated transitions, state lifecycle management, and event notifications.
/// </summary>
/// <typeparam name="TState">The state enum type (e.g., EnemyState, CompanionState).</typeparam>
/// <typeparam name="TContext">The context class containing shared data for states.</typeparam>
/// <example>
/// <code>
/// // Create context and state machine
/// var context = new EnemyContext(controller, archetype, transform, navAgent, animator, health);
/// var stateMachine = new StateMachine&lt;EnemyState, EnemyContext&gt;(context);
///
/// // Register states
/// stateMachine.RegisterState(EnemyState.Idle, new EnemyIdleState());
/// stateMachine.RegisterState(EnemyState.Chase, new EnemyChaseState());
///
/// // Register valid transitions
/// stateMachine.RegisterTransitions(EnemyState.Idle, EnemyState.Chase, EnemyState.Patrol);
/// stateMachine.RegisterTransitions(EnemyState.Chase, EnemyState.Idle, EnemyState.Attack);
///
/// // Initialize with starting state
/// stateMachine.Initialize(EnemyState.Idle);
///
/// // In Update loop
/// stateMachine.Update();
///
/// // Request state changes
/// stateMachine.RequestStateChange(EnemyState.Chase);
/// </code>
/// </example>
public class StateMachine<TState, TContext>
    where TState : Enum
    where TContext : class
{
    private readonly TContext _context;
    private readonly Dictionary<TState, IState<TContext>> _states = new();
    private readonly HashSet<(TState from, TState to)> _validTransitions = new();

    private IState<TContext> _currentStateInstance;
    private TState _currentState;
    private TState _previousState;
    private bool _isInitialized;
    private bool _isPaused;

    /// <summary>
    /// The current active state.
    /// </summary>
    public TState CurrentState => _currentState;

    /// <summary>
    /// The previous state before the last transition.
    /// </summary>
    public TState PreviousState => _previousState;

    /// <summary>
    /// Whether the state machine has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Whether the state machine is paused (Update/FixedUpdate won't run).
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Fired when a state transition occurs.
    /// Parameters: (previousState, newState)
    /// </summary>
    public event Action<TState, TState> OnStateChanged;

    /// <summary>
    /// Creates a new state machine with the given context.
    /// </summary>
    /// <param name="context">The shared context that states will use.</param>
    /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
    public StateMachine(TContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Register a state implementation for a given state enum value.
    /// </summary>
    /// <param name="state">The state enum value.</param>
    /// <param name="stateInstance">The state implementation instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if stateInstance is null.</exception>
    public void RegisterState(TState state, IState<TContext> stateInstance)
    {
        if (stateInstance == null)
            throw new ArgumentNullException(nameof(stateInstance));

        _states[state] = stateInstance;
    }

    /// <summary>
    /// Register a valid transition between two states.
    /// </summary>
    /// <param name="from">The source state.</param>
    /// <param name="to">The destination state.</param>
    public void RegisterTransition(TState from, TState to)
    {
        _validTransitions.Add((from, to));
    }

    /// <summary>
    /// Register multiple valid transitions from one state to several others.
    /// </summary>
    /// <param name="from">The source state.</param>
    /// <param name="toStates">The destination states.</param>
    public void RegisterTransitions(TState from, params TState[] toStates)
    {
        foreach (var to in toStates)
        {
            _validTransitions.Add((from, to));
        }
    }

    /// <summary>
    /// Initialize the state machine with a starting state.
    /// Must be called before Update/FixedUpdate.
    /// </summary>
    /// <param name="initialState">The state to start in.</param>
    /// <returns>True if initialization succeeded.</returns>
    public bool Initialize(TState initialState)
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[StateMachine] Already initialized");
            return false;
        }

        if (!_states.TryGetValue(initialState, out var stateInstance))
        {
            Debug.LogError($"[StateMachine] No state registered for {initialState}");
            return false;
        }

        _currentState = initialState;
        _previousState = initialState;
        _currentStateInstance = stateInstance;
        _isInitialized = true;

        _currentStateInstance.Enter(_context);

        return true;
    }

    /// <summary>
    /// Request a state change with transition validation.
    /// </summary>
    /// <param name="newState">The state to transition to.</param>
    /// <returns>True if the transition was successful.</returns>
    public bool RequestStateChange(TState newState)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[StateMachine] Cannot change state - not initialized");
            return false;
        }

        // Same state - no-op, return success
        if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
            return true;

        // Validate transition
        if (!IsValidTransition(_currentState, newState))
        {
            Debug.LogWarning($"[StateMachine] Invalid transition: {_currentState} -> {newState}");
            return false;
        }

        return ExecuteTransition(newState);
    }

    /// <summary>
    /// Force a state change without transition validation.
    /// Use sparingly for edge cases like death or interrupts.
    /// </summary>
    /// <param name="newState">The state to transition to.</param>
    /// <returns>True if the transition was successful.</returns>
    public bool ForceState(TState newState)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[StateMachine] Cannot force state - not initialized");
            return false;
        }

        // Same state - no-op
        if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
            return true;

        return ExecuteTransition(newState);
    }

    /// <summary>
    /// Check if a transition from one state to another is valid.
    /// </summary>
    /// <param name="from">The source state.</param>
    /// <param name="to">The destination state.</param>
    /// <returns>True if the transition is registered as valid.</returns>
    public bool IsValidTransition(TState from, TState to)
    {
        return _validTransitions.Contains((from, to));
    }

    /// <summary>
    /// Called every frame by the owner MonoBehaviour.
    /// </summary>
    public void Update()
    {
        if (!_isInitialized || _isPaused)
            return;

        _currentStateInstance?.Update(_context);
    }

    /// <summary>
    /// Called at fixed intervals by the owner MonoBehaviour.
    /// </summary>
    public void FixedUpdate()
    {
        if (!_isInitialized || _isPaused)
            return;

        _currentStateInstance?.FixedUpdate(_context);
    }

    /// <summary>
    /// Pause the state machine. Update and FixedUpdate will not run while paused.
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>
    /// Resume the state machine from a paused state.
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
    }

    /// <summary>
    /// Check if a state has been registered.
    /// </summary>
    /// <param name="state">The state to check.</param>
    /// <returns>True if the state is registered.</returns>
    public bool HasState(TState state)
    {
        return _states.ContainsKey(state);
    }

    /// <summary>
    /// Get the number of registered states.
    /// </summary>
    public int RegisteredStateCount => _states.Count;

    /// <summary>
    /// Get the number of registered transitions.
    /// </summary>
    public int RegisteredTransitionCount => _validTransitions.Count;

    private bool ExecuteTransition(TState newState)
    {
        if (!_states.TryGetValue(newState, out var newStateInstance))
        {
            Debug.LogError($"[StateMachine] No state registered for {newState}");
            return false;
        }

        // Exit current state
        _currentStateInstance?.Exit(_context);

        // Update state tracking
        _previousState = _currentState;
        _currentState = newState;
        _currentStateInstance = newStateInstance;

        // Enter new state
        _currentStateInstance.Enter(_context);

        // Fire event
        OnStateChanged?.Invoke(_previousState, _currentState);

        return true;
    }
}
