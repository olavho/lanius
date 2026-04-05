using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Lanius.Business.Test.Layout;

[TestClass]
public class CalendarLayoutEngineTests
{
    private Mock<ICommitAnalyzer> _mockCommitAnalyzer = null!;
    private Mock<IBranchAnalyzer> _mockBranchAnalyzer = null!;
    private Mock<ILogger<CalendarLayoutEngine>> _mockLogger = null!;
    private CalendarLayoutEngine _layoutEngine = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockCommitAnalyzer = new Mock<ICommitAnalyzer>();
        _mockBranchAnalyzer = new Mock<IBranchAnalyzer>();
        _mockLogger = new Mock<ILogger<CalendarLayoutEngine>>();

        _layoutEngine = new CalendarLayoutEngine(
            _mockCommitAnalyzer.Object,
            _mockBranchAnalyzer.Object,
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
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<(int processed, int total)>?>()))
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
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<(int processed, int total)>?>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions
        {
            Mode = LayoutMode.Calendar,
            Granularity = CalendarGranularity.Month, // Explicit: span < 150 days would auto-select Day
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
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<(int processed, int total)>?>()))
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
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<(int processed, int total)>?>()))
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

    #region GroupCommitsByDay Tests

    [TestMethod]
    public void GroupCommitsByDay_EmptyList_ReturnsEmptyGroups()
    {
        var groups = _layoutEngine.GroupCommitsByDay([]);
        Assert.IsEmpty(groups);
    }

    [TestMethod]
    public void GroupCommitsByDay_SameDayCommits_ReturnsSingleGroup()
    {
        var commits = new List<Commit>
        {
            CreateCommit("a", new DateTimeOffset(2026, 4, 5, 8, 0, 0, TimeSpan.Zero)),
            CreateCommit("b", new DateTimeOffset(2026, 4, 5, 14, 0, 0, TimeSpan.Zero)),
            CreateCommit("c", new DateTimeOffset(2026, 4, 5, 22, 0, 0, TimeSpan.Zero))
        };

        var groups = _layoutEngine.GroupCommitsByDay(commits);

        Assert.HasCount(1, groups);
        Assert.AreEqual(3, groups[0].CommitCount);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodStart);
    }

    [TestMethod]
    public void GroupCommitsByDay_DifferentDays_ReturnsMultipleGroupsInOrder()
    {
        var commits = new List<Commit>
        {
            CreateCommit("d3", new DateTimeOffset(2026, 4, 5, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("d1", new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("d2", new DateTimeOffset(2026, 4, 3, 10, 0, 0, TimeSpan.Zero))
        };

        var groups = _layoutEngine.GroupCommitsByDay(commits);

        Assert.HasCount(3, groups);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodStart);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 3, 0, 0, 0, TimeSpan.Zero), groups[1].PeriodStart);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero), groups[2].PeriodStart);
    }

    #endregion

    #region GroupCommitsByWeek Tests

    [TestMethod]
    public void GroupCommitsByWeek_EmptyList_ReturnsEmptyGroups()
    {
        var groups = _layoutEngine.GroupCommitsByWeek([]);
        Assert.IsEmpty(groups);
    }

    [TestMethod]
    public void GroupCommitsByWeek_SameWeekCommits_ReturnsSingleGroup()
    {
        // Mon Apr 6 - Sun Apr 12, 2026
        var commits = new List<Commit>
        {
            CreateCommit("a", new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero)),  // Mon
            CreateCommit("b", new DateTimeOffset(2026, 4, 9, 10, 0, 0, TimeSpan.Zero)),  // Thu
            CreateCommit("c", new DateTimeOffset(2026, 4, 12, 10, 0, 0, TimeSpan.Zero))  // Sun
        };

        var groups = _layoutEngine.GroupCommitsByWeek(commits);

        Assert.HasCount(1, groups);
        Assert.AreEqual(3, groups[0].CommitCount);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 6, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodStart, "Week starts on Monday");
        Assert.AreEqual(new DateTimeOffset(2026, 4, 12, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodEnd, "Week ends on Sunday");
    }

    [TestMethod]
    public void GroupCommitsByWeek_WeekBoundary_SundayAndMondayInDifferentWeeks()
    {
        // Sun Apr 12 is end of week1; Mon Apr 13 starts week2
        var commits = new List<Commit>
        {
            CreateCommit("w1", new DateTimeOffset(2026, 4, 12, 23, 0, 0, TimeSpan.Zero)), // Sun → week1
            CreateCommit("w2", new DateTimeOffset(2026, 4, 13, 1, 0, 0, TimeSpan.Zero))  // Mon → week2
        };

        var groups = _layoutEngine.GroupCommitsByWeek(commits);

        Assert.HasCount(2, groups, "Sunday and Monday should be in different weeks");
        Assert.AreEqual(new DateTimeOffset(2026, 4, 6, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodStart, "Week 1 starts Mon Apr 6");
        Assert.AreEqual(new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero), groups[1].PeriodStart, "Week 2 starts Mon Apr 13");
    }

    #endregion

    #region GroupCommitsByYear Tests

    [TestMethod]
    public void GroupCommitsByYear_EmptyList_ReturnsEmptyGroups()
    {
        var groups = _layoutEngine.GroupCommitsByYear([]);
        Assert.IsEmpty(groups);
    }

    [TestMethod]
    public void GroupCommitsByYear_SameYearCommits_ReturnsSingleGroup()
    {
        var commits = new List<Commit>
        {
            CreateCommit("a", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            CreateCommit("b", new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)),
            CreateCommit("c", new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero))
        };

        var groups = _layoutEngine.GroupCommitsByYear(commits);

        Assert.HasCount(1, groups);
        Assert.AreEqual(3, groups[0].CommitCount);
        Assert.AreEqual(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodStart);
        Assert.AreEqual(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), groups[0].PeriodEnd);
    }

    [TestMethod]
    public void GroupCommitsByYear_MultipleYears_ReturnsGroupsInOrder()
    {
        var commits = new List<Commit>
        {
            CreateCommit("y26a", new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero)),
            CreateCommit("y24",  new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            CreateCommit("y25",  new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero)),
            CreateCommit("y26b", new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero))
        };

        var groups = _layoutEngine.GroupCommitsByYear(commits);

        Assert.HasCount(3, groups);
        Assert.AreEqual(2024, groups[0].PeriodStart.Year);
        Assert.AreEqual(1, groups[0].CommitCount);
        Assert.AreEqual(2025, groups[1].PeriodStart.Year);
        Assert.AreEqual(1, groups[1].CommitCount);
        Assert.AreEqual(2026, groups[2].PeriodStart.Year);
        Assert.AreEqual(2, groups[2].CommitCount);
    }

    #endregion

    #region Granularity Dispatch Tests

    [TestMethod]
    public async Task CalculateLayoutAsync_DayGranularity_ProducesOneDayNodePerDay()
    {
        // Arrange - 3 commits on different days in the same month
        var commits = new List<Commit>
        {
            CreateCommit("d1", new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("d2", new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("d3", new DateTimeOffset(2026, 4, 3, 10, 0, 0, TimeSpan.Zero))
        };

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(), null, null, It.IsAny<CancellationToken>(), It.IsAny<IProgress<(int processed, int total)>?>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions
        {
            Mode = LayoutMode.Calendar,
            Granularity = CalendarGranularity.Day,
            CanvasWidth = 1000,
            CanvasHeight = 600
        };

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync("test-repo", options, cancellationToken: TestContext.CancellationToken);

        // Assert - 3 different days → 3 nodes (not 1 month node)
        Assert.HasCount(3, result.Nodes, "Day granularity should produce one node per day");
        Assert.AreEqual("period-2026-04-01", result.Nodes[0].CommitId);
        Assert.AreEqual("period-2026-04-02", result.Nodes[1].CommitId);
        Assert.AreEqual("period-2026-04-03", result.Nodes[2].CommitId);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_WeekGranularity_ProducesOneNodePerWeek()
    {
        // Arrange - 4 commits across 2 weeks
        var commits = new List<Commit>
        {
            // Week 1: Mon Apr 6 – Sun Apr 12, 2026
            CreateCommit("w1a", new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("w1b", new DateTimeOffset(2026, 4, 8, 10, 0, 0, TimeSpan.Zero)),
            // Week 2: Mon Apr 13 – Sun Apr 19, 2026
            CreateCommit("w2a", new DateTimeOffset(2026, 4, 13, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("w2b", new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero))
        };

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(), null, null, It.IsAny<CancellationToken>(), It.IsAny<IProgress<(int processed, int total)>?>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions
        {
            Mode = LayoutMode.Calendar,
            Granularity = CalendarGranularity.Week,
            CanvasWidth = 1000,
            CanvasHeight = 600
        };

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync("test-repo", options, cancellationToken: TestContext.CancellationToken);

        // Assert - 2 weeks → 2 nodes
        Assert.HasCount(2, result.Nodes, "Week granularity should produce one node per week");
        Assert.AreEqual("period-2026-04-06", result.Nodes[0].CommitId, "First week starts Mon Apr 6");
        Assert.AreEqual("period-2026-04-13", result.Nodes[1].CommitId, "Second week starts Mon Apr 13");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_YearGranularity_ProducesOneNodePerYear()
    {
        // Arrange - 4 commits across 3 years
        var commits = new List<Commit>
        {
            CreateCommit("y24a", new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("y24b", new DateTimeOffset(2024, 11, 1, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("y25a", new DateTimeOffset(2025, 3, 20, 10, 0, 0, TimeSpan.Zero)),
            CreateCommit("y26a", new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero))
        };

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(), null, null, It.IsAny<CancellationToken>(), It.IsAny<IProgress<(int processed, int total)>?>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions
        {
            Mode = LayoutMode.Calendar,
            Granularity = CalendarGranularity.Year,
            CanvasWidth = 1000,
            CanvasHeight = 600
        };

        // Act
        var result = await _layoutEngine.CalculateLayoutAsync("test-repo", options, cancellationToken: TestContext.CancellationToken);

        // Assert - 3 years → 3 nodes
        Assert.HasCount(3, result.Nodes, "Year granularity should produce one node per year");
        Assert.AreEqual("period-2024", result.Nodes[0].CommitId);
        Assert.AreEqual("period-2025", result.Nodes[1].CommitId);
        Assert.AreEqual("period-2026", result.Nodes[2].CommitId);
    }

    #endregion

    #region Large Repository Tests (> 1000 commits)

    [TestMethod]
    public void GroupCommitsByMonth_1200CommitsOver3Years_Produces36Groups()
    {
        var commits = GenerateCommits(1200, new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(3 * 365));

        var groups = _layoutEngine.GroupCommitsByMonth(commits);

        Assert.HasCount(36, groups, "3 years × 12 months = 36 groups");
        Assert.IsTrue(groups.All(g => g.CommitCount > 0), "All groups should have commits");
        Assert.AreEqual(1200, groups.Sum(g => g.CommitCount), "All commits should be accounted for");
    }

    [TestMethod]
    public void GroupCommitsByDay_1000CommitsEachOnDifferentDay_Produces1000Groups()
    {
        // 1000 commits, 1 per day (interval = 999/999 = 1 day exactly)
        var commits = GenerateCommits(1000, new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(999));

        var groups = _layoutEngine.GroupCommitsByDay(commits);

        Assert.HasCount(1000, groups, "1 commit per day = 1000 day groups");
        Assert.IsTrue(groups.All(g => g.CommitCount == 1), "Each group should have exactly 1 commit");
        Assert.AreEqual(1000, groups.Sum(g => g.CommitCount));
    }

    [TestMethod]
    public void GroupCommitsByWeek_1000CommitsOver500Weeks_ProducesApprox500Groups()
    {
        // 1000 commits over 500 weeks (~3.5 days per commit → ~2 per week)
        var start = new DateTimeOffset(2014, 1, 6, 0, 0, 0, TimeSpan.Zero); // Monday
        var commits = GenerateCommits(1000, start, TimeSpan.FromDays(500 * 7));

        var groups = _layoutEngine.GroupCommitsByWeek(commits);

        Assert.IsTrue(groups.Count >= 490 && groups.Count <= 510,
            $"~500 week groups expected, got {groups.Count}");
        Assert.IsTrue(groups.All(g => g.PeriodStart.DayOfWeek == DayOfWeek.Monday),
            "All week groups should start on Monday");
        Assert.AreEqual(1000, groups.Sum(g => g.CommitCount));
    }

    [TestMethod]
    public void GroupCommitsByYear_2000CommitsOver10Years_Produces10Groups()
    {
        var commits = GenerateCommits(2000, new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(10 * 365));

        var groups = _layoutEngine.GroupCommitsByYear(commits);

        Assert.HasCount(10, groups, "10 years = 10 year groups");
        Assert.AreEqual(2000, groups.Sum(g => g.CommitCount));
        Assert.IsTrue(groups.All(g => g.PeriodStart.Month == 1 && g.PeriodStart.Day == 1), "PeriodStart = Jan 1");
        Assert.IsTrue(groups.All(g => g.PeriodEnd.Month == 12 && g.PeriodEnd.Day == 31), "PeriodEnd = Dec 31");
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_1200CommitsOver10Years_AutoSelectsYearGranularity()
    {
        var commits = GenerateCommits(1200, new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(10 * 365));

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(), null, null, It.IsAny<CancellationToken>(), It.IsAny<IProgress<(int processed, int total)>?>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions { Mode = LayoutMode.Calendar, CanvasWidth = 2000, CanvasHeight = 600 };

        var result = await _layoutEngine.CalculateLayoutAsync("test-repo", options, cancellationToken: TestContext.CancellationToken);

        // > 5 years span → auto-selected Year granularity → 10 nodes
        Assert.HasCount(10, result.Nodes, "10 years = 10 year nodes");
        Assert.AreEqual(1200, result.TotalCommits);
    }

    [TestMethod]
    public async Task CalculateLayoutAsync_500CommitsOver100Days_AutoSelectsDayGranularity()
    {
        // 500 commits over 100 days → < 150 days → auto Day granularity → ~101 day nodes
        var commits = GenerateCommits(500, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(100));

        _mockCommitAnalyzer
            .Setup(x => x.GetCommitsChronologicallyAsync(
                It.IsAny<string>(), null, null, It.IsAny<CancellationToken>(), It.IsAny<IProgress<(int processed, int total)>?>()))
            .ReturnsAsync(commits);

        var options = new LayoutOptions { Mode = LayoutMode.Calendar, CanvasWidth = 2000, CanvasHeight = 600 };

        var result = await _layoutEngine.CalculateLayoutAsync("test-repo", options, cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Nodes.Count >= 99 && result.Nodes.Count <= 101,
            $"~101 day nodes expected for 100-day span, got {result.Nodes.Count}");
        Assert.AreEqual(500, result.TotalCommits);
    }

    #endregion

    #region Helper Methods

    private static IReadOnlyList<Commit> GenerateCommits(int count, DateTimeOffset start, TimeSpan totalSpan)
    {
        var commits = new List<Commit>(count);
        var intervalTicks = count > 1 ? totalSpan.Ticks / (count - 1) : 0;
        for (var i = 0; i < count; i++)
        {
            commits.Add(CreateCommit($"commit-{i:D5}", start.AddTicks(intervalTicks * i)));
        }
        return commits;
    }

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
