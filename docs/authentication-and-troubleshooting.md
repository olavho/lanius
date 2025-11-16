# Clone Failed Troubleshooting & Authentication Guide

## Current Issue: "Failed to fetch"

### Quick Diagnosis

The error message "Failed to fetch" when cloning suggests one of these issues:

1. **Network connectivity**
2. **Repository URL format**
3. **Repository accessibility** (private vs public)
4. **LibGit2Sharp configuration**

### Testing the Repository

The repository `https://github.com/MicrosoftDocs/mslearn-microservices-devops-aspnet-core.git` is **public** and should work without authentication.

### Improved Error Handling

**Changes Made**:
- Added `try-catch` for `LibGit2SharpException`
- Clean up partial clones on failure
- Wrap exceptions with user-friendly messages
- Better error propagation to frontend

### Check These First

1. **Is the API running?**
   ```bash
   dotnet run --project src/Lanius.Api
   # Should see: Now listening on: https://localhost:5001
   ```

2. **Can you reach GitHub from the server?**
   ```bash
   # Test from command line where API runs
   git clone https://github.com/MicrosoftDocs/mslearn-microservices-devops-aspnet-core.git test-clone
   ```

3. **Check browser console for actual error message**
   - Open browser DevTools (F12)
   - Check Console tab
   - Check Network tab for failed request
   - Look at response body for detailed error

### Common Issues & Solutions

#### Issue 1: "Repository already exists"
**Symptom**: Trying to clone the same repo twice  
**Solution**: 
- Delete the existing repository first
- Or use a different URL

#### Issue 2: Network timeout
**Symptom**: Long wait then failure  
**Cause**: Large repository, slow connection  
**Solution**:
- Try a smaller repo first: `https://github.com/octocat/Hello-World.git`
- Check firewall/proxy settings

#### Issue 3: SSL/Certificate issues
**Symptom**: "SSL certificate problem"  
**Cause**: Corporate proxy, self-signed cert  
**Solution**: 
- Test with HTTP instead (if available): `http://github.com/...`
- Configure git to skip SSL verification (not recommended for production)

#### Issue 4: Private Repository
**Symptom**: "Repository not found" or "authentication failed"  
**Cause**: Repository is private, requires authentication  
**Solution**: See authentication section below

---

## Authentication for Private Repositories

### Current Status
**MVP**: Only **public repositories** are supported (no authentication)

### Why No Authentication in MVP?

1. **Simplicity**: Focus on core visualization features
2. **Security**: Storing credentials safely requires additional infrastructure
3. **Scope**: MVP targets public repos only

### Future Enhancement: Adding Authentication

If you need private repository support, here's the implementation plan:

#### Option 1: Personal Access Token (PAT)

**UI Changes**:
```html
<input type="text" id="repo-url" placeholder="Repository URL">
<input type="password" id="pat-token" placeholder="Personal Access Token (optional)">
<button id="clone-btn">Clone</button>
```

**Backend Changes**:
```csharp
// CloneRepositoryRequest.cs
public class CloneRepositoryRequest
{
    public required string Url { get; init; }
    public string? PersonalAccessToken { get; init; } // Added
}

// RepositoryService.cs
public async Task<RepositoryInfo> CloneRepositoryAsync(
    string url, 
    string? token = null,
    CancellationToken cancellationToken = default)
{
    var cloneOptions = new CloneOptions
    {
        Checkout = true,
        RecurseSubmodules = false,
        FetchOptions = new FetchOptions
        {
            CredentialsProvider = (url, usernameFromUrl, types) =>
            {
                if (!string.IsNullOrEmpty(token))
                {
                    return new UsernamePasswordCredentials
                    {
                        Username = token,  // GitHub uses token as username
                        Password = string.Empty
                    };
                }
                return new DefaultCredentials(); // Anonymous for public repos
            }
        }
    };

    Repository.Clone(url, localPath, cloneOptions);
}
```

#### Option 2: OAuth Flow (More Secure)

**Flow**:
1. User clicks "Connect GitHub"
2. Redirect to GitHub OAuth
3. User authorizes app
4. GitHub returns token
5. Store token securely (encrypted, per-user)

**Implementation**:
- Use ASP.NET Core Identity
- Store tokens in database (encrypted)
- Pass token to LibGit2Sharp

#### Option 3: SSH Keys

**Requirements**:
- User generates SSH key pair
- Adds public key to GitHub
- Provides private key to Lanius

**Implementation**:
```csharp
FetchOptions = new FetchOptions
{
    CredentialsProvider = (url, usernameFromUrl, types) =>
    {
        return new SshUserKeyCredentials
        {
            Username = "git",
            PublicKey = "path/to/public.key",
            PrivateKey = "path/to/private.key",
            Passphrase = "optional-passphrase"
        };
    }
}
```

---

## Testing with Public Repositories

### Recommended Test Repos

**Small (< 1MB)**:
```
https://github.com/octocat/Hello-World.git
https://github.com/octocat/Spoon-Knife.git
```

**Medium (1-10MB)**:
```
https://github.com/github/gitignore.git
https://github.com/firstcontributions/first-contributions.git
```

**Large (> 10MB)**:
```
https://github.com/MicrosoftDocs/mslearn-microservices-devops-aspnet-core.git
https://github.com/microsoft/terminal.git (Warning: ~500MB)
```

### Testing Steps

1. **Start with smallest repo**:
   ```
   URL: https://github.com/octocat/Hello-World.git
   Expected: Quick success (< 5 seconds)
   ```

2. **Try medium repo**:
   ```
   URL: https://github.com/github/gitignore.git
   Expected: Success in 5-15 seconds
   ```

3. **Try your target repo**:
   ```
   URL: https://github.com/MicrosoftDocs/mslearn-microservices-devops-aspnet-core.git
   Expected: Success in 30-60 seconds (depends on connection)
   ```

---

## Debugging Failed Clones

### Check API Logs

**Look for**:
```
[Information] Cloning repository from URL: https://...
[Warning] Failed to clone repository: https://...
[Error] LibGit2SharpException: ...
```

### Check Browser DevTools

**Network Tab**:
```
POST /api/repository/clone
Status: 400 Bad Request
Response: {
  "error": "CloneFailed",
  "message": "Failed to clone repository: ...",
  "timestamp": "..."
}
```

### Common LibGit2Sharp Errors

| Error Message | Cause | Solution |
|---------------|-------|----------|
| "failed to resolve address" | DNS/network issue | Check internet connection |
| "SSL certificate problem" | Certificate issue | Check firewall/proxy |
| "authentication required" | Private repo | Add authentication (see above) |
| "repository not found" | Wrong URL or private | Verify URL, check public/private |
| "timeout" | Large repo or slow connection | Wait longer or try smaller repo |

---

## Current Limitations (MVP)

### What Works ?
- ? Clone public repositories via HTTPS
- ? Repositories up to reasonable size (~100MB)
- ? GitHub, GitLab, Bitbucket public repos
- ? Error messages propagated to frontend

### What Doesn't Work ?
- ? Private repositories (no authentication)
- ? SSH URLs (git@github.com:...)
- ? Repositories requiring 2FA
- ? Corporate repos behind firewalls
- ? Very large repos (> 500MB may timeout)

---

## Next Steps for Full Auth Support

If authentication is critical for your use case:

1. **Decide on auth method**:
   - PAT (simplest for MVP+)
   - OAuth (most secure)
   - SSH keys (for power users)

2. **Update data model**:
   ```csharp
   public class CloneRepositoryRequest
   {
       public required string Url { get; init; }
       public AuthenticationMethod AuthMethod { get; init; }
       public string? Token { get; init; }
       public string? Username { get; init; }
       public string? Password { get; init; }
   }
   ```

3. **Update LibGit2Sharp calls**:
   - Add `CredentialsProvider` to `CloneOptions`
   - Add `CredentialsProvider` to `FetchOptions`
   - Handle different credential types

4. **Secure credential storage**:
   - Never log credentials
   - Encrypt tokens in database
   - Use ASP.NET Core Data Protection

5. **Update frontend**:
   - Add auth fields to UI
   - Store tokens securely (consider secure storage APIs)
   - Clear tokens on logout

---

## Workaround for Now

If you need to visualize a private repo:

1. **Make a local clone**:
   ```bash
   git clone https://your-private-repo.git /path/to/local/clone
   ```

2. **Point Lanius to local path** (requires code change):
   ```csharp
   // Add method to RepositoryService
   public async Task<RepositoryInfo> LoadLocalRepositoryAsync(string localPath)
   {
       if (!Repository.IsValid(localPath))
           throw new InvalidOperationException("Not a valid Git repository");
       
       // Copy to Lanius storage
       var repoId = GenerateRepositoryId(localPath);
       var targetPath = GetRepositoryPath(repoId);
       
       CopyDirectory(localPath, targetPath);
       
       return await GetRepositoryInfoAsync(repoId);
   }
   ```

---

## Summary

**For the current error**:
1. Check browser console for detailed error
2. Try with `https://github.com/octocat/Hello-World.git` first
3. Check API logs for LibGit2Sharp exceptions
4. Verify network connectivity

**For private repos**:
- Not currently supported in MVP
- Implementation guide provided above
- Workaround: clone locally, then import

**Status**: MVP supports **public repositories only**. Authentication support is a planned enhancement.
