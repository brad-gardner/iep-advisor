---
review_agents: [dotnet-reviewer, react-reviewer, react-async-reviewer, bruno-reviewer, typescript-reviewer, code-simplicity-reviewer, agent-smith, performance-oracle]
worker_agents: [dotnet-worker, react-worker, bruno-worker]
plan_review_agents: [architecture-strategist, code-simplicity-reviewer, performance-oracle]
base_branch: main


model_preferences:
    sht-work:
      claude: sonnet
      codex: gpt-5.6-terra
    sht-review:
      claude:
        light: sonnet
        standard: inherit
        deep: inherit
      codex:
        light: gpt-5.6-terra
        standard: gpt-5.6-sol
        deep: gpt-5.6-sol
    sht-plan:
      claude: sonnet
      codex: gpt-5.6-terra
    sht-storm:
      claude: sonnet
      codex: gpt-5.6-terra
    sht-compound:
      claude: haiku
      codex: gpt-5.6-luna
    sht-docs:
      claude: sonnet
      codex: gpt-5.6-terra
    sht-validate:
      claude: sonnet
      codex: gpt-5.6-terra

base_branch: main
---
# Review Context

- Full-stack monorepo: `/api` (.NET 9 + EF Core), `/web` (React 19 + Vite + TypeScript + Tailwind)
- Custom JWT auth (not ASP.NET Identity) — review auth code with that in mind
- Claude API used for IEP document analysis — watch for prompt injection in user-uploaded content
- Generic repository pattern + service result wrappers on backend
- Feature-based folder structure on frontend
