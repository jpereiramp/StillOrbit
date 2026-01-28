/// <summary>
/// Companion is navigating to a resource depot.
/// Destination is set by CompanionAutoDeposit before state transition.
/// </summary>
public class CompanionMovingToDepotState : BaseState<CompanionContext>
{
    // Movement destination is set by AutoDeposit.TriggerAutoDeposit()
    // No additional logic needed here - AutoDeposit handles depot navigation
}
