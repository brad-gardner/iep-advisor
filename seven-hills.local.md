---
review_agents: [dotnet-reviewer, react-reviewer, react-async-reviewer, typescript-reviewer, code-simplicity-reviewer, security-agent-smith, performance-oracle]
plan_review_agents: [architecture-strategist, code-simplicity-reviewer, performance-oracle]
---

# Review Context

- Full-stack monorepo: `/api` (.NET 9 + EF Core), `/web` (React 19 + Vite + TypeScript + Tailwind)
- Custom JWT auth (not ASP.NET Identity) — review auth code with that in mind
- Claude API used for IEP document analysis — watch for prompt injection in user-uploaded content
- Generic repository pattern + service result wrappers on backend
- Feature-based folder structure on frontend
