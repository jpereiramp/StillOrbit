/// <summary>
/// Interface for objects that specify what damage type should be used against them.
/// </summary>
public interface IDamageTypeProvider
{
    DamageType DamageType { get; }
}