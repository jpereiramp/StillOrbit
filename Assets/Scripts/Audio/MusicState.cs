/// <summary>
/// All possible music contexts in the game.
/// Add new entries as gameplay expands — no code changes required elsewhere
/// as long as a matching entry exists in MusicStateConfig.
/// </summary>
public enum MusicState
{
    None,
    Exploration,
    Combat,
    Boss,
    Calm,
    Base,
    Stealth,
    GameOver
}
