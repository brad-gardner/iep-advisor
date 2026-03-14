# iep-assistant

A multi-stack monolithic repository containing integrated applications.

## Project Structure

This repository contains the following components:

```
iep-assistant/
├── api/          # .NET API Backend
├── web/          # ReactJS Web Application
├── mobile/       # React Native (Expo) Mobile Application
└── README.md     # This file
```

## Getting Started

### Prerequisites

- Node.js 18+
- .NET SDK 8.0+
- Docker Desktop (for local database)

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd iep-assistant

# Install dependencies
npm install

# Start Docker services (database)
npm run docker:up

# Run all services in development mode
npm run dev
```

### Development

```bash
# Run individual services
npm run dev:api      # Start .NET API
npm run dev:web      # Start React web app
npm run dev:mobile   # Start Expo mobile app

# Build for production
npm run build

# Run tests
npm run test

# Lint code
npm run lint
```

## Project Information

- **Organization**: sevenhillstechnology
- **Database**: {{database}}
- **APM Provider**: elastic

## Documentation

- [Architecture Documentation](./ARCHITECTURE.md) - System architecture and design decisions
- [Contributing Guide](./CONTRIBUTING.md) - How to contribute to this project
- API Documentation - Available at `http://localhost:5000/swagger` when API is running

## Technologies

### API
- .NET 8.0
- Entity Framework Core
- {{database}}

### Web
- React with TypeScript
- Vite
- elastic

### Mobile
- React Native
- Expo
- Sentry

## License

MIT

---

Generated with [SHT CLI](https://github.com/yourcompany/sht-cli)
