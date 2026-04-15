---
name: C# WPF Code Review
description: A custom Antigravity skill that performs structured code reviews of C# source code, analyzing naming conventions, performance pitfalls, security vulnerabilities, and .NET architecture best practices.
---
# Review C# Code for Best Practices

Perform a structured code review of C# source code. Identify issues related to naming conventions, performance, security, readability, and .NET/MVVM best practices. Produce a prioritized report with actionable suggestions.

## Gather Context

Before reviewing the codebase, determine the context:

1. **Code to review** — the target C# files, classes, or architecture layers (e.g. ViewModels, Services).
2. **Review focus** — what to prioritize:
   - `all` (default) — full review across all categories
   - `architecture` — UI coupling, MVVM adherence, dependency injection
   - `naming` — naming conventions only
   - `performance` — performance and memory allocations
   - `security` — security vulnerabilities only
   - `readability` — clarity, structure, and maintainability

## Review Categories

### 1. Structure and Design

- Single Responsibility Principle — does the class do too many things?
- File length and class complexity
- Proper MVVM separation — no UI/Window specifics locked tightly inside ViewModels.
- Dependency injection — is `new` used where DI should be?
- Proper abstractions for services (e.g., `IDialogService`).

### 2. Naming Conventions

Verify against C# Coding Conventions:

- Classes, records, structs: PascalCase (e.g. `UserManager`)
- Interfaces: `I` + PascalCase (e.g. `IUserRepository`)
- Methods, Properties: PascalCase
- Private fields: `_camelCase`
- Async methods: PascalCase + `Async` suffix

### 3. Performance

- String concatenation in loops → suggest `StringBuilder` or string interpolations.
- Unnecessary allocations: boxing, repeated `ToList()`.
- Threading issues: `async void` (should be `async Task` except for event handlers), deadlock risks from `Task.Result` or `Task.Wait()`.

### 4. Security

- Input validation — public method parameters not validated.
- Proper null safety — missing null checks or inconsistent nullable annotations.
- Unsafe exception handling — catching `System.Exception` carelessly.

### 5. Readability and Maintainability

- Methods that are too long.
- Deeply nested conditionals (suggest early returns or guard clauses).
- Magic numbers/strings.

## Rules

**Do:**

- Explain **why** each issue matters.
- Be specific about locations (class name, method name, and lines).
- Sort issues by severity: Critical -> Warning -> Info.
- Acknowledge good patterns; cite at least 1-2 things done well.

**Don't:**

- Be vague ("consider improving performance"). Give an actionable fix snippet.
- Overwhelm with noise. Group minor rules into categories.

## Report Format

```markdown
# Code Review: {Project/Component Name}

> Focus: {focus} | Reviewed: {date}

## Summary
{One-paragraph overview summarizing architectural integrity and issue count by severity.}

## Issues

| # | Severity | Location | Issue | Why It Matters | Suggestion |
|---|----------|----------|-------|----------------|------------|
| 1 | Critical | `Class.Method:L12` | {...} | {...}          | {...}      |
| 2 | Warning  | `Class.Property`   | {...} | {...}          | {...}      |

## What's Done Well
- {Positive pattern observed}

## Recommendations
1. **[Critical]** {...}
2. **[Warning]** {...}
```
