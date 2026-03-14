# Project Standards and Best Practices

This is a monolithic repository containing multiple applications that work together as a unified system.

## Code Quality Standards

### General Principles
- Write clean, maintainable, and self-documenting code
- Follow SOLID principles
- Prefer composition over inheritance
- Write unit tests for business logic
- Document complex algorithms and business rules

### Naming Conventions
- Use meaningful and descriptive names
- Follow language-specific conventions (PascalCase for C#, camelCase for JavaScript/TypeScript)
- Avoid abbreviations unless widely understood
- Use verb-noun pairs for methods (e.g., `getUserById`, `createOrder`)

### Error Handling
- Always handle errors gracefully
- Log errors with sufficient context for debugging
- Use try-catch blocks appropriately
- Return meaningful error messages to clients

### Security
- Never commit secrets, API keys, or credentials
- Use environment variables for configuration
- Validate and sanitize all user inputs
- Implement proper authentication and authorization
- Follow OWASP security guidelines

### Performance
- Optimize database queries
- Use caching where appropriate
- Implement pagination for large datasets
- Minimize API calls
- Use async/await for I/O operations

## Version Control

### Git Workflow
- Use feature branches for development
- Write descriptive commit messages
- Keep commits small and focused
- Squash commits before merging to main
- Use conventional commits format: `type(scope): description`

### Branch Naming
- `feature/description` - New features
- `bugfix/description` - Bug fixes
- `hotfix/description` - Critical production fixes
- `refactor/description` - Code refactoring

### Pull Requests
- Provide clear description of changes
- Link related issues
- Ensure CI/CD passes
- Request reviews from team members
- Address all review comments

## Testing

- Write unit tests for new features
- Maintain test coverage above 70%
- Write integration tests for critical paths
- Test edge cases and error scenarios
- Use meaningful test descriptions


---

# .NET API Development

## Technology Stack

- .NET 8.0
- Entity Framework Core
- ASP.NET Core Web API
- Swagger/OpenAPI

## Project Structure

```
api/
├── Controllers/      # API endpoints
├── Services/         # Business logic
├── Models/          # Data models and DTOs
├── Data/            # EF Core DbContext and migrations
├── Middleware/      # Custom middleware
└── Program.cs       # Application entry point
```

## Common Tasks

### Running the API

```bash
cd api
dotnet run
```

API will be available at `http://localhost:5000`
Swagger UI at `http://localhost:5000/swagger`

### Database Migrations

```bash
# Create a new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# Rollback migration
dotnet ef database update PreviousMigrationName
```

### Adding a New Endpoint

1. Create a controller in `/Controllers`
2. Define request/response DTOs in `/Models`
3. Implement business logic in `/Services`
4. Add any required database entities
5. Update Swagger documentation if needed

### Testing

```bash
dotnet test
```

## Best Practices

- Use async/await for all I/O operations
- Implement proper error handling with try-catch
- Use dependency injection for services
- Validate input using Data Annotations or FluentValidation
- Return appropriate HTTP status codes
- Use DTOs to avoid exposing internal models
- Implement proper logging
