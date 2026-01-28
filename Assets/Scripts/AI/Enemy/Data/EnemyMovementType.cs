/// <summary>
/// How the enemy moves through the world.
/// </summary>
public enum EnemyMovementType
{
    /// <summary>Standard ground NavMesh navigation.</summary>
    Ground,

    /// <summary>Flying movement (ignores NavMesh Y).</summary>
    Flying,

    /// <summary>Stationary turret-style enemy.</summary>
    Stationary,

    /// <summary>Burrowing/tunneling movement.</summary>
    Burrowing
}