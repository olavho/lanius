namespace Lanius.Business.Configuration;

/// <summary>
/// Configuration options for real-time monitoring.
/// </summary>
public class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>
    /// Polling interval for checking repository updates.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether to enable real-time monitoring.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
