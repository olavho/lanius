namespace Lanius.Api.DTOs;

/// <summary>
/// Standard error response.
/// </summary>
public class ErrorResponse
{
    public required string Error { get; init; }
    public required string Message { get; init; }
    public string? Detail { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
