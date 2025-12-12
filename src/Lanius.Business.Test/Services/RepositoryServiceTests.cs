using Lanius.Business.Configuration;
using Lanius.Business.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Options;

namespace Lanius.Business.Test.Services;

[TestClass]
public class RepositoryServiceTests
{
    private string _testBasePath = null!;
    private RepositoryService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _testBasePath = Path.Combine(Path.GetTempPath(), "LaniusTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testBasePath);

        var options = Options.Create(new RepositoryStorageOptions
        {
            BasePath = _testBasePath
        });

        _service = new RepositoryService(options, null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testBasePath))
        {
            try
            {
                // Force garbage collection to release any file handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Small delay to allow OS to release file locks
                System.Threading.Thread.Sleep(100);

                Directory.Delete(_testBasePath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors - OS will clean up temp directory eventually
            }
        }
    }

    [TestMethod]
    [Ignore("Requires network access - run manually or in CI/CD")]
    public async Task CloneRepositoryAsync_ValidUrl_CreatesRepository()
    {
        // Arrange
        var url = "https://github.com/octocat/Hello-World.git";

        // Act
        var info = await _service.CloneRepositoryAsync(url);

        // Assert
        Assert.IsNotNull(info);
        Assert.AreEqual(url, info.Url);
        Assert.IsTrue(Directory.Exists(info.LocalPath));
        Assert.IsTrue(_service.RepositoryExists(info.Id));
    }

    [TestMethod]
    public async Task CloneRepositoryAsync_RepositoryAlreadyExists_FetchesAndReturnsInfo()
    {
        // Arrange - Create a local repository instead of cloning from GitHub
        var localRepoPath = CreateLocalTestRepository();
        var url = "test://fake-url";

        // Manually create repository in service's storage
        var repoId = GenerateRepositoryId(url);
        var targetPath = Path.Combine(_testBasePath, repoId);
        Directory.Move(localRepoPath, targetPath);

        // Add remote to repository
        using (var repo = new Repository(targetPath))
        {
            repo.Network.Remotes.Add("origin", url);
        }

        // Act - Should not throw, should return existing repository info
        var info = await _service.CloneRepositoryAsync(url);

        // Assert
        Assert.IsNotNull(info);
        Assert.AreEqual(repoId, info.Id);
        Assert.AreEqual(url, info.Url);
        Assert.IsTrue(_service.RepositoryExists(repoId));
    }

    [TestMethod]
    public async Task GetRepositoryInfoAsync_ExistingRepository_ReturnsInfo()
    {
        // Arrange - Create a local repository
        var localRepoPath = CreateLocalTestRepository();
        var url = "test://example-repo";
        var repoId = GenerateRepositoryId(url);
        var targetPath = Path.Combine(_testBasePath, repoId);
        Directory.Move(localRepoPath, targetPath);

        // Add remote to repository
        using (var repo = new Repository(targetPath))
        {
            repo.Network.Remotes.Add("origin", url);
        }

        // Act
        var info = await _service.GetRepositoryInfoAsync(repoId);

        // Assert
        Assert.IsNotNull(info);
        Assert.AreEqual(repoId, info.Id);
        Assert.AreEqual(url, info.Url);
        Assert.IsGreaterThan(0, info.TotalCommits);
    }

    [TestMethod]
    public async Task GetRepositoryInfoAsync_NonExistentRepository_ReturnsNull()
    {
        // Arrange
        var nonExistentId = "nonexistent";

        // Act
        var info = await _service.GetRepositoryInfoAsync(nonExistentId);

        // Assert
        Assert.IsNull(info);
    }

    [TestMethod]
    public void RepositoryExists_ExistingRepository_ReturnsTrue()
    {
        // Arrange - Create a local repository
        var localRepoPath = CreateLocalTestRepository();
        var url = "test://example-repo";
        var repoId = GenerateRepositoryId(url);
        var targetPath = Path.Combine(_testBasePath, repoId);
        Directory.Move(localRepoPath, targetPath);

        // Act
        var exists = _service.RepositoryExists(repoId);

        // Assert
        Assert.IsTrue(exists);
    }

    [TestMethod]
    public void RepositoryExists_NonExistentRepository_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = "nonexistent";

        // Act
        var exists = _service.RepositoryExists(nonExistentId);

        // Assert
        Assert.IsFalse(exists);
    }

    [TestMethod]
    public async Task DeleteRepositoryAsync_ExistingRepository_RemovesDirectory()
    {
        // Arrange - Create a local repository
        var localRepoPath = CreateLocalTestRepository();
        var url = "test://example-repo";
        var repoId = GenerateRepositoryId(url);
        var targetPath = Path.Combine(_testBasePath, repoId);
        Directory.Move(localRepoPath, targetPath);

        // Act
        await _service.DeleteRepositoryAsync(repoId);

        // Assert
        Assert.IsFalse(Directory.Exists(targetPath));
        Assert.IsFalse(_service.RepositoryExists(repoId));
    }

    [TestMethod]
    public async Task DeleteRepositoryAsync_NonExistentRepository_DoesNotThrow()
    {
        // Arrange
        var nonExistentId = "nonexistent";

        // Act & Assert - should not throw
        await _service.DeleteRepositoryAsync(nonExistentId);
    }

    private string CreateLocalTestRepository()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "LaniusTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        Repository.Init(tempPath);

        using var repo = new Repository(tempPath);
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
        repo.Config.Set("user.name", "Test User");
        repo.Config.Set("user.email", "test@example.com");

        var testFile = Path.Combine(tempPath, "README.md");
        File.WriteAllText(testFile, "# Test Repository");

        Commands.Stage(repo, "README.md");
        repo.Commit("Initial commit", signature, signature, new CommitOptions());

        return tempPath;
    }

    private string GenerateRepositoryId(string url)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
