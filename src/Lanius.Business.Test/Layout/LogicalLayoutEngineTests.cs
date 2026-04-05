using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Lanius.Business.Models;
using Lanius.Business.Services;
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
    private LogicalLayoutEngine _layoutEngine = null!;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        _mockCommitAnalyzer = new Mock<ICommitAnalyzer>();
        _mockBranchAnalyzer = new Mock<IBranchAnalyzer>();

        _layoutEngine = new LogicalLayoutEngine(
            _mockCommitAnalyzer.Object,
            _mockBranchAnalyzer.Object);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_EmptyRepository_ReturnsEmptyResult()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions();

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync(repositoryId, false, It.IsAny<CancellationToken>()))
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
            Name = "main",
            FullName = "refs/heads/main",
            TipSha = "abc123",
            IsRemote = false
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
            .Setup(x => x.GetBranchesAsync(repositoryId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsAsync(repositoryId, "main", It.IsAny<CancellationToken>()))
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
        Assert.AreEqual("main", node.BranchName);
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
            Name = "main",
            FullName = "refs/heads/main",
            TipSha = "def456",
            IsRemote = false
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
            .Setup(x => x.GetBranchesAsync(repositoryId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsAsync(repositoryId, "main", It.IsAny<CancellationToken>()))
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
        Assert.AreEqual("main", edge.BranchName);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_MergeCommit_MarkedAsSignificant()
    {
        // Arrange
        var repositoryId = "test-repo";
        var options = new LayoutOptions();

        var branch = new Branch
        {
            Name = "main",
            FullName = "refs/heads/main",
            TipSha = "merge123",
            IsRemote = false
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
            .Setup(x => x.GetBranchesAsync(repositoryId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsAsync(repositoryId, "main", It.IsAny<CancellationToken>()))
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
            .Setup(x => x.GetBranchesAsync(repositoryId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mainBranch, devBranch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsAsync(repositoryId, "main", It.IsAny<CancellationToken>()))
            .ReturnsAsync([commit1]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsAsync(repositoryId, "develop", It.IsAny<CancellationToken>()))
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
            .Setup(x => x.GetCommitsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            Name = "main",
            FullName = "refs/heads/main",
            TipSha = "commit1",
            IsRemote = false
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
            .Setup(x => x.GetBranchesAsync(repositoryId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([branch]);

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
}
