using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Lanius.Business.Models;
using Lanius.Business.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Lanius.Business.Test.Layout;

[TestClass]
public class CalendarLayoutEngineTests
{
    private Mock<ICommitAnalyzer> _mockCommitAnalyzer = null!;
    private Mock<IBranchAnalyzer> _mockBranchAnalyzer = null!;
    private Mock<IRepositoryService> _mockRepositoryService = null!;
    private Mock<ILogger<CalendarLayoutEngine>> _mockLogger = null!;
    private CalendarLayoutEngine _layoutEngine = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockCommitAnalyzer = new Mock<ICommitAnalyzer>();
        _mockBranchAnalyzer = new Mock<IBranchAnalyzer>();
        _mockRepositoryService = new Mock<IRepositoryService>();
        _mockLogger = new Mock<ILogger<CalendarLayoutEngine>>();

        _layoutEngine = new CalendarLayoutEngine(
            _mockCommitAnalyzer.Object,
            _mockBranchAnalyzer.Object,
            _mockRepositoryService.Object,
            _mockLogger.Object
        );
    }

    #region GroupCommitsByMonth Tests

    [TestMethod]
    public void GroupCommitsByMonth_EmptyList_ReturnsEmptyGroups()
    {
        // Arrange
        var commits = new List<Commit>();

        // Act
        var groups = _layoutEngine.GroupCommitsByMonth(commits);

        // Assert
        Assert.IsEmpty(groups, "Empty commit list should produce no groups");
    }

    [TestMethod]
    public void GroupCommitsByMonth_SingleCommit_ReturnsSingleGroup()
    {
        // Arrange
        var commits = new List<Commit>
        {
            CreateCommit("commit1", new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero))
        };

        // Act
        var groups = _layoutEngine.GroupCommitsByMonth(commits);

        // Assert
        Assert.HasCount(1, groups, "Single commit should create one group");
        Assert.AreEqual(1, groups[0].CommitCount, "Group should contain 1 commit");
        Assert.AreEqual(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodStart);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodEnd);
    }

    [TestMethod]
    public void GroupCommitsByMonth_MultipleCommitsSameMonth_ReturnsSingleGroup()
    {
        // Arrange
        var commits = new List<Commit>
        {
            CreateCommit("commit1", new DateTimeOffset(2026, 4, 5, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("commit2", new DateTimeOffset(2026, 4, 15, 14, 30, 0, TimeSpan.Zero)),
            CreateCommit("commit3", new DateTimeOffset(2026, 4, 28, 18, 45, 0, TimeSpan.Zero))
        };

        // Act
        var groups = _layoutEngine.GroupCommitsByMonth(commits);

        // Assert
        Assert.HasCount(1, groups, "Commits in same month should create one group");
        Assert.AreEqual(3, groups[0].CommitCount, "Group should contain 3 commits");
        Assert.Contains("commit1", groups[0].CommitIds);
        Assert.Contains("commit2", groups[0].CommitIds);
        Assert.Contains("commit3", groups[0].CommitIds);
    }

    [TestMethod]
    public void GroupCommitsByMonth_MultiplleMonths_ReturnsMultipleGroups()
    {
        // Arrange
        var commits = new List<Commit>
        {
            CreateCommit("jan1", new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("jan2", new DateTimeOffset(2026, 1, 20, 14, 0, 0, TimeSpan.Zero)),
            CreateCommit("feb1", new DateTimeOffset(2026, 2, 5, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("mar1", new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("mar2", new DateTimeOffset(2026, 3, 20, 14, 0, 0, TimeSpan.Zero)),
            CreateCommit("mar3", new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero))
        };

        // Act
        var groups = _layoutEngine.GroupCommitsByMonth(commits);

        // Assert
        Assert.HasCount(3, groups, "Should create 3 groups for 3 months");

        // January
        Assert.AreEqual(2, groups[0].CommitCount);
        Assert.AreEqual(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodStart);

        // February
        Assert.AreEqual(1, groups[1].CommitCount);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), groups[1].PeriodStart);

        // March
        Assert.AreEqual(3, groups[2].CommitCount);
        Assert.AreEqual(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), groups[2].PeriodStart);
    }

    [TestMethod]
    public void GroupCommitsByMonth_ChronologicalOrder_GroupsAreSorted()
    {
        // Arrange - commits out of order
        var commits = new List<Commit>
        {
            CreateCommit("mar", new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("jan", new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("feb", new DateTimeOffset(2026, 2, 5, 10, 0, 0, TimeSpan.Zero))
        };

        // Act
        var groups = _layoutEngine.GroupCommitsByMonth(commits);

        // Assert
        Assert.HasCount(3, groups, "Should create 3 groups for 3 months");
        Assert.AreEqual(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodStart, "First group should be January");
        Assert.AreEqual(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), groups[1].PeriodStart, "Second group should be February");
        Assert.AreEqual(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), groups[2].PeriodStart, "Third group should be March");
    }

    #endregion

    #region CalculateNodeRadius Tests

    [TestMethod]
    public void CalculateNodeRadius_ZeroCommits_ReturnsMinRadius()
    {
        // Act
        var radius = CalendarLayoutEngine.CalculateNodeRadius(0, 100);

        // Assert
        Assert.AreEqual(4.0, radius, "Zero commits should return min radius (4px)");
    }

    [TestMethod]
    public void CalculateNodeRadius_MaxCommits_ReturnsMaxRadius()
    {
        // Act
        var radius = CalendarLayoutEngine.CalculateNodeRadius(100, 100);

        // Assert
        Assert.AreEqual(20.0, radius, "Max commits should return max radius (20px)");
    }

    [TestMethod]
    public void CalculateNodeRadius_HalfMaxCommits_ReturnsMidRangeRadius()
    {
        // Act - logarithmic scaling means 50 commits != 50% radius
        var radius = CalendarLayoutEngine.CalculateNodeRadius(50, 100);

        // Assert
        Assert.IsGreaterThan(4.0, radius, "Radius should be greater than min");
        Assert.IsLessThan(20.0, radius, "Radius should be less than max");
        Assert.IsGreaterThan(15.0, radius, "With log scaling, 50% commits should be >75% radius");
    }

    [TestMethod]
    public void CalculateNodeRadius_LogarithmicScaling_DistributesWell()
    {
        // Arrange - test logarithmic distribution
        int maxCommits = 1000;

        var radius10 = CalendarLayoutEngine.CalculateNodeRadius(10, maxCommits);
        var radius100 = CalendarLayoutEngine.CalculateNodeRadius(100, maxCommits);
        var radius1000 = CalendarLayoutEngine.CalculateNodeRadius(1000, maxCommits);

        // Assert - logarithmic scaling means equal multiplicative steps produce similar radius increases
        var step1 = radius100 - radius10;  // 10x increase (10 → 100)
        var step2 = radius1000 - radius100; // 10x increase (100 → 1000)

        Assert.IsLessThan(2.0, Math.Abs(step1 - step2), "Logarithmic scaling should produce similar radius increases for equal multiplicative steps");
    }

    #endregion

    #region CalculateNodePositions Tests

    [TestMethod]
    public void CalculateNodePositions_EmptyGroups_ReturnsEmptyNodes()
    {
        // Arrange
        var groups = new List<PeriodGroup>();

        // Act
        var nodes = CalendarLayoutEngine.CalculateNodePositions(groups, 1000);

        // Assert
        Assert.HasCount(0, nodes, "Empty groups should produce no nodes");
    }

    [TestMethod]
    public void CalculateNodePositions_SingleGroup_CentersNode()
    {
        // Arrange
        var groups = new List<PeriodGroup>
        {
            new() {
                PeriodStart = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero),
                CommitCount = 10,
                CommitIds = [.. new List<string> { "commit1" }]
            }
        };

        // Act
        var nodes = CalendarLayoutEngine.CalculateNodePositions(groups, 1000);

        // Assert
        Assert.HasCount(1, nodes);
        Assert.AreEqual(50.0, nodes[0].X, "Single node should be at left margin (50px)");
        Assert.AreEqual(300.0, nodes[0].Y, "Node should be at center Y (300px)");
    }

    [TestMethod]
    public void CalculateNodePositions_MultipleGroups_DistributesEvenly()
    {
        // Arrange
        var groups = new List<PeriodGroup>
        {
            new() {
                PeriodStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                CommitCount = 10,
                CommitIds = [.. new List<string> { "jan" }]
            },
            new() {
                PeriodStart = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero),
                CommitCount = 20,
                CommitIds = [.. new List<string> { "feb" }]
            },
            new() {
                PeriodStart = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
                CommitCount = 15,
                CommitIds = [.. new List<string> { "mar" }]
            }
        };

        // Act
        var nodes = CalendarLayoutEngine.CalculateNodePositions(groups, 1000);

        // Assert
        Assert.HasCount(3, nodes);

        // Verify even spacing (marginX=50, usableWidth=900, spacing=450)
        Assert.AreEqual(50.0, nodes[0].X, "First node at left margin");
        Assert.AreEqual(500.0, nodes[1].X, "Second node at center (50 + 450)");
        Assert.AreEqual(950.0, nodes[2].X, "Third node at right margin (50 + 900)");

        // All same Y (single row)
        Assert.IsTrue(nodes.All(n => n.Y == 300.0), "All nodes should be at Y=300");
    }

    [TestMethod]
    public void CalculateNodePositions_NodeMetadata_ContainsPeriodInfo()
    {
        // Arrange
        var groups = new List<PeriodGroup>
        {
            new() {
                PeriodStart = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero),
                CommitCount = 42,
                CommitIds = [.. new List<string> { "commit1", "commit2" }]
            }
        };

        // Act
        var nodes = CalendarLayoutEngine.CalculateNodePositions(groups, 1000);

        // Assert
        Assert.HasCount(1, nodes);
        var node = nodes[0];

        Assert.AreEqual("period-2026-04", node.CommitId);
        Assert.AreEqual("all", node.BranchName);
        Assert.Contains("2026", node.Message ?? string.Empty, $"Expected message to contain '2026', but got: {node.Message}");
        Assert.Contains("42 commit", node.Message ?? string.Empty, $"Expected message to contain '42 commit', but got: {node.Message}");
    }

    [TestMethod]
    public void CalculateNodePositions_SignificanceFlag_TopActivity()
    {
        // Arrange - max commits is 100, significant threshold is 70% = 70 commits
        var groups = new List<PeriodGroup>
        {
            new() {
                PeriodStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                CommitCount = 100, // Max
                CommitIds = [.. new List<string> { "jan" }]
            },
            new() {
                PeriodStart = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero),
                CommitCount = 75, // Above threshold (75 > 70)
                CommitIds = [.. new List<string> { "feb" }]
            },
            new() {
                PeriodStart = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
                CommitCount = 50, // Below threshold (50 < 70)
                CommitIds = [.. new List<string> { "mar" }]
            }
        };

        // Act
        var nodes = CalendarLayoutEngine.CalculateNodePositions(groups, 1000);

        // Assert
        Assert.IsTrue(nodes[0].IsSignificant, "Max commits (100) should be significant");
        Assert.IsTrue(nodes[1].IsSignificant, "75 commits (>70% of max) should be significant");
        Assert.IsFalse(nodes[2].IsSignificant, "50 commits (<70% of max) should not be significant");
    }

    #endregion

    #region CalculateLayoutAsync Integration Tests

    [TestMethod]
    public async Task CalculateLayoutAsync_EmptyRepository_ReturnsEmptyLayout()
    {
        // Arrange
        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var options = new LayoutOptions
        {
            Mode = LayoutMode.Calendar,
            CanvasWidth = 1000,
            CanvasHeight = 600
        };

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync("test-repo", options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(LayoutMode.Calendar, result.Mode);
        Assert.HasCount(0, result.Nodes);
        Assert.HasCount(0, result.Edges);
        Assert.AreEqual(0, result.TotalCommits);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_ValidCommits_ReturnsCalendarLayout()
    {
        // Arrange
        var commits = new List<Commit>
        {
            CreateCommit("jan1", new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("jan2", new DateTimeOffset(2026, 1, 20, 14, 0, 0, TimeSpan.Zero)),
            CreateCommit("feb1", new DateTimeOffset(2026, 2, 5, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("mar1", new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero))
        };

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions
        {
            Mode = LayoutMode.Calendar,
            CanvasWidth = 1000,
            CanvasHeight = 600
        };

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync("test-repo", options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(LayoutMode.Calendar, result.Mode);
        Assert.HasCount(3, result.Nodes, "Should create 3 nodes for 3 months");
        Assert.HasCount(0, result.Edges, "Calendar view has no edges in Phase 4a");
        Assert.AreEqual(4, result.TotalCommits);
        Assert.AreEqual(1000, result.Width);
        Assert.AreEqual(600, result.Height);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_ProgressReporting_ReportsSteps()
    {
        // Arrange
        var commits = new List<Commit>
        {
            CreateCommit("commit1", new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero))
        };

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions
        {
            Mode = LayoutMode.Calendar,
            CanvasWidth = 1000,
            CanvasHeight = 600
        };

        var progressReports = new List<LayoutProgress>();
        var progress = new SynchronousProgress<LayoutProgress>(p => progressReports.Add(p));

        // Act
        await _layoutEngine.CalculateLayoutAsync("test-repo", options, progress, TestContext.CancellationToken);

        // Assert
        Assert.IsGreaterThanOrEqualTo(3, progressReports.Count, "Should report at least 3 progress steps");
        Assert.IsTrue(progressReports.Any(p => p.Operation.Contains("Loading commits")));
        Assert.IsTrue(progressReports.Any(p => p.Operation.Contains("Grouping commits")));
        Assert.IsTrue(progressReports.Any(p => p.Operation.Contains("Layout complete")));
        Assert.AreEqual(100, progressReports.Last().Percentage, "Final report should be 100%");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_GranularityOverride_UsesManualGranularity()
    {
        // Arrange - date range < 150 days would auto-select Day, but we override to Month
        var commits = new List<Commit>
        {
            CreateCommit("commit1", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("commit2", new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero))
        };

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions
        {
            Mode = LayoutMode.Calendar,
            Granularity = CalendarGranularity.Month, // Manual override
            CanvasWidth = 1000,
            CanvasHeight = 600
        };

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync("test-repo", options, cancellationToken: TestContext.CancellationToken);

        // Assert
        Assert.HasCount(2, result.Nodes, "Should group by month (2 months)");
        Assert.AreEqual("period-2026-01", result.Nodes[0].CommitId);
        Assert.AreEqual("period-2026-02", result.Nodes[1].CommitId);
    }

    #endregion

    #region Helper Methods

    private static Commit CreateCommit(string sha, DateTimeOffset timestamp)
    {
        return new Commit
        {
            Sha = sha,
            Message = $"Commit {sha}",
            Author = "Test Author",
            AuthorEmail = "test@example.com",
            Timestamp = timestamp,
            Branches = [.. new List<string> { "origin/main" }],
            ParentShas = [.. new List<string>()]
        };
    }

    public TestContext TestContext { get; set; }

    #endregion
}
