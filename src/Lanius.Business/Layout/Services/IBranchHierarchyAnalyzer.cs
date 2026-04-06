using Lanius.Business.Analysis.Models;
using Lanius.Business.Layout.Models;

namespace Lanius.Business.Layout.Services;

/// <summary>
/// Analyzes branch hierarchy to determine parent relationships and merge bases.
/// </summary>
public interface IBranchHierarchyAnalyzer
{
    Task<List<BranchHierarchyInfo>> AnalyzeBranchHierarchyAsync(
        string repositoryId,
        List<Branch> branches,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
