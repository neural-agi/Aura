# Aura Technology Stack

> **Status:** Frozen for Phase 1 (Foundation)
>
> This document defines the official technology stack for Aura.
>
> The goal is to eliminate unnecessary technology decisions during implementation and provide a stable engineering foundation.
>
> Technologies should only change if they provide a clear architectural, performance, maintainability, or developer experience advantage.

---

# Philosophy

Aura prioritizes:

- Long-term maintainability
- Native Windows integration
- Performance
- Simplicity
- Reliability
- Excellent developer experience
- Minimal external dependencies
- Mature ecosystems

Technology choices should optimize for the lifetime of the project rather than short-term convenience.

---

# Core Platform

| Component | Selection |
|------------|-----------|
| Language | C# |
| Runtime | .NET 9 |
| Platform | Windows 11 |
| Target Architecture | x64 (ARM64 planned) |

### Rationale

- First-class Windows support
- Excellent tooling
- Mature ecosystem
- Strong performance
- Long-term Microsoft support

---

# UI Framework

| Component | Selection |
|------------|-----------|
| UI Framework | WinUI 3 |
| Windows App SDK | Latest Stable |

### Rationale

Aura is designed as a native Windows experience.

WinUI 3 provides:

- Native controls
- Fluent Design
- Windows App SDK integration
- Long-term Microsoft support
- High-quality desktop experience

Cross-platform support is intentionally not a goal for the MVP.

---

# Architecture

| Component | Selection |
|------------|-----------|
| Pattern | MVVM |
| Overall Architecture | Clean Architecture |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |

### Principles

- Explicit boundaries
- Feature isolation
- Dependency inversion
- High cohesion
- Low coupling

---

# Logging

| Component | Selection |
|------------|-----------|
| Framework | Serilog |

### Rationale

Chosen for:

- Structured logging
- Excellent sinks
- Mature ecosystem
- High performance
- Production diagnostics

---

# Configuration

| Component | Selection |
|------------|-----------|
| Framework | Microsoft.Extensions.Configuration |

Configuration sources may include:

- appsettings.json
- Environment variables
- User settings
- Future plugin configuration

---

# Serialization

| Component | Selection |
|------------|-----------|
| Primary | System.Text.Json |

### Decision

Aura will use **System.Text.Json** as the default serializer.

Advantages:

- Native .NET implementation
- Excellent performance
- Low allocations
- No external dependency
- Continuous improvements from Microsoft

### Exception

Newtonsoft.Json may be introduced **only** when justified by a specific technical requirement, such as:

- Complex polymorphic serialization
- Advanced custom converters
- Legacy compatibility
- Scenarios where System.Text.Json would introduce excessive implementation complexity

It is **not** a default dependency.

---

# Testing

## Test Framework

- xUnit

## Assertions

- FluentAssertions

## Mocking

- NSubstitute

### Testing Philosophy

Aura prefers:

1. Real implementations
2. Fake implementations
3. Mock objects

Mocks should be used only where they provide clear value.

Avoid excessive interaction-based testing.

Behavior should be tested more often than implementation details.

---

# Benchmarking

| Component | Selection |
|------------|-----------|
| Framework | BenchmarkDotNet |

Performance-critical systems should be benchmarked rather than optimized based on assumptions.

---

# Formatting

| Component | Selection |
|------------|-----------|
| Formatter | dotnet format |

Formatting is mandatory.

Developers should not manually enforce formatting rules.

---

# Static Analysis

| Component | Selection |
|------------|-----------|
| Roslyn Analyzers | Enabled |
| Nullable Reference Types | Enabled |
| Warnings As Errors | Enabled where practical |

Code quality should be enforced automatically whenever possible.

---

# Version Control

| Component | Selection |
|------------|-----------|
| Platform | Git |
| Hosting | GitHub |

Development follows a trunk-based workflow using the `main` branch.

---

# Continuous Integration

| Component | Selection |
|------------|-----------|
| CI Platform | GitHub Actions |

Every pull request should automatically perform:

- Restore
- Build
- Static analysis
- Formatting verification
- Unit tests

Future additions may include:

- Benchmark validation
- Security scanning
- Dependency analysis
- Coverage reporting

---

# Dependency Management

Principles:

- Prefer built-in .NET libraries.
- Minimize third-party dependencies.
- Avoid unnecessary abstraction libraries.
- Every dependency should have a clear purpose.

---

# Performance Goals

Aura should:

- Start quickly
- Use memory efficiently
- Minimize background CPU usage
- Avoid unnecessary allocations
- Avoid unnecessary wake-ups
- Remain responsive under load

Performance improvements should be supported by profiling and benchmarks.

---

# Security Principles

- Least privilege
- Secure defaults
- Deterministic behavior
- Explicit permissions
- Privacy-first design

---

# Accessibility

Accessibility is a core engineering requirement.

All user-facing components should support:

- Keyboard navigation
- Screen readers
- High contrast mode
- DPI scaling
- Windows accessibility APIs

Accessibility should never be treated as a post-release feature.

---

# Future Technologies

Potential future additions include:

- MSIX packaging
- Automatic updates
- Plugin SDK
- Native AOT (where appropriate)
- ARM64 optimization

These are outside the scope of Phase 1.

---

# Technologies Not Selected

## Newtonsoft.Json

Not selected as the default serializer.

Reason:

System.Text.Json sufficiently meets Aura's current requirements while reducing external dependencies.

Newtonsoft.Json remains an approved fallback for justified scenarios.

---

## Moq

Not selected.

Reason:

Aura favors behavior-focused testing using real implementations and fakes.

NSubstitute provides a simpler API that aligns well with the project's testing philosophy.

---

## Cross-Platform UI Frameworks

Examples:

- Avalonia
- Uno Platform
- MAUI

Reason:

Aura is intentionally designed as a Windows-native application.

Native Windows integration is prioritized over portability.

---

# Review Policy

Technology choices are considered frozen for Phase 1.

Changes should only be made when they provide a measurable improvement in:

- Performance
- Maintainability
- Developer experience
- Reliability
- Architectural consistency

Technology changes should be deliberate rather than trend-driven.

---

# Final Principle

Technology exists to serve the product.

Aura's architecture, user experience, and engineering principles should drive technology choices—not the other way around.