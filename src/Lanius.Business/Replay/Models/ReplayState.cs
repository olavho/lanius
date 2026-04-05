namespace Lanius.Business.Replay.Models;

/// <summary>
/// State of a replay session.
/// </summary>
public enum ReplayState
{
    Idle,
    Playing,
    Paused,
    Completed,
    Cancelled
}
