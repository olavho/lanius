namespace Lanius.Business.Configuration;

/// <summary>
/// Configuration options for repository storage.
/// </summary>
public class RepositoryStorageOptions
{
    public const string SectionName = "RepositoryStorage";

    private string _basePath = string.Empty;

    /// <summary>
    /// Base directory path for storing cloned repositories.
    /// Supports environment variable expansion (e.g., %LOCALAPPDATA%, %USERPROFILE%).
    /// Default: {User's LocalApplicationData}/Lanius/repositories
    /// </summary>
    public string BasePath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_basePath))
            {
                // Default to user's local app data folder
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "Lanius", "repositories");
            }

            // Expand environment variables
            return Environment.ExpandEnvironmentVariables(_basePath);
        }
        set => _basePath = value;
    }

    /// <summary>
    /// Maximum number of repositories to store.
    /// 0 = unlimited
    /// </summary>
    public int MaxRepositories { get; set; } = 0;

    /// <summary>
    /// Age after which unused repositories are cleaned up.
    /// TimeSpan format (e.g., "30.00:00:00" = 30 days).
    /// </summary>
    public TimeSpan AutoCleanupAge { get; set; } = TimeSpan.Zero;
}
