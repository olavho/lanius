# Quick Testing Guide

## 🚀 Start Application

```powershell
cd C:\local\private\Lanius
dotnet run --project src/Lanius.Api
```

Open browser: `https://localhost:7032`

---

## ✅ What to Test

### 1. Load Small Repository (245 commits, 11 branches)
**Expected**: < 3 seconds, all branches visible

### 2. Load Large Repository (5331 commits, 78 branches)
**Expected**: < 10 seconds, steady progress, all branches visible

---

## 👀 What to Look For

### Visual Elements
- ✅ Horizontal lines for each branch
- ✅ Colored boxes at start of each branch
- ✅ Hover over box shows branch name
- ✅ Commits positioned on branch lines
- ✅ No overlapping commits

### Performance
- ✅ Progress shows "Analyzing branch hierarchy..." (10-20%)
- ✅ Progress shows "Loading commits: branch-name (X/Y)" (20-60%)
- ✅ Load completes in < 3s (small) or < 10s (large)

### Logging
**Visual Studio Output Window** → Show output from: **Lanius.Api**
```
Loading commits for 11 branches using branch hierarchy optimization
Loaded 245 commits for branch main
Loaded 15 commits for branch feature/xyz
Commit loading complete. Total unique commits: 245
```

**Browser Console (F12)**:
```
Branch info extracted: 11 branches
=== renderLayout COMPLETE ===
```

---

## 🐛 Troubleshooting

### No branches visible
- Check browser console for JavaScript errors
- Check VS Output window for backend errors

### Stuck at X% progress
- Check VS Output window - should see "Loading commits for branch..." messages
- If truly stuck (no new messages for 30+ seconds), report as bug

### Performance still slow
- Check VS Output window for commit counts per branch
- Expected: 100-500 commits per branch (not 5000+)
- If seeing high counts, hierarchy detection may have failed

---

## 📊 Success Metrics

### Before This Fix
- 245 commits: **15 seconds** ❌
- 5331 commits: **Stuck at 10%** ❌
- Only 1 branch visible ❌

### After This Fix (Expected)
- 245 commits: **< 3 seconds** ✅
- 5331 commits: **< 10 seconds** ✅
- All branches visible ✅
- Branch indicators visible ✅
- Detailed progress/logging ✅

---

## 📝 Report Results

If testing successful:
- Note actual load times
- Note commit counts from logs
- Verify all visual elements present

If issues found:
- Screenshot of issue
- Copy/paste browser console errors
- Copy/paste VS Output window logs
- Note which repository and commit count

---

**Quick Reference**: See `docs/chat/2026-04-05-11-implementation-complete.md` for full details
