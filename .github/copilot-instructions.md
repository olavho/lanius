# GitHub Copilot Instructions for Lanius Project

## Documentation Conventions

### Chat Summaries and Analysis
- **Location**: All analysis, summaries, and deliberations created by Copilot must be saved under `docs/chat/`
- **Naming Convention**: Use the format `YYYY-MM-DD-##-description.md`
  - **Date**: ISO format (YYYY-MM-DD) for chronological sorting
  - **Serial Number**: Two-digit sequence (01, 02, 03, etc.) for multiple entries on the same day
  - **Description**: Brief kebab-case description of the topic
  - **Examples**:
    - `2024-01-15-01-architecture-decisions.md`
    - `2024-01-15-02-api-design-review.md`
    - `2024-02-03-01-performance-analysis.md`

### Communication Style
- **Keep all documentation and communication brief and to the point**
- Avoid verbose explanations unless specifically requested
- Use bullet points and concise language
- Get to the actionable information quickly

## Project Structure

### Solution File
- **.slnx** - XML-based Visual Studio solution file (root directory)
- Use `dotnet sln` commands to manage projects in the solution

### Technology Stack
- **.NET 10** - Target framework for all projects
- **ASP.NET Core Web API** - API layer with SignalR support
- **LibGit2Sharp** - Git repository interaction
- **Rx.NET** - Reactive Extensions for replay mode
- **D3.js** - Frontend visualization library

### Project Organization
- `src/Lanius.Api/` - Web API project (controllers, SignalR hubs, configuration)
- `src/Lanius.Business/` - **Business logic layer** (Git analysis, repository services, domain models)
- `src/Lanius.Business.Test/` - **Unit tests** for business logic
- `src/Lanius.Web/` - **Frontend web application** (HTML, CSS, JavaScript, D3.js visualizations)
  - `wwwroot/` - Static web files served by ASP.NET Core
- `docs/` - All project documentation
- `docs/chat/` - Copilot analysis and summaries (chronologically organized)
- `docs/plans/` - Implementation plans and roadmaps

## Architecture Guidelines

### Separation of Concerns

#### **Lanius.Api** - Presentation/API Layer
- REST API controllers (`/api/repo`, `/api/commits`, `/api/branches`)
- SignalR hubs for real-time updates
- Request/response DTOs
- API configuration and middleware
- Static file hosting (serves Lanius.Web wwwroot)
- Authentication/authorization (when implemented)
- **NO business logic** - delegate to `Lanius.Business`

#### **Lanius.Business** - Domain/Business Layer
- **Git repository operations** (clone, fetch, pull via LibGit2Sharp)
- **Commit analysis** (metadata extraction, diff statistics)
- **Branch analysis** (detection, divergence calculation, common ancestors)
- **Replay services** (Rx.NET observable streams)
- **Repository monitoring** (polling for updates)
- Domain models (`Commit`, `Branch`, `DiffStats`, etc.)
- Service interfaces and implementations
- **All analytics logic belongs here**

#### **Lanius.Business.Test** - Test Layer
- **Comprehensive unit tests** for all business logic
- Test coverage for:
  - Repository operations
  - Commit metadata extraction
  - Branch analysis algorithms
  - Diff statistics calculation
  - Replay stream behavior
- Use mocking frameworks (e.g., Moq, NSubstitute) for external dependencies
- Aim for high code coverage of business logic

#### **Lanius.Web** - Frontend Layer
- **Static HTML, CSS, JavaScript files**
- Located in `wwwroot/` following ASP.NET Core conventions
- D3.js visualizations for commit graphs
- SignalR client for real-time updates
- Minimalist UI design
- **NO server-side code** - pure static files served by Lanius.Api

### Code Conventions

#### General Guidelines
- Follow standard C# naming conventions
- Use meaningful, descriptive names
- Keep methods focused and single-purpose
- Async/await for I/O operations (Git operations, file system)
- Dependency injection for all services

#### Testing Requirements
- **Every new business logic class must have corresponding unit tests**
- Test public methods and interfaces
- Cover edge cases and error conditions
- Use AAA pattern (Arrange, Act, Assert)
- Name tests clearly: `MethodName_Scenario_ExpectedBehavior`

#### Git Operations
- Always use LibGit2Sharp for Git interactions
- Handle repository operations asynchronously where possible
- Proper resource disposal (`using` statements for `Repository`)
- Error handling for network failures, authentication issues

#### Reactive Extensions (Rx.NET)
- Use observables for time-based commit streams
- Implement proper disposal of subscriptions
- Handle backpressure for large commit histories

#### Frontend (Lanius.Web)
- All static files must be placed in `wwwroot/`
- Use D3.js for data visualizations
- SignalR client for real-time communication with API
- Minimize external dependencies (load from CDN)
- Follow existing minimalist design patterns

## When Creating Documentation
1. Determine if the content is analysis/chat ? save to `docs/chat/` with dated naming
2. Use the next available serial number for the current date
3. Keep content structured with clear headings
4. Be concise - get to the point quickly

## Project-Specific Context

### Lanius Purpose
Git repository visualization tool with:
- Branch overview graphs
- Commit replay/animation
- Real-time monitoring
- Support for large repositories (20K+ commits, 300+ branches)

### MVP Scope
- Single-user, self-hosted
- Public repositories only
- Disk-based storage with incremental updates
- 5-second polling for real-time updates
- Focus: main, project/*, release/* branches
