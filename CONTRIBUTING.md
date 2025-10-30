# Contributing to WAiSA

Thank you for your interest in contributing to WAiSA (Workplace AI System Assistant)! This document provides guidelines and best practices for contributing to the project.

## 🤝 Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for all contributors.

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Git
- Azure CLI (for cloud-related contributions)
- A GitHub account

### Setting Up Your Development Environment

1. **Fork the repository** on GitHub

2. **Clone your fork:**
   ```bash
   git clone https://github.com/YOUR_USERNAME/waisa.git
   cd waisa
   ```

3. **Add upstream remote:**
   ```bash
   git remote add upstream https://github.com/ORIGINAL_OWNER/waisa.git
   ```

4. **Install dependencies:**
   ```bash
   dotnet restore
   ```

5. **Run tests to verify setup:**
   ```bash
   dotnet test
   ```

## 📋 How to Contribute

### Reporting Bugs

Before creating a bug report, please:

1. **Check existing issues** to avoid duplicates
2. **Verify the bug** in the latest version

When creating a bug report, include:

- **Clear title** describing the issue
- **Steps to reproduce** the bug
- **Expected behavior** vs actual behavior
- **Environment details** (OS, .NET version, Azure services)
- **Screenshots or logs** if applicable
- **Possible solution** if you have one

### Suggesting Enhancements

Enhancement suggestions are welcome! Please include:

- **Clear description** of the enhancement
- **Use cases** and benefits
- **Potential implementation** approach
- **Impact assessment** (breaking changes, performance)

### Pull Requests

1. **Create a feature branch:**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following our coding standards

3. **Add tests** for new functionality

4. **Update documentation** as needed

5. **Commit with clear messages:**
   ```bash
   git commit -m "feat: add confidence scoring algorithm"
   ```

6. **Push to your fork:**
   ```bash
   git push origin feature/your-feature-name
   ```

7. **Create a Pull Request** on GitHub

## 📝 Coding Standards

### C# / .NET Guidelines

- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use **meaningful variable and method names**
- Add **XML documentation** for public APIs
- Keep methods **focused and small** (single responsibility)
- Use **async/await** for I/O operations
- Handle **exceptions appropriately**

### Code Style

```csharp
// ✅ Good
public async Task<QueryResult> ProcessQueryAsync(
    string query,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(query))
    {
        throw new ArgumentException("Query cannot be empty", nameof(query));
    }

    var result = await _orchestrator.RouteQueryAsync(query, cancellationToken);
    return result;
}

// ❌ Bad
public QueryResult ProcessQuery(string q)
{
    var r = _orchestrator.RouteQuery(q);
    return r;
}
```

### Testing Requirements

- **Unit tests** for business logic
- **Integration tests** for external dependencies
- **Minimum 80% code coverage** for new code
- Use **meaningful test names** describing the scenario

```csharp
[Fact]
public async Task ProcessQueryAsync_WithEmptyQuery_ThrowsArgumentException()
{
    // Arrange
    var service = new QueryService();

    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(
        () => service.ProcessQueryAsync("", CancellationToken.None));
}
```

### Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `style:` - Code style changes (formatting, no logic change)
- `refactor:` - Code refactoring
- `test:` - Adding or updating tests
- `chore:` - Maintenance tasks

Examples:
```
feat: add Redis caching for query results
fix: resolve race condition in confidence scoring
docs: update API specification with new endpoints
test: add integration tests for LLM provider
```

## 🏗️ Project Structure

```
backend/
├── WAiSA.API/              # API controllers and configuration
├── WAiSA.Infrastructure/   # External service integrations
├── WAiSA.Shared/          # Shared models and utilities
└── WAiSA.API.Tests/       # Test projects
```

### Adding New Features

When adding new features:

1. **Discuss** in an issue first for major changes
2. **Update** relevant documentation
3. **Add tests** with good coverage
4. **Update** OpenAPI specification if needed
5. **Consider** backward compatibility

## 🧪 Testing

### Running Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test backend/WAiSA.API.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific test
dotnet test --filter "FullyQualifiedName~QueryServiceTests"
```

### Test Categories

- **Unit Tests** - Fast, isolated, no external dependencies
- **Integration Tests** - Test with real external services
- **E2E Tests** - End-to-end scenarios

## 📚 Documentation

When changing functionality:

1. **Update** inline code comments
2. **Update** XML documentation
3. **Update** README.md if needed
4. **Update** architecture-diagrams.md for architectural changes
5. **Update** api-specification.yaml for API changes

## 🔍 Code Review Process

All submissions require code review:

1. **Automated checks** must pass (build, tests, linting)
2. **At least one approval** from maintainers
3. **Address feedback** promptly and professionally
4. **Resolve conflicts** with main branch
5. **Squash commits** if requested

### Review Checklist

- [ ] Code follows project conventions
- [ ] Tests pass and have good coverage
- [ ] Documentation is updated
- [ ] No breaking changes (or properly documented)
- [ ] Performance impact considered
- [ ] Security implications reviewed

## 🐛 Debugging Tips

### Local Development

```bash
# Enable detailed logging
export ASPNETCORE_ENVIRONMENT=Development

# Run with verbose output
dotnet run --project backend/WAiSA.API --verbosity detailed
```

### Azure Deployment

```bash
# View logs
az webapp log tail --resource-group waisa-poc-rg --name your-app-name

# Download logs
az webapp log download --resource-group waisa-poc-rg --name your-app-name
```

## 🎯 Areas Needing Contribution

We're particularly interested in contributions for:

- 🧪 **Test coverage** improvements
- 📖 **Documentation** enhancements
- 🐛 **Bug fixes** and stability improvements
- 🚀 **Performance** optimizations
- 🌐 **Internationalization** support
- ♿ **Accessibility** improvements
- 🔒 **Security** enhancements

## 📞 Getting Help

If you need help:

- **GitHub Discussions** - For questions and discussions
- **GitHub Issues** - For bugs and feature requests
- **Pull Request Comments** - For PR-specific questions

## 🏆 Recognition

Contributors will be:

- Listed in the project's contributors section
- Mentioned in release notes for significant contributions
- Eligible to become maintainers based on contributions

## 📅 Release Process

1. **Feature freeze** for release candidate
2. **Testing period** (1 week)
3. **Documentation update**
4. **Version tagging** following semantic versioning
5. **Release notes** published
6. **Deployment** to staging then production

## ⚖️ License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to WAiSA! Your efforts help make this project better for everyone. 🎉
