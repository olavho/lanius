namespace Lanius.Api.DTOs;

/// <summary>
/// Request to clone a Git repository.
/// </summary>
public class CloneRepositoryRequest
{
    /// <summary>
    /// The Git repository URL (HTTPS or SSH).
    /// </summary>
    public required string Url { get; init; }
}
