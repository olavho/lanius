# Lanius

Lanius is a visualization tool for Git repositories.  
It provides interactive views of commit history, branches, merges, and real-time updates.

## Features
- Clone and analyze Git repositories via URL.
- Extract commit metadata (author, time, message, lines added/removed).
- Identify branches, merges, and common ancestors.
- Visualize:
  - Main branch with feature/release branches.
  - Merges into feature branches.
  - Replay mode: animated commit history.
  - Real-time updates when new commits arrive.
- Built with:
  - **C#/.NET** backend (LibGit2Sharp, Rx.NET, SignalR).
  - **JavaScript** frontend (D3.js for visualization).

## Goals
- Provide clear, intuitive Git history visualizations.
- Support both static analysis and live repository monitoring.
- Enable replay and animation of commit activity.

## License
MIT (to be confirmed).
