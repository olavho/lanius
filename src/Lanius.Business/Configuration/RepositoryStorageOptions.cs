namespace Lanius.Business.Configuration;

/// <summary>
/// Configuration options for repository storage.
/// </summary>
public class RepositoryStorageOptions
{
    public const string SectionName = "RepositoryStorage";

    /// <summary>
    /// Base directory for storing cloned repositories.
    /// Default: {app-data}/repositories
    /// </summary>
    public string BasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lanius",
        "repositories");

    /// <summary>
    /// Maximum number of repositories to keep on disk (0 = unlimited).
    /// </summary>
    public int MaxRepositories { get; set; } = 0;

    /// <summary>
    /// Auto-cleanup repositories older than this (0 = no auto-cleanup).
    /// </summary>
    public TimeSpan AutoCleanupAge { get; set; } = TimeSpan.Zero;
}
