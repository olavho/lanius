using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Lanius.Business.Test.Layout;

[TestClass]
public class ConstellationLayoutEngineTests
{
    private Mock<ICommitAnalyzer> _mockCommitAnalyzer = null!;
    private Mock<IBranchAnalyzer> _mockBranchAnalyzer = null!;
    private Mock<ILogger<ConstellationLayoutEngine>> _mockLogger = null!;
    private ConstellationLayoutEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockCommitAnalyzer = new Mock<ICommitAnalyzer>();
        _mockBranchAnalyzer = new Mock<IBranchAnalyzer>();
        _mockLogger = new Mock<ILogger<ConstellationLayoutEngine>>();

        _engine = new ConstellationLayoutEngine(
            _mockCommitAnalyzer.Object,
            _mockBranchAnalyzer.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_NoBranches_ReturnsEmptyResult()
    {
        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync("repo", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Branch>());

        var result = await _engine.CalculateLayoutAsync("repo", new LayoutOptions());

        Assert.AreEqual(LayoutMode.Constellation, result.Mode);
        Assert.AreEqual(0, result.Nodes.Count);
        Assert.AreEqual(0, result.Edges.Count);
        Assert.AreEqual(0, result.TotalBranches);
        Assert.AreEqual(0, result.TotalCommits);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_WithCommits_ProducesConstellationNodesAndEdges()
    {
        var branch = new Branch
        {
            Name = "origin/main",
            FullName = "refs/remotes/origin/main",
            TipSha = "c2",
            IsRemote = true
        };

        var c1 = CreateCommit("c1", DateTimeOffset.UtcNow.AddMinutes(-10), [], ["origin/main"]);
        var c2 = CreateCommit("c2", DateTimeOffset.UtcNow, ["c1"], ["origin/main"]);

        _mockBranchAnalyzer
            .Setup(x => x.GetBranchesAsync("repo", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { branch });

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsAsync("repo", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { c1, c2 });

        var result = await _engine.CalculateLayoutAsync("repo", new LayoutOptions { Mode = LayoutMode.Constellation });

        Assert.AreEqual(LayoutMode.Constellation, result.Mode);
        Assert.AreEqual(2, result.Nodes.Count);
        Assert.AreEqual(1, result.Edges.Count);
        Assert.AreEqual("c1", result.Edges[0].FromCommitId);
        Assert.AreEqual("c2", result.Edges[0].ToCommitId);
        Assert.IsTrue(result.Width > 0);
        Assert.IsTrue(result.Height > 0);
    }

    private static Commit CreateCommit(string sha, DateTimeOffset ts, IReadOnlyList<string> parents, IReadOnlyList<string> branches)
    {
        return new Commit
        {
            Sha = sha,
            Author = "Test",
            AuthorEmail = "test@example.com",
            Committer = "Test",
            CommitterEmail = "test@example.com",
            CommitterTimestamp = ts,
            Timestamp = ts,
            Message = $"Commit {sha}",
            ParentShas = parents,
            Branches = branches,
            Stats = new DiffStats
            {
                LinesAdded = 2,
                LinesRemoved = 1,
                FilesChanged = 1
            }
        };
    }
}
