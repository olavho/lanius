using System.Reactive.Linq;
using System.Reactive.Subjects;
using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Replay.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Lanius.Business.Replay.Services;

/// <summary>
/// Service for replaying commit history using Rx.NET observables.
/// </summary>
public class ReplayService(IServiceProvider serviceProvider) : IReplayService
{
    private readonly Dictionary<string, ReplaySessionContext> _sessions = [];
    private readonly Lock _lock = new();

    public async Task<ReplaySession> StartReplayAsync(
        string repositoryId,
        ReplayOptions options,
        CancellationToken cancellationToken = default)
    {
        // Create a scope to get ICommitAnalyzer
        using var scope = serviceProvider.CreateScope();
        var commitAnalyzer = scope.ServiceProvider.GetRequiredService<ICommitAnalyzer>();

        IReadOnlyList<Commit> commits;

        if (!string.IsNullOrWhiteSpace(options.BranchFilter))
        {
            // Fetch commits for the specific branch — LibGit2Sharp resolves "origin/main" natively
            commits = await commitAnalyzer.GetCommitsSinceAsync(
                repositoryId,
                options.BranchFilter,
                sinceCommitSha: null,
                cancellationToken);

            // Apply date range if specified
            if (options.StartDate.HasValue || options.EndDate.HasValue)
            {
                commits = [.. commits.Where(c =>
                    (!options.StartDate.HasValue || c.Timestamp >= options.StartDate.Value) &&
                    (!options.EndDate.HasValue || c.Timestamp <= options.EndDate.Value))];
            }
        }
        else
        {
            commits = await commitAnalyzer.GetCommitsChronologicallyAsync(
                repositoryId,
                options.StartDate,
                options.EndDate,
                cancellationToken);
        }

        // Replay must stream oldest → newest so commits appear left-to-right on the timeline
        commits = [.. commits.OrderBy(c => c.Timestamp)];

        // Skip commits before StartIndex (used for scrub/seek)
        if (options.StartIndex > 0 && options.StartIndex < commits.Count)
            commits = [.. commits.Skip(options.StartIndex)];

        var sessionId = Guid.NewGuid().ToString("N");
        var session = new ReplaySession
        {
            SessionId = sessionId,
            RepositoryId = repositoryId,
            State = ReplayState.Playing,
            Options = options,
            TotalCommits = commits.Count,
            CurrentIndex = 0,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Create observable stream
        var subject = new ReplaySubject<Commit>();
        var pauseSubject = new BehaviorSubject<bool>(false);
        var speedSubject = new BehaviorSubject<double>(options.Speed);

        var context = new ReplaySessionContext
        {
            Session = session,
            Commits = commits,
            Subject = subject,
            PauseSubject = pauseSubject,
            SpeedSubject = speedSubject,
            CurrentIndex = 0
        };

        lock (_lock)
        {
            _sessions[sessionId] = context;
        }

        // Start replay in background
        _ = Task.Run(() => RunReplay(sessionId, cancellationToken), cancellationToken);

        return session;
    }

    public void PauseReplay(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var context))
            {
                context.PauseSubject.OnNext(true);
                context.Session = context.Session with { State = ReplayState.Paused };
            }
        }
    }

    public void ResumeReplay(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var context))
            {
                context.PauseSubject.OnNext(false);
                context.Session = context.Session with { State = ReplayState.Playing };
            }
        }
    }

    public void StopReplay(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var context))
            {
                context.CancellationTokenSource.Cancel();
                context.Subject.OnCompleted();
                context.Session = context.Session with
                {
                    State = ReplayState.Cancelled,
                    CompletedAt = DateTimeOffset.UtcNow
                };
            }
        }
    }

    public void SetSpeed(string sessionId, double speed)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var context))
            {
                context.SpeedSubject.OnNext(speed);
                var newOptions = context.Session.Options with { Speed = speed };
                context.Session = context.Session with { Options = newOptions };
            }
        }
    }

    public ReplaySession? GetSession(string sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(sessionId, out var context) ? context.Session : null;
        }
    }

    public IObservable<Commit> GetCommitStream(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var context))
            {
                return context.Subject.AsObservable();
            }
            return Observable.Empty<Commit>();
        }
    }

    private async Task RunReplay(string sessionId, CancellationToken cancellationToken)
    {
        ReplaySessionContext? context;
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out context))
            {
                return;
            }
        }

        try
        {
            var commits = context.Commits;
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                context.CancellationTokenSource.Token).Token;

            for (int i = 0; i < commits.Count; i++)
            {
                if (linkedToken.IsCancellationRequested)
                {
                    break;
                }

                // Wait while paused
                while (context.PauseSubject.Value)
                {
                    await Task.Delay(100, linkedToken);
                }

                var commit = commits[i];

                // Calculate delay based on speed
                // Speed 1.0 = 1 commit per second
                // Speed 2.0 = 2 commits per second (500ms delay)
                // Speed 0.5 = 1 commit per 2 seconds (2000ms delay)
                var currentSpeed = context.SpeedSubject.Value;
                var delayMs = (int)(1000.0 / currentSpeed);

                // Emit commit
                context.Subject.OnNext(commit);

                // Update session state
                lock (_lock)
                {
                    context.CurrentIndex = i + 1;
                    context.Session = context.Session with { CurrentIndex = i + 1 };
                }

                // Wait before next commit (except for last one)
                if (i < commits.Count - 1)
                {
                    await Task.Delay(delayMs, linkedToken);
                }
            }

            // Complete the stream
            context.Subject.OnCompleted();

            lock (_lock)
            {
                context.Session = context.Session with
                {
                    State = ReplayState.Completed,
                    CompletedAt = DateTimeOffset.UtcNow
                };
            }
        }
        catch (OperationCanceledException)
        {
            // Replay was cancelled
            context.Subject.OnCompleted();
        }
        catch (Exception ex)
        {
            context.Subject.OnError(ex);

            lock (_lock)
            {
                context.Session = context.Session with
                {
                    State = ReplayState.Cancelled,
                    CompletedAt = DateTimeOffset.UtcNow
                };
            }
        }
    }

    private class ReplaySessionContext
    {
        public required ReplaySession Session { get; set; }
        public required IReadOnlyList<Commit> Commits { get; init; }
        public required ReplaySubject<Commit> Subject { get; init; }
        public required BehaviorSubject<bool> PauseSubject { get; init; }
        public required BehaviorSubject<double> SpeedSubject { get; init; }
        public CancellationTokenSource CancellationTokenSource { get; } = new();
        public int CurrentIndex { get; set; }
    }
}
