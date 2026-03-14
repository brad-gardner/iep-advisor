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

# Monorepo Architecture

This project uses a monolithic repository structure with multiple applications.

## Structure Overview

```
project-root/
├── api/          # .NET API Backend
├── web/          # ReactJS Web Application
├── mobile/       # React Native (Expo) Mobile App
├── .github/      # CI/CD Workflows
└── docker-compose.yml
```

## Workspace Management

All applications are managed from the root `package.json` using npm workspaces or scripts.

### Root-Level Commands
- `npm run dev` - Start all services in development mode
- `npm run build` - Build all applications
- `npm run test` - Run tests for all applications
- `npm run lint` - Lint all applications

## Shared Resources

### Environment Variables
- Root `.env` file for shared configuration
- Stack-specific `.env` files in each directory
- Never commit `.env` files to version control

### Docker Services
- Database services defined in `docker-compose.yml`
- APM services (if using Elastic APM)
- Start with: `npm run docker:up`
- Stop with: `npm run docker:down`

## Development Workflow

1. Start Docker services first
2. Run migrations (if using API)
3. Start development servers
4. Make changes in appropriate directories
5. Test changes before committing
