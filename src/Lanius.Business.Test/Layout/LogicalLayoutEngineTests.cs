using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Lanius.Business.Storage.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace Lanius.Business.Test.Layout;

/// <summary>
/// Synchronous progress reporter for testing (avoids race conditions with Progress{T}).
/// </summary>
internal class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}

[TestClass]
public class LogicalLayoutEngineTests
{
    private Mock<ICommitAnalyzer> _mockCommitAnalyzer = null!;
    private Mock<IBranchAnalyzer> _mockBranchAnalyzer = null!;
    private Mock<IBranchHierarchyAnalyzer> _mockBranchHierarchyAnalyzer = null!;
    private Mock<IRepositoryStorageService> _mockRepositoryService = null!;
    private Mock<ILogger<LogicalLayoutEngine>> _mockLogger = null!;
    private IMemoryCache _memoryCache = null!;
    private LogicalLayoutEngine _layoutEngine = null!;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        _mockCommitAnalyzer = new Mock<ICommitAnalyzer>();
        _mockBranchAnalyzer = new Mock<IBranchAnalyzer>();
        _mockRepositoryService = new Mock<IRepositoryStorageService>();
        _mockLogger = new Mock<ILogger<LogicalLayoutEngine>>();
        _mockBranchHierarchyAnalyzer = new Mock<IBranchHierarchyAnalyzer>();

        // Default: return simple tier-less hierarchy for any branches passed
        _mockBranchHierarchyAnalyzer
            .Setup(x => x.AnalyzeBranchHierarchyAsync(
                It.IsAny<string>(),
                It.IsAny<List<Branch>>(),
                It.IsAny<IProgress<LayoutProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, List<Branch> branches, IProgress<LayoutProgress>? _, CancellationToken _) =>
                Task.FromResult(branches.Select(b => new BranchHierarchyInfo
                {
                    Name = b.Name,
                    Tier = BranchTier.Other,
                    MergeBaseSha = null,
                    CommitCount = 0
                }).ToList()));

        // Setup repository service to return a valid repository info
        _mockRepositoryService
            .Setup(x => x.GetRepositoryPath(It.IsAny<string>()))
            .Returns(@"C:\temp\test-repo");

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _layoutEngine = new LogicalLayoutEngine(
            _mockCommitAnalyzer.Object,
            _mockBranchAnalyzer.Object,
            _mockBranchHierarchyAnalyzer.Object,
            _mockRepositoryService.Object,
            _memoryCache,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_EmptyRepository_ReturnsEmptyResult()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions();

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. new List<Branch>()]);

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(LayoutMode.Logical, result.Mode);
        Assert.IsEmpty(result.Nodes);
        Assert.IsEmpty(result.Edges);
        Assert.AreEqual(0, result.TotalCommits);
        Assert.AreEqual(0, result.TotalBranches);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_SingleBranchSingleCommit_CreatesOneNode()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions { CanvasWidth = 1200, CanvasHeight = 600 };

        var branch = new Branch
        {
            Name = "origin/main",
            FullName = "refs/remotes/origin/main",
            TipSha = "abc123",
            IsRemote = true
        };

        var commit = new Commit
        {
            Sha = "abc123",
            Author = "Test Author",
            AuthorEmail = "test@example.com",
            Timestamp = DateTimeOffset.UtcNow,
            Message = "Initial commit",
            ParentShas = []
        };

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = [commit] });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Nodes);
        Assert.IsEmpty(result.Edges); // No edges for single commit
        Assert.AreEqual(1, result.TotalCommits);
        Assert.AreEqual(1, result.TotalBranches);

        var node = result.Nodes[0];
        Assert.AreEqual("abc123", node.CommitId);
        Assert.AreEqual("origin/main", node.BranchName);
        Assert.AreEqual(4, node.Radius); // Not a merge commit
        Assert.IsFalse(node.IsSignificant);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_TwoCommitsOneBranch_CreatesNodesAndEdge()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions();

        var branch = new Branch
        {
            Name = "origin/main",
            FullName = "refs/remotes/origin/main",
            TipSha = "def456",
            IsRemote = true
        };

        var commit1 = new Commit
        {
            Sha = "abc123",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-1),
            Message = "First commit",
            ParentShas = []
        };

        var commit2 = new Commit
        {
            Sha = "def456",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow,
            Message = "Second commit",
            ParentShas = ["abc123"]
        };

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = [commit1, commit2] });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.HasCount(2, result.Nodes);
        Assert.HasCount(1, result.Edges); // One edge connecting commits

        var edge = result.Edges[0];
        Assert.AreEqual("abc123", edge.FromCommitId);
        Assert.AreEqual("def456", edge.ToCommitId);
        Assert.AreEqual(EdgeType.Normal, edge.Type);
        Assert.AreEqual("origin/main", edge.BranchName);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_MergeCommit_MarkedAsSignificant()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions();

        var branch = new Branch
        {
            Name = "origin/main",
            FullName = "refs/remotes/origin/main",
            TipSha = "merge123",
            IsRemote = true
        };

        var mergeCommit = new Commit
        {
            Sha = "merge123",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow,
            Message = "Merge branch 'feature'",
            ParentShas = ["abc123", "def456"] // Two parents = merge
        };

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = [mergeCommit] });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        var node = result.Nodes[0];
        Assert.IsTrue(node.IsSignificant);
        Assert.AreEqual(6, node.Radius); // Larger radius for significant commits
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_MultipleBranches_AssignsDifferentYLanes()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions { BranchSpacing = 40, MarginY = 60 };

        var mainBranch = new Branch
        {
            Name = "origin/main",
            FullName = "refs/remotes/origin/main",
            TipSha = "commit1",
            IsRemote = true
        };

        var devBranch = new Branch
        {
            Name = "origin/develop",
            FullName = "refs/remotes/origin/develop",
            TipSha = "commit2",
            IsRemote = true
        };

        var commit1 = new Commit
        {
            Sha = "commit1",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow,
            Message = "Main commit",
            ParentShas = []
        };

        var commit2 = new Commit
        {
            Sha = "commit2",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow,
            Message = "Dev commit",
            ParentShas = []
        };

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mainBranch, devBranch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>>
            {
                ["origin/main"] = [commit1],
                ["origin/develop"] = [commit2]
            });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.HasCount(2, result.Nodes);
        Assert.AreEqual(2, result.TotalBranches);

        // Nodes should have different Y positions (different lanes)
        var yPositions = result.Nodes.Select(n => n.Y).Distinct().ToList();
        Assert.HasCount(2, yPositions, "Each branch should have its own Y lane");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_WithBranchFilter_FiltersCorrectly()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions { BranchFilter = "main,develop" };

        var mainBranch = new Branch
        {
            Name = "main",
            FullName = "refs/heads/main",
            TipSha = "commit1",
            IsRemote = false
        };

        var devBranch = new Branch
        {
            Name = "develop",
            FullName = "refs/heads/develop",
            TipSha = "commit2",
            IsRemote = false
        };

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesByPatternAsync(
                repositoryId,
                It.Is<IEnumerable<string>>(p => p.Contains("main") && p.Contains("develop")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([mainBranch, devBranch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsBatchAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>>());

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        _mockBranchAnalyzer.Verify(
            x => x.GetBranchesByPatternAsync(
                repositoryId,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockBranchAnalyzer.Verify(
            x => x.GetBranchesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_ReportsProgress()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions();
        var progressReports = new List<LayoutProgress>();
        var progress = new SynchronousProgress<LayoutProgress>(p => progressReports.Add(p));

        var branch = new Branch
        {
            Name = "origin/main",
            FullName = "refs/remotes/origin/main",
            TipSha = "commit1",
            IsRemote = true
        };

        var commit = new Commit
        {
            Sha = "commit1",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow,
            Message = "Test commit",
            ParentShas = []
        };

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsBatchAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = [commit] });

        // Act
        await _layoutEngine.CalculateLayoutAsync(repositoryId, options, progress, TestContext.CancellationToken);

        // Assert
        Assert.IsNotEmpty(progressReports, "Should report progress");
        Assert.IsTrue(progressReports.Any(p => p.Percentage == 0), "Should report 0% progress");
        Assert.IsTrue(progressReports.Any(p => p.Percentage == 100), "Should report 100% progress");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_NullRepositoryId_ThrowsArgumentException()
    {
        // Arrange
        var options = new LayoutOptions();

        // Act & Assert
        var exception = false;
        try
        {
            await _layoutEngine.CalculateLayoutAsync(null!, options, cancellationToken: TestContext.CancellationToken);
        }
        catch (ArgumentException)
        {
            exception = true;
        }

        Assert.IsTrue(exception, "Expected ArgumentException to be thrown for null repositoryId");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var repositoryId = "test-repo";

        // Act & Assert
        var exception = false;
        try
        {
            await _layoutEngine.CalculateLayoutAsync(repositoryId, null!, cancellationToken: TestContext.CancellationToken);
        }
        catch (ArgumentNullException)
        {
            exception = true;
        }

        Assert.IsTrue(exception, "Expected ArgumentNullException to be thrown for null options");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_BranchSplit_CreatesBranchEdge()
    {
        // Arrange: main branch with commit, feature branch splits from it
        var repositoryId = "test-repo";
        var options = new LayoutOptions();

        var mainBranch = new Branch
        {
            Name = "origin/main",
            FullName = "refs/remotes/origin/main",
            TipSha = "main1",
            IsRemote = true
        };

        var featureBranch = new Branch
        {
            Name = "origin/feature",
            FullName = "refs/remotes/origin/feature",
            TipSha = "feature1",
            IsRemote = true
        };

        var mainCommit = new Commit
        {
            Sha = "main1",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2),
            Message = "Main commit",
            ParentShas = []
        };

        var featureCommit = new Commit
        {
            Sha = "feature1",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-1),
            Message = "Feature commit",
            ParentShas = ["main1"] // Feature branches from main
        };

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mainBranch, featureBranch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>>
            {
                ["origin/main"] = [mainCommit],
                ["origin/feature"] = [mainCommit, featureCommit]
            });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.HasCount(2, result.Nodes);

        // Should have branch edge from main1 to feature1
        var branchEdge = result.Edges.FirstOrDefault(e =>
            e.FromCommitId == "main1" &&
            e.ToCommitId == "feature1" &&
            e.Type == EdgeType.Branch);

        Assert.IsNotNull(branchEdge, "Should have Branch-type edge for split point");
        Assert.AreEqual("origin/feature", branchEdge.BranchName);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_MergeBranches_CreatesMergeEdge()
    {
        // Arrange: feature branch merges back into main
        var repositoryId = "test-repo";
        var options = new LayoutOptions();

        var mainBranch = new Branch
        {
            Name = "origin/main",
            FullName = "refs/remotes/origin/main",
            TipSha = "merge1",
            IsRemote = true
        };

        var featureBranch = new Branch
        {
            Name = "origin/feature",
            FullName = "refs/remotes/origin/feature",
            TipSha = "feature1",
            IsRemote = true
        };

        var baseCommit = new Commit
        {
            Sha = "base1",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-3),
            Message = "Base commit",
            ParentShas = []
        };

        var featureCommit = new Commit
        {
            Sha = "feature1",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2),
            Message = "Feature work",
            ParentShas = ["base1"]
        };

        var mergeCommit = new Commit
        {
            Sha = "merge1",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-1),
            Message = "Merge feature into main",
            ParentShas = ["base1", "feature1"] // Two parents = merge (IsMerge computed from this)
        };

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mainBranch, featureBranch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>>
            {
                ["origin/main"] = [baseCommit, mergeCommit],
                ["origin/feature"] = [baseCommit, featureCommit]
            });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.HasCount(3, result.Nodes);

        // Should have merge edge from feature1 to merge1
        var mergeEdge = result.Edges.FirstOrDefault(e =>
            e.FromCommitId == "feature1" &&
            e.ToCommitId == "merge1" &&
            e.Type == EdgeType.Merge);

        Assert.IsNotNull(mergeEdge, "Should have Merge-type edge for merge point");
        Assert.AreEqual("origin/main", mergeEdge.BranchName, "Merge edge should belong to target branch");

        // Verify merge commit is marked as significant
        var mergeNode = result.Nodes.First(n => n.CommitId == "merge1");
        Assert.IsTrue(mergeNode.IsSignificant);
        Assert.AreEqual(6, mergeNode.Radius);
    }

    // ── Phase 2: Grid coordinate tests ──────────────────────────────────────

    [TestMethod]
    public async Task CalculateLayoutAsync_SingleBranch_GridColumnsAreChronological()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions();
        var branch = new Branch { Name = "origin/main", FullName = "refs/remotes/origin/main", TipSha = "c3", IsRemote = true };
        var t0 = DateTimeOffset.UtcNow;
        var commit1 = new Commit { Sha = "c1", Author = "A", AuthorEmail = "a@b", Timestamp = t0, Message = "1", ParentShas = [] };
        var commit2 = new Commit { Sha = "c2", Author = "A", AuthorEmail = "a@b", Timestamp = t0.AddHours(1), Message = "2", ParentShas = ["c1"] };
        var commit3 = new Commit { Sha = "c3", Author = "A", AuthorEmail = "a@b", Timestamp = t0.AddHours(2), Message = "3", ParentShas = ["c2"] };

        _mockBranchAnalyzer.Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>())).ReturnsAsync([branch]);
        _mockCommitAnalyzer.Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = [commit1, commit2, commit3] });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        var nodes = result.Nodes.OrderBy(n => n.Timestamp).ToList();
        Assert.AreEqual(0, nodes[0].GridColumn, "Earliest commit should be column 0");
        Assert.AreEqual(1, nodes[1].GridColumn, "Middle commit should be column 1");
        Assert.AreEqual(2, nodes[2].GridColumn, "Latest commit should be column 2");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_SingleBranch_NoColumnCollisionsOnSameRow()
    {
        // Arrange: three commits at the same timestamp (maximum collision risk)
        var repositoryId = "test-repo";
        var options = new LayoutOptions();
        var branch = new Branch { Name = "origin/main", FullName = "refs/remotes/origin/main", TipSha = "c3", IsRemote = true };
        var sameTime = DateTimeOffset.UtcNow;
        var commit1 = new Commit { Sha = "aaa", Author = "A", AuthorEmail = "a@b", Timestamp = sameTime, Message = "1", ParentShas = [] };
        var commit2 = new Commit { Sha = "bbb", Author = "A", AuthorEmail = "a@b", Timestamp = sameTime, Message = "2", ParentShas = [] };
        var commit3 = new Commit { Sha = "ccc", Author = "A", AuthorEmail = "a@b", Timestamp = sameTime, Message = "3", ParentShas = [] };

        _mockBranchAnalyzer.Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>())).ReturnsAsync([branch]);
        _mockCommitAnalyzer.Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = [commit1, commit2, commit3] });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert: all three on the same row — each must have a unique column
        var columnsOnRow0 = result.Nodes.Where(n => n.GridRow == 0).Select(n => n.GridColumn).ToList();
        Assert.HasCount(3, columnsOnRow0);
        Assert.AreEqual(columnsOnRow0.Count, columnsOnRow0.Distinct().Count(), "No two nodes on the same row should share a column");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_MultipleBranches_GridRowsMatchBranchOrder()
    {
        // Arrange: two branches — 'origin/develop' sorts before 'origin/main' alphabetically
        var repositoryId = "test-repo";
        var options = new LayoutOptions { BranchSpacing = 40, MarginY = 60 };
        var mainBranch = new Branch { Name = "origin/main", FullName = "refs/remotes/origin/main", TipSha = "m1", IsRemote = true };
        var devBranch = new Branch { Name = "origin/develop", FullName = "refs/remotes/origin/develop", TipSha = "d1", IsRemote = true };
        var t0 = DateTimeOffset.UtcNow;
        var mainCommit = new Commit { Sha = "m1", Author = "A", AuthorEmail = "a@b", Timestamp = t0, Message = "main", ParentShas = [] };
        var devCommit = new Commit { Sha = "d1", Author = "A", AuthorEmail = "a@b", Timestamp = t0.AddHours(1), Message = "dev", ParentShas = [] };

        _mockBranchAnalyzer.Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>())).ReturnsAsync([mainBranch, devBranch]);
        _mockCommitAnalyzer.Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = [mainCommit], ["origin/develop"] = [devCommit] });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert: origin/develop (row 0) and origin/main (row 1)
        var devNode = result.Nodes.First(n => n.BranchName == "origin/develop");
        var mainNode = result.Nodes.First(n => n.BranchName == "origin/main");

        Assert.AreEqual(0, devNode.GridRow, "origin/develop should be row 0 (alphabetically first)");
        Assert.AreEqual(1, mainNode.GridRow, "origin/main should be row 1");
        Assert.AreEqual(options.MarginY, devNode.Y, "Row 0 Y = MarginY");
        Assert.AreEqual(options.MarginY + options.BranchSpacing, mainNode.Y, "Row 1 Y = MarginY + BranchSpacing");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_ThreeCommits_LayoutResultHasCorrectRowAndColumnCount()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions();
        var branch = new Branch { Name = "origin/main", FullName = "refs/remotes/origin/main", TipSha = "c3", IsRemote = true };
        var t0 = DateTimeOffset.UtcNow;
        var commits = new List<Commit>
        {
            new() { Sha = "c1", Author = "A", AuthorEmail = "a@b", Timestamp = t0,             Message = "1", ParentShas = [] },
            new() { Sha = "c2", Author = "A", AuthorEmail = "a@b", Timestamp = t0.AddHours(1), Message = "2", ParentShas = ["c1"] },
            new() { Sha = "c3", Author = "A", AuthorEmail = "a@b", Timestamp = t0.AddHours(2), Message = "3", ParentShas = ["c2"] }
        };

        _mockBranchAnalyzer.Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>())).ReturnsAsync([branch]);
        _mockCommitAnalyzer.Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = commits });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(1, result.RowCount, "One branch = one row");
        Assert.AreEqual(3, result.ColumnCount, "Three commits = three columns");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_GridColumnX_MatchesMarginPlusColumnTimesWidth()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions { MarginX = 50, ColumnWidth = 25.0 };
        var branch = new Branch { Name = "origin/main", FullName = "refs/remotes/origin/main", TipSha = "c2", IsRemote = true };
        var t0 = DateTimeOffset.UtcNow;
        var commit1 = new Commit { Sha = "c1", Author = "A", AuthorEmail = "a@b", Timestamp = t0,             Message = "1", ParentShas = [] };
        var commit2 = new Commit { Sha = "c2", Author = "A", AuthorEmail = "a@b", Timestamp = t0.AddHours(1), Message = "2", ParentShas = ["c1"] };

        _mockBranchAnalyzer.Setup(x => x.GetBranchesAsync(repositoryId, true, It.IsAny<CancellationToken>())).ReturnsAsync([branch]);
        _mockCommitAnalyzer.Setup(x => x.GetCommitsBatchAsync(repositoryId, It.IsAny<IReadOnlyList<(string, string?)>>(), It.IsAny<IProgress<(int, int, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Commit>> { ["origin/main"] = [commit1, commit2] });

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync(repositoryId, options, cancellationToken: TestContext.CancellationToken);

        // Assert: X = MarginX + GridColumn * ColumnWidth
        foreach (var node in result.Nodes)
        {
            var expectedX = options.MarginX + node.GridColumn * options.ColumnWidth;
            Assert.AreEqual(expectedX, node.X, $"Node {node.CommitId}: X should equal MarginX + GridColumn * ColumnWidth");
        }
    }
}

