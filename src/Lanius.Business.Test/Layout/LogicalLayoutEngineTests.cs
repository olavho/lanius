using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Lanius.Business.Models;
using Lanius.Business.Services;
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
    private Mock<IRepositoryService> _mockRepositoryService = null!;
    private Mock<ILoggerFactory> _mockLoggerFactory = null!;
    private Mock<ILogger<LogicalLayoutEngine>> _mockLogger = null!;
    private LogicalLayoutEngine _layoutEngine = null!;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        _mockCommitAnalyzer = new Mock<ICommitAnalyzer>();
        _mockBranchAnalyzer = new Mock<IBranchAnalyzer>();
        _mockRepositoryService = new Mock<IRepositoryService>();
        _mockLogger = new Mock<ILogger<LogicalLayoutEngine>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();

        // Setup logger factory to return the mock logger
        _mockLoggerFactory
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(_mockLogger.Object);

        // Setup repository service to return a valid repository info
        _mockRepositoryService
            .Setup(x => x.GetRepositoryInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new RepositoryInfo
            {
                Id = "test-repo",
                Url = "https://github.com/test/repo",
                LocalPath = @"C:\temp\test-repo",
                DefaultBranch = "main",
                ClonedAt = DateTimeOffset.UtcNow,
                LastFetchedAt = DateTimeOffset.UtcNow
            });

        _layoutEngine = new LogicalLayoutEngine(
            _mockCommitAnalyzer.Object,
            _mockBranchAnalyzer.Object,
            _mockRepositoryService.Object,
            _mockLoggerFactory.Object);
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
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/main", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([commit]);

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
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/main", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([commit1, commit2]);
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
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/main", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mergeCommit]);
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
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/main", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([commit1]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/develop", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([commit2]);
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
            .Setup(x => x.GetCommitsSinceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
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
            .Setup(x => x.GetCommitsSinceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([commit]);

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
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/main", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mainCommit]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/feature", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([mainCommit, featureCommit]);

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
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/main", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([baseCommit, mergeCommit]);
        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsSinceAsync(repositoryId, "origin/feature", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([baseCommit, featureCommit]);

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
}

