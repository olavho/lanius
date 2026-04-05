namespace Lanius.Business.Models;

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
