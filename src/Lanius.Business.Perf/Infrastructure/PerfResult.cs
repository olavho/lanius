namespace Lanius.Business.Perf.Infrastructure;

/// <summary>
/// A single recorded performance measurement for one test run.
/// </summary>
public record PerfResult(
    string MachineName,
    string DotNetVersion,
    string TestName,
    string RepositoryPath,
    DateTimeOffset Timestamp,
    Dictionary<string, long> PhaseMs);
