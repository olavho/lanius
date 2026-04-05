namespace Lanius.Business.Layout.Models;

/// <summary>
/// Branch hierarchy tier for organizing and processing branches.
/// Lower tier numbers are processed first (main before features).
/// </summary>
public enum BranchTier
{
    /// <summary>
    /// Main development branch (main, master)
    /// </summary>
    Main = 0,

    /// <summary>
    /// Long-lived project branches (project/*)
    /// </summary>
    Project = 1,

    /// <summary>
    /// Release branches (release/*)
    /// </summary>
    Release = 2,

    /// <summary>
    /// Short-lived feature/bugfix branches (feature/*, bugfix/*, hotfix/*)
    /// </summary>
    Feature = 3,

    /// <summary>
    /// Other branches (dependabot, etc.)
    /// </summary>
    Other = 99
}
