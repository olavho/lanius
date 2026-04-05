using Lanius.Business.Layout.Models;

namespace Lanius.Business.Layout.Services;

/// <summary>
/// Progress information for layout calculation operations.
/// </summary>
public record LayoutProgress(
    int Percentage,
    string Operation,
    int ProcessedItems,
    int TotalItems
);

/// <summary>
/// Interface for layout calculation engines.
/// </summary>
public interface ILayoutEngine
{
    /// <summary>
    /// Calculate layout positions for commits in a repository.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="options">Layout options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Layout result with positioned nodes and edges.</returns>
    Task<LayoutResult> CalculateLayoutAsync(
        string repositoryId,
        LayoutOptions options,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
