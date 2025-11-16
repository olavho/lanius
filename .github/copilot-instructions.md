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

### Technology Stack
- **.NET 10** - Target framework for all projects
- **Razor Pages** - Web framework (prioritize Razor Pages patterns over Blazor or MVC)

### Project Organization
- `src/Lanius.Web/` - Razor Pages web application
- `src/Lanius.Business/` - Business logic layer
- `src/Lanius.Business.Test/` - Unit tests
- `docs/` - All project documentation
- `docs/chat/` - Copilot analysis and summaries (chronologically organized)

## Code Conventions

### General Guidelines
- Follow standard C# naming conventions
- Use meaningful, descriptive names
- Keep methods focused and single-purpose
- Write unit tests for business logic

### Razor Pages Specific
- Use page models for logic, keep `.cshtml` files focused on UI
- Follow the convention: `PageName.cshtml` and `PageName.cshtml.cs`
- Use tag helpers over HTML helpers
- Leverage model binding and validation attributes

## When Creating Documentation
1. Determine if the content is analysis/chat ? save to `docs/chat/` with dated naming
2. Use the next available serial number for the current date
3. Keep content structured with clear headings
4. Be concise - get to the point quickly
