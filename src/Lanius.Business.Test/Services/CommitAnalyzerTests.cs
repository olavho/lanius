using Lanius.Business.Models;
using Lanius.Business.Services;
using Moq;
using LibGit2Sharp;
using DomainCommit = Lanius.Business.Models.Commit;
using DomainDiffStats = Lanius.Business.Models.DiffStats;
using DomainRepositoryInfo = Lanius.Business.Models.RepositoryInfo;

namespace Lanius.Business.Test.Services;

[TestClass]
public class CommitAnalyzerTests
{
    private Mock<IRepositoryService> _mockRepoService = null!;
    private CommitAnalyzer _analyzer = null!;
    private string _testRepoId = null!;
    private readonly List<string> _tempPaths = new();

    [TestInitialize]
    public void Setup()
    {
        _testRepoId = "test-repo";
        _mockRepoService = new Mock<IRepositoryService>();
        _analyzer = new CommitAnalyzer(_mockRepoService.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Force garbage collection to release any file handles
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Small delay to allow OS to release file locks
        System.Threading.Thread.Sleep(100);

        // Clean up all temporary repositories created during this test
        foreach (var tempPath in _tempPaths)
        {
            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        _tempPaths.Clear();
    }

    [TestMethod]
    public async Task GetCommitsAsync_RepositoryNotFound_ThrowsException()
    {
        // Arrange
        _mockRepoService.Setup(x => x.RepositoryExists(_testRepoId)).Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _analyzer.GetCommitsAsync(_testRepoId));
        
        Assert.IsTrue(exception.Message.Contains("not found"));
    }

    [TestMethod]
    public async Task GetCommitAsync_InvalidSha_ReturnsNull()
    {
        // Arrange
        var tempPath = CreateTemporaryRepository();
        SetupMockRepository(tempPath);

        // Act
        DomainCommit? commit = await _analyzer.GetCommitAsync(_testRepoId, "invalid-sha-12345678");

        // Assert
        Assert.IsNull(commit);
    }

    [TestMethod]
    public async Task GetCommitsAsync_ValidRepository_ReturnsCommits()
    {
        // Arrange
        var tempPath = CreateTemporaryRepository();
        SetupMockRepository(tempPath);

        // Act
        IReadOnlyList<DomainCommit> commits = await _analyzer.GetCommitsAsync(_testRepoId);

        // Assert
        Assert.IsNotNull(commits);
        Assert.IsTrue(commits.Count > 0);
        Assert.IsNotNull(commits[0].Sha);
        Assert.IsNotNull(commits[0].Author);
        Assert.IsNotNull(commits[0].Message);
    }

    [TestMethod]
    public async Task GetCommitsChronologicallyAsync_ValidRepository_ReturnsOrderedCommits()
    {
        // Arrange
        var tempPath = CreateTemporaryRepository();
        SetupMockRepository(tempPath);

        // Act
        IReadOnlyList<DomainCommit> commits = await _analyzer.GetCommitsChronologicallyAsync(_testRepoId);

        // Assert
        Assert.IsNotNull(commits);
        Assert.IsTrue(commits.Count > 0);

        // Verify chronological order
        for (int i = 1; i < commits.Count; i++)
        {
            Assert.IsTrue(commits[i].Timestamp >= commits[i - 1].Timestamp,
                "Commits should be in chronological order");
        }
    }

    [TestMethod]
    public async Task GetCommitStatsAsync_ValidCommit_ReturnsStats()
    {
        // Arrange
        var tempPath = CreateTemporaryRepository();
        SetupMockRepository(tempPath);
        
        IReadOnlyList<DomainCommit> commits = await _analyzer.GetCommitsAsync(_testRepoId);
        var firstCommitSha = commits[0].Sha;

        // Act
        var stats = await _analyzer.GetCommitStatsAsync(_testRepoId, firstCommitSha);

        // Assert
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.LinesAdded >= 0);
        Assert.IsTrue(stats.LinesRemoved >= 0);
        Assert.IsTrue(stats.FilesChanged >= 0);
    }

    [TestMethod]
    public void DiffStats_ColorIndicator_CalculatesCorrectly()
    {
        // Arrange & Act
        var additionsOnly = new DomainDiffStats { LinesAdded = 100, LinesRemoved = 0, FilesChanged = 1 };
        var deletionsOnly = new DomainDiffStats { LinesAdded = 0, LinesRemoved = 100, FilesChanged = 1 };
        var balanced = new DomainDiffStats { LinesAdded = 50, LinesRemoved = 50, FilesChanged = 1 };
        var moreAdditions = new DomainDiffStats { LinesAdded = 75, LinesRemoved = 25, FilesChanged = 1 };

        // Assert
        Assert.AreEqual(1.0, additionsOnly.ColorIndicator, 0.01);
        Assert.AreEqual(-1.0, deletionsOnly.ColorIndicator, 0.01);
        Assert.AreEqual(0.0, balanced.ColorIndicator, 0.01);
        Assert.AreEqual(0.5, moreAdditions.ColorIndicator, 0.01);
    }

    private string CreateTemporaryRepository()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "LaniusTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        // Track this path for cleanup
        _tempPaths.Add(tempPath);

        // Initialize a git repository
        Repository.Init(tempPath);

        using var repo = new Repository(tempPath);

        // Configure identity and set default branch to main
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
        repo.Config.Set("user.name", "Test User");
        repo.Config.Set("user.email", "test@example.com");

        // Create a test file
        var testFile = Path.Combine(tempPath, "test.txt");
        File.WriteAllText(testFile, "Hello World");

        // Stage and commit
        Commands.Stage(repo, "test.txt");
        repo.Commit("Initial commit", signature, signature, new CommitOptions());

        // Rename branch to 'main' if it's not already
        var headBranch = repo.Head.FriendlyName;
        if (headBranch != "main")
        {
            var mainBranch = repo.CreateBranch("main");
            Commands.Checkout(repo, mainBranch);
        }

        return tempPath;
    }

    private void SetupMockRepository(string localPath)
    {
        _mockRepoService.Setup(x => x.RepositoryExists(_testRepoId)).Returns(true);
        
        // Get the actual default branch name
        using var repo = new Repository(localPath);
        var defaultBranch = repo.Head.FriendlyName;
        
        _mockRepoService.Setup(x => x.GetRepositoryInfoAsync(_testRepoId))
            .ReturnsAsync(new DomainRepositoryInfo
            {
                Id = _testRepoId,
                Url = "test://repo",
                LocalPath = localPath,
                DefaultBranch = defaultBranch,
                ClonedAt = DateTimeOffset.UtcNow,
                TotalCommits = 1,
                TotalBranches = 1
            });
    }
}
