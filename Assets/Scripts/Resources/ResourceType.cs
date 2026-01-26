/// <summary>
/// Defines all resource types in the game.
/// Resources are stackable quantities (not physical items).
/// </summary>
public enum ResourceType
{
    None = 0,

    // Raw Materials
    Wood = 1,
    Stone = 2,

    // Ores
    IronOre = 10,
    CopperOre = 11,
    GoldOre = 12,

    // Refined Materials
    IronIngot = 20,
    CopperIngot = 21,
    GoldIngot = 22,

    // Other
    Fiber = 30,
    Leather = 31,
}

/// <summary>
/// Category for grouping resources in UI.
/// </summary>
public enum ResourceCategory
{
    Raw,
    Ore,
    Refined,
    Organic
}
