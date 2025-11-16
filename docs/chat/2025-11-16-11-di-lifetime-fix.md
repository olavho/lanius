# DI Lifetime Mismatch Fix

## Problem

**Exception**: `AggregateException` on startup
```
Cannot consume scoped service 'Lanius.Business.Services.ICommitAnalyzer' 
from singleton 'Lanius.Business.Services.IReplayService'
```

## Root Cause

**Dependency Injection Lifetime Mismatch**:
- `ReplayService` registered as **Singleton**
- `ICommitAnalyzer` registered as **Scoped**
- ? Singletons cannot consume scoped services directly

## Why This Happened

`ReplayService` needs to be a singleton because:
- Manages long-lived replay sessions across requests
- Maintains session state in memory
- Observable subscriptions span multiple HTTP requests

`ICommitAnalyzer` needs to be scoped because:
- Depends on scoped services like repositories
- Per-request lifecycle
- Proper disposal of resources

## Solution

**Use Service Locator Pattern for Scoped Dependencies**

Instead of injecting `ICommitAnalyzer` directly into `ReplayService`, inject `IServiceProvider` and create scopes when needed:

### Before (? Broken)
```csharp
public class ReplayService : IReplayService
{
    private readonly ICommitAnalyzer _commitAnalyzer;
    
    public ReplayService(ICommitAnalyzer commitAnalyzer)
    {
        _commitAnalyzer = commitAnalyzer; // ? Cannot inject scoped into singleton
    }
}
```

### After (? Fixed)
```csharp
public class ReplayService : IReplayService
{
    private readonly IServiceProvider _serviceProvider;
    
    public ReplayService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task<ReplaySession> StartReplayAsync(...)
    {
        // ? Create scope when needed
        using var scope = _serviceProvider.CreateScope();
        var commitAnalyzer = scope.ServiceProvider.GetRequiredService<ICommitAnalyzer>();
        
        var commits = await commitAnalyzer.GetCommitsChronologicallyAsync(...);
        // ... use commits
    }
}
```

## Changes Made

### 1. ReplayService.cs
```csharp
using Microsoft.Extensions.DependencyInjection; // Added

public ReplayService(IServiceProvider serviceProvider)
{
    _serviceProvider = serviceProvider;
}

public async Task<ReplaySession> StartReplayAsync(...)
{
    using var scope = _serviceProvider.CreateScope();
    var commitAnalyzer = scope.ServiceProvider.GetRequiredService<ICommitAnalyzer>();
    // ... use scoped service
}
```

### 2. Program.cs
```csharp
// Keep as Singleton - correct for long-lived sessions
builder.Services.AddSingleton<IReplayService, ReplayService>();
builder.Services.AddSingleton<ReplaySignalRBridge>();

// Scoped services
builder.Services.AddScoped<ICommitAnalyzer, CommitAnalyzer>();
builder.Services.AddScoped<IBranchAnalyzer, BranchAnalyzer>();
```

## Key Points

? **ReplayService remains Singleton** - Required for session management  
? **Uses IServiceProvider** - Creates scopes for scoped dependencies  
? **Proper disposal** - `using var scope` ensures cleanup  
? **No lifetime conflicts** - Scoped services resolved from scopes  

## Alternative Approaches (Not Used)

### ? Make ReplayService Scoped
- **Problem**: Sessions would be lost between requests
- **Problem**: Multiple instances for same session

### ? Make ICommitAnalyzer Singleton
- **Problem**: Would need to make all dependencies singleton
- **Problem**: Potential resource leaks with LibGit2Sharp

### ? Service Locator Pattern (Used)
- **Benefit**: Singleton can consume scoped services safely
- **Benefit**: Explicit control over scope lifetime
- **Trade-off**: Slight violation of pure DI, but appropriate here

## Verification

```bash
dotnet build
# Result: Build succeeded. 0 Error(s)

dotnet run --project src/Lanius.Api
# Result: Application starts without exceptions ?
```

## Related Patterns

### When to Use Service Locator
- **Singleton needs scoped service**: Use `IServiceProvider`
- **Factory pattern**: Create instances on demand
- **Plugin architecture**: Resolve services dynamically
- **Long-lived services**: Background services, hubs

### When NOT to Use
- **Normal controllers**: Use constructor injection
- **Simple services**: Direct dependency injection
- **Short-lived services**: Use appropriate lifetime

## Documentation Updated

This fix ensures the replay mode functionality works correctly while maintaining proper DI lifetime semantics.

---

**Status**: ? **Fixed and Verified**  
**Build**: ? **Successful**  
**Runtime**: ? **No exceptions**
