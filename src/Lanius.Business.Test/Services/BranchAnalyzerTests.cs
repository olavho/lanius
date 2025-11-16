using Lanius.Business.Services;
using LibGit2Sharp;
using Moq;
using DomainBranch = Lanius.Business.Models.Branch;
using DomainRepositoryInfo = Lanius.Business.Models.RepositoryInfo;

namespace Lanius.Business.Test.Services;

[TestClass]
public class BranchAnalyzerTests
{
    private Mock<IRepositoryService> _mockRepoService = null!;
    private BranchAnalyzer _analyzer = null!;
    private string _testRepoId = null!;
    private readonly List<string> _tempPaths = new();

    [TestInitialize]
    public void Setup()
    {
        _testRepoId = "test-repo";
        _mockRepoService = new Mock<IRepositoryService>();
        _analyzer = new BranchAnalyzer(_mockRepoService.Object);
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
    public async Task GetBranchesAsync_RepositoryNotFound_ThrowsException()
    {
        // Arrange
        _mockRepoService.Setup(x => x.RepositoryExists(_testRepoId)).Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _analyzer.GetBranchesAsync(_testRepoId));

        Assert.Contains("not found", exception.Message);
    }

    [TestMethod]
    public async Task GetBranchesAsync_ValidRepository_ReturnsBranches()
    {
        // Arrange
        var tempPath = CreateTemporaryRepository();
        SetupMockRepository(tempPath);

        // Act
        IReadOnlyList<DomainBranch> branches = await _analyzer.GetBranchesAsync(_testRepoId);

        // Assert
        Assert.IsNotNull(branches);
        Assert.IsNotEmpty(branches);
        Assert.IsNotNull(branches[0].Name);
        Assert.IsNotNull(branches[0].TipSha);
    }

    [TestMethod]
    public async Task GetBranchesByPatternAsync_MainPattern_ReturnsMainBranch()
    {
        // Arrange
        var tempPath = CreateTemporaryRepository();
        SetupMockRepository(tempPath);

        // Get actual default branch name
        using var repo = new Repository(tempPath);
        var defaultBranch = repo.Head.FriendlyName;

        // Act
        IReadOnlyList<DomainBranch> branches = await _analyzer.GetBranchesByPatternAsync(_testRepoId, new[] { "main", "master" });

        // Assert
        Assert.IsNotNull(branches);
        Assert.IsNotEmpty(branches);
        Assert.IsTrue(branches.Any(b => b.Name == defaultBranch));
    }

    [TestMethod]
    public async Task GetBranchesByPatternAsync_WildcardPattern_ReturnsMatchingBranches()
    {
        // Arrange
        var tempPath = CreateRepositoryWithMultipleBranches();
        SetupMockRepository(tempPath);

        // Act
        IReadOnlyList<DomainBranch> branches = await _analyzer.GetBranchesByPatternAsync(_testRepoId, new[] { "feature/*" });

        // Assert
        Assert.IsNotNull(branches);
        Assert.IsTrue(branches.All(b => b.Name.StartsWith("feature/")));
    }

    [TestMethod]
    public async Task GetBranchAsync_ExistingBranch_ReturnsBranch()
    {
        // Arrange
        var tempPath = CreateTemporaryRepository();
        SetupMockRepository(tempPath);

        // Get actual default branch name
        using var repo = new Repository(tempPath);
        var defaultBranch = repo.Head.FriendlyName;

        // Act
        DomainBranch? branch = await _analyzer.GetBranchAsync(_testRepoId, defaultBranch);

        // Assert
        Assert.IsNotNull(branch);
        Assert.AreEqual(defaultBranch, branch.Name);
    }

    [TestMethod]
    public async Task GetBranchAsync_NonExistentBranch_ReturnsNull()
    {
        // Arrange
        var tempPath = CreateTemporaryRepository();
        SetupMockRepository(tempPath);

        // Act
        DomainBranch? branch = await _analyzer.GetBranchAsync(_testRepoId, "nonexistent");

        // Assert
        Assert.IsNull(branch);
    }

    [TestMethod]
    public async Task GetBranchDivergenceAsync_TwoBranches_CalculatesDivergence()
    {
        // Arrange
        var tempPath = CreateRepositoryWithDivergentBranches();
        SetupMockRepository(tempPath);

        // Act
        var (ahead, behind) = await _analyzer.GetBranchDivergenceAsync(_testRepoId, "main", "feature");

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, ahead);
        Assert.IsGreaterThanOrEqualTo(0, behind);
    }

    [TestMethod]
    public async Task FindCommonAncestorAsync_TwoBranches_FindsMergeBase()
    {
        // Arrange
        var tempPath = CreateRepositoryWithDivergentBranches();
        SetupMockRepository(tempPath);

        // Act
        var commonAncestor = await _analyzer.FindCommonAncestorAsync(_testRepoId, "main", "feature");

        // Assert
        Assert.IsNotNull(commonAncestor);
        Assert.AreEqual(40, commonAncestor.Length); // SHA length
    }

    private string CreateTemporaryRepository()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "LaniusTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        // Track this path for cleanup
        _tempPaths.Add(tempPath);

        Repository.Init(tempPath);

        using var repo = new Repository(tempPath);
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
        repo.Config.Set("user.name", "Test User");
        repo.Config.Set("user.email", "test@example.com");

        var testFile = Path.Combine(tempPath, "test.txt");
        File.WriteAllText(testFile, "Hello World");

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

    private string CreateRepositoryWithMultipleBranches()
    {
        var tempPath = CreateTemporaryRepository();

        using var repo = new Repository(tempPath);
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);

        // Create feature branches
        repo.CreateBranch("feature/login");
        repo.CreateBranch("feature/signup");
        repo.CreateBranch("release/1.0");

        return tempPath;
    }

    private string CreateRepositoryWithDivergentBranches()
    {
        var tempPath = CreateTemporaryRepository();

        using var repo = new Repository(tempPath);
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);

        // Create and checkout feature branch
        var featureBranch = repo.CreateBranch("feature");
        Commands.Checkout(repo, featureBranch);

        // Make a commit on feature branch
        var testFile = Path.Combine(tempPath, "feature.txt");
        File.WriteAllText(testFile, "Feature work");
        Commands.Stage(repo, "feature.txt");
        repo.Commit("Feature commit", signature, signature, new CommitOptions());

        // Switch back to main
        Commands.Checkout(repo, repo.Branches["main"]);

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
