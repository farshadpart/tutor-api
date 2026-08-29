# Tutor API (tutor-api)

ASP.NET Core backend API for the Tutor app — a conversational tutor experience with account management, chat, audio processing, and logging features.

## Table of contents
- [Overview](#overview)
- [Features](#features)
- [Tech stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [Available scripts](#available-scripts)
- [Project structure (high level)](#project-structure-high-level)
- [Configuration](#configuration)
- [Testing & quality](#testing--quality)
- [Publishing](#publishing)
- [Troubleshooting](#troubleshooting)
- [Resources](#resources)

## Overview
The Tutor API backend is an ASP.NET Core service that provides REST endpoints for user authentication, account management, AI chat interactions, audio processing, and logging for the Tutor mobile application.

## Features
- User account management and authentication
- Chat interface for AI tutor interactions
- Audio file upload and processing
- Real-time logging and monitoring
- Redis caching for performance
- PostgreSQL database for persistent data
- Email notifications via MailJet

## Tech stack
- ASP.NET Core (.NET 10.0.x)
- C#
- PostgreSQL (database)
- Redis (caching)
- MailJet (email service)
- xUnit for tests

## Prerequisites
- .NET SDK `10.0.x`
- PostgreSQL
- Redis
- MailJet API credentials

## Quick start
1. Clone the repository:
   git clone https://github.com/farshadpart/tutor-api.git
2. Install dependencies:
   dotnet restore Tutor.Api.sln
3. Configure the environment (see the Configuration section).
4. Run tests:
   dotnet test Tutor.Api.sln
5. Start the development server:
   dotnet run --project Tutor.Api/Tutor.Api.csproj

## Available scripts
(From common dotnet commands)
- dotnet restore Tutor.Api.sln — restore NuGet packages
- dotnet build Tutor.Api.sln — compile the solution
- dotnet test Tutor.Api.sln — run all tests
- dotnet run --project Tutor.Api/Tutor.Api.csproj — start development server on http://localhost:5252
- dotnet publish -c Release — publish for production

## Project structure (high level)
- /Tutor.Api — main API project and entry point
- /Tutor.Api/Controllers — API endpoint controllers
- /Tutor.Api/Services — business logic and service layer
- /Tutor.Api/Models — data models and DTOs
- /Tutor.Api/Data — database context and migrations
- /Tutor.Api/Middleware — custom middleware
- /Tutor.Tests — unit and integration tests
- /.github/workflows — CI/CD pipelines

## Configuration

Development configuration is in `Tutor.Api/appsettings.Development.json`.

Production uses environment variables:

- `TutorConnectionString`
- `RedisConnectionString`
- `MailJetApiKey`
- `MailJetApiSecretKey`

## Testing & quality
- Unit & integration tests: dotnet test Tutor.Api.sln
- Build: dotnet build Tutor.Api.sln
- CI: Consider adding workflows to run tests on push/PR

## Publishing

The publish workflow is:

```text
.github/workflows/publish.yml
```

It runs manually with `workflow_dispatch`, tests the solution, publishes
`Tutor.Api/Tutor.Api.csproj`, and uploads a packaged artifact.

Version and artifact names use a `number.number.number` version from the commit
message. If the commit message does not include one, the workflow uses the
latest git tag with that format. The GitHub run number is appended as the build
number.

```text
Version: <version>.<github-run-number>
Run: Publish Tutor
Name: Tutor <version>.<github-run-number>
Package: Tutor <version>.<github-run-number>.tar.gz
```

## Troubleshooting
- Database connection issues: verify PostgreSQL is running, and the connection string is correct.
- Redis connection errors: ensure Redis is running on the configured port (default: 6379).
- Port already in use: configure in `launchSettings.json` or set the `ASPNETCORE_URLS` environment variable.

## Resources
- ASP.NET Core docs: https://learn.microsoft.com/en-us/aspnet/core
- Entity Framework Core: https://learn.microsoft.com/en-us/ef/core
- PostgreSQL docs: https://www.postgresql.org/docs
- Redis docs: https://redis.io/documentation
- MailJet docs: https://dev.mailjet.com
