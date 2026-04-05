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
    private const int CleanupRetryCount = 3;
    private const int CleanupRetryDelayMs = 200;

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
        if (!Directory.Exists(_testBasePath))
        {
            return;
        }

        // Retry cleanup multiple times to handle file locks
        for (int i = 0; i < CleanupRetryCount; i++)
        {
            try
            {
                // Force garbage collection to release any file handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Delay increases with each retry
                System.Threading.Thread.Sleep(CleanupRetryDelayMs * (i + 1));

                // Remove read-only attributes before deletion
                RemoveReadOnlyAttributes(_testBasePath);

                Directory.Delete(_testBasePath, recursive: true);
                return; // Success
            }
            catch (IOException) when (i < CleanupRetryCount - 1)
            {
                // Retry on IOException
                continue;
            }
            catch (UnauthorizedAccessException) when (i < CleanupRetryCount - 1)
            {
                // Retry on access errors
                continue;
            }
            catch
            {
                // Ignore all errors on final attempt - OS will clean up temp directory eventually
                break;
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
        var info = await _service.CloneRepositoryAsync(url, TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(info);
        Assert.AreEqual(url, info.Url);
        Assert.IsTrue(Directory.Exists(info.LocalPath));
        Assert.IsTrue(_service.RepositoryExists(info.Id));
    }

    [TestMethod]
    public async Task CloneRepositoryAsync_RepositoryAlreadyExists_FetchesAndReturnsInfo()
    {
        // Arrange - Create a local repository directly in target location
        var url = "test://fake-url-01";
        var repoId = GenerateRepositoryId(url);
        var targetPath = Path.Combine(_testBasePath, repoId);

        CreateLocalTestRepository(targetPath, url);

        // Act - Should not throw, should return existing repository info
        var info = await _service.CloneRepositoryAsync(url, TestContext.CancellationToken);

        // Assert
        Assert.IsNotNull(info);
        Assert.AreEqual(repoId, info.Id);
        Assert.AreEqual(url, info.Url);
        Assert.IsTrue(_service.RepositoryExists(repoId));
    }

    [TestMethod]
    public async Task GetRepositoryInfoAsync_ExistingRepository_ReturnsInfo()
    {
        // Arrange - Create a local repository directly in target location
        var url = "test://example-repo-02";
        var repoId = GenerateRepositoryId(url);
        var targetPath = Path.Combine(_testBasePath, repoId);

        CreateLocalTestRepository(targetPath, url);

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
        // Arrange - Create a local repository directly in target location
        var url = "test://example-repo-03";
        var repoId = GenerateRepositoryId(url);
        var targetPath = Path.Combine(_testBasePath, repoId);

        CreateLocalTestRepository(targetPath, url);

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
        // Arrange - Create a local repository directly in target location
        var url = "test://example-repo-4";
        var repoId = GenerateRepositoryId(url);
        var targetPath = Path.Combine(_testBasePath, repoId);

        CreateLocalTestRepository(targetPath, url);

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

    [TestMethod]
    public async Task ListRepositoriesAsync_NoRepositories_ReturnsEmptyList()
    {
        // Arrange - clean base path (already done in Setup)

        // Act
        var repositories = await _service.ListRepositoriesAsync();

        // Assert
        Assert.IsNotNull(repositories);
        Assert.AreEqual(0, repositories.Count());
    }

    [TestMethod]
    public async Task ListRepositoriesAsync_WithRepositories_ReturnsList()
    {
        // Arrange - Create multiple test repositories
        var repo1Url = "test://example-repo-1";
        var repo1Id = GenerateRepositoryId(repo1Url);
        var repo1Path = Path.Combine(_testBasePath, repo1Id);
        CreateLocalTestRepository(repo1Path, repo1Url);

        // Add small delay to ensure different timestamps
        await Task.Delay(10, TestContext.CancellationToken);

        var repo2Url = "test://example-repo-2";
        var repo2Id = GenerateRepositoryId(repo2Url);
        var repo2Path = Path.Combine(_testBasePath, repo2Id);
        CreateLocalTestRepository(repo2Path, repo2Url);

        // Act
        var repositories = await _service.ListRepositoriesAsync();

        // Assert
        Assert.IsNotNull(repositories);
        Assert.AreEqual(2, repositories.Count());

        var repoList = repositories.ToList();
        Assert.IsTrue(repoList.Any(r => r.Id == repo1Id));
        Assert.IsTrue(repoList.Any(r => r.Id == repo2Id));
        Assert.IsTrue(repoList.Any(r => r.Url == repo1Url));
        Assert.IsTrue(repoList.Any(r => r.Url == repo2Url));
    }

    [TestMethod]
    public async Task ListRepositoriesAsync_WithInvalidDirectory_SkipsInvalid()
    {
        // Arrange - Create one valid repository and one invalid directory
        var validUrl = "test://valid-repo";
        var validId = GenerateRepositoryId(validUrl);
        var validPath = Path.Combine(_testBasePath, validId);
        CreateLocalTestRepository(validPath, validUrl);

        // Create an invalid directory (not a git repo)
        var invalidPath = Path.Combine(_testBasePath, "invalid-dir");
        Directory.CreateDirectory(invalidPath);
        File.WriteAllText(Path.Combine(invalidPath, "test.txt"), "not a repo");

        // Act
        var repositories = await _service.ListRepositoriesAsync();

        // Assert
        Assert.IsNotNull(repositories);
        Assert.AreEqual(1, repositories.Count());
        Assert.AreEqual(validId, repositories.First().Id);
    }

    private static void CreateLocalTestRepository(string path, string remoteUrl)
    {
        Directory.CreateDirectory(path);

        Repository.Init(path);

        using var repo = new Repository(path);
        var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
        repo.Config.Set("user.name", "Test User");
        repo.Config.Set("user.email", "test@example.com");

        var testFile = Path.Combine(path, "README.md");
        File.WriteAllText(testFile, "# Test Repository");

        Commands.Stage(repo, "README.md");
        repo.Commit("Initial commit", signature, signature, new CommitOptions());

        // Add remote origin
        repo.Network.Remotes.Add("origin", remoteUrl);
    }

    private static string GenerateRepositoryId(string url)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static void RemoveReadOnlyAttributes(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                return;
            }

            dirInfo.Attributes &= ~FileAttributes.ReadOnly;

            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes &= ~FileAttributes.ReadOnly;
            }

            foreach (var dir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
            {
                dir.Attributes &= ~FileAttributes.ReadOnly;
            }
        }
        catch
        {
            // Best effort - ignore errors
        }
    }

    public TestContext TestContext { get; set; }
}
