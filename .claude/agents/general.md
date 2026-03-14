# General Development Agent

This agent provides general guidance for working in this monolithic repository.

## Repository Structure

This is a monolithic repository containing multiple applications:
- `/api` - .NET API backend
- `/web` - ReactJS web frontend
- `/mobile` - React Native (Expo) mobile app

## Development Workflow

### Making Changes

1. **Work in the appropriate directory** - Navigate to the relevant stack folder (api, web, or mobile)
2. **Follow stack-specific conventions** - Each stack has its own best practices (see stack-specific agents)
3. **Test your changes** - Run tests in the specific stack directory
4. **Commit with clear messages** - Use conventional commit format

### Running Commands

```bash
# From root directory
npm run dev           # Run all services
npm run dev:api       # Run API only
npm run dev:web       # Run web only
npm run dev:mobile    # Run mobile only

# From stack directory (e.g., cd api)
dotnet run            # Run .NET API directly
dotnet test           # Run API tests
```

### Code Organization

- **Shared logic** - Keep shared constants and utilities at root level if used across stacks
- **Stack-specific code** - Keep within the respective stack directory
- **Database migrations** - Manage in the `/api` directory
- **Environment variables** - Use `.env` files (never commit secrets!)

### Best Practices

1. **Never commit secrets** - Use environment variables for API keys, connection strings, etc.
2. **Write tests** - Aim for meaningful test coverage
3. **Document non-obvious code** - Add comments for complex business logic
4. **Keep dependencies updated** - Regularly update npm/NuGet packages
5. **Review before committing** - Use `git diff` to review your changes

### Common Tasks

**Adding a new API endpoint:**
1. Create controller in `/api/Controllers`
2. Add service logic in `/api/Services`
3. Add data models in `/api/Models`
4. Update Swagger documentation
5. Write integration tests

**Adding a new web component:**
1. Create component in `/web/src/components`
2. Add styles (CSS-in-JS or separate file)
3. Export from appropriate index.ts
4. Write component tests

**Adding a new mobile screen:**
1. Create screen in `/mobile/src/screens`
2. Add navigation route
3. Create necessary hooks/services
4. Test on iOS and Android

## Getting Help

- Check stack-specific agents for detailed guidance
- Review existing code for patterns
- Check documentation in `/docs` folder
- Run tests to ensure changes work correctly

## Monitoring and Debugging

- API logs: Check console output or APM dashboard
- Web errors: Check browser console and APM
- Mobile crashes: Check Sentry dashboard
- Database: Use SQL Server Management Studio or Azure Data Studio

## Deployment

- CI/CD pipelines are configured in `.github/workflows`
- Builds run automatically on push to main/develop
- Review pipeline output for errors before merging PRs

---

For stack-specific guidance, see:
- `api/.claude/agents/api-development.md`
- `web/.claude/agents/web-development.md`
- `mobile/.claude/agents/mobile-development.md`
