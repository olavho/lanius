using LibGit2Sharp;

namespace Lanius.Business.Test.Services;

/// <summary>
/// Minimal tests to understand LibGit2Sharp authentication patterns.
/// </summary>
[TestClass]
public class LibGit2SharpAuthTests
{

    [TestCleanup]
    public void Cleanup()
    {
        CleanupDirectory(@"c:\temp\test-clone-1");
    }

    [TestMethod]
    public void Test1_DefaultCredentials()
    {
        var url = "https://azuredevops.finods.com/SLS2/sls-tool/_git/sls-tool-web";
        var localDir = @"c:\temp\test-clone-1";

        // Try using DefaultCredentials (Windows auth)
        var options = new CloneOptions { };
        options.FetchOptions.CredentialsProvider = (_url, _user, _cred) =>
                new DefaultCredentials();

        try
        {
            var result = Repository.Clone(url, localDir, options);
            Assert.IsNotNull(result);
            Console.WriteLine("✓ SUCCESS: DefaultCredentials works");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAILED: {ex.Message}");
            throw;
        }
        finally
        {
            CleanupDirectory(localDir);
        }
    }

    private void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
                Directory.Delete(path, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}