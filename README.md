# Tutor API

Tutor API is an ASP.NET Core backend for account management, subscriptions,
chat, audio, and logging endpoints. It uses PostgreSQL for application
data, Redis for distributed locking/rate-limiting support, ASP.NET Core
Identity for users, JWT bearer authentication, Serilog logging, and MailJet or
SMTP for email delivery depending on the environment.

## Project Structure

- `Tutor.Api/`: ASP.NET Core Web API project.
- `Tutor.Api/Controllers/`: HTTP API controllers.
- `Tutor.Api/Services/`: application services and external integrations.
- `Tutor.Api/Data/`: Entity Framework Core database context.
- `Tutor.Api/Migrations/`: EF Core database migrations.
- `Tutor.Api/Models/`: entities, request models, settings, constants, and exceptions.
- `Tests/Tutor.Api.Tests/`: xUnit test project.
- `Tutor.Api/github/workflows/publish.yml`: publish workflow definition.

## Requirements

- .NET SDK `10.0.x`
- PostgreSQL
- Redis
- Optional local SMTP server on port `2525` for development email testing

## Local Configuration

Development settings are read from `Tutor.Api/appsettings.Development.json`.
The default local values are:

```json
{
  "ConnectionStrings": {
    "TutorContext": "Host=localhost;Port=5432;Database=tutor_api;Username=postgres;Password=P@ssword1;",
    "Redis": "localhost:6379,password=DefaultPassword,abortConnect=false"
  },
  "MailConfiguration": {
    "SmtpConfiguration": {
      "Host": "localhost",
      "Port": 2525,
      "EnableSsl": false,
      "UserName": "",
      "Password": ""
    }
  }
}
```

For local development, make sure PostgreSQL and Redis are running and that the
database credentials match the connection string above.

## Production Configuration

Outside the `Development` environment, connection strings and MailJet
credentials are read from environment variables:

- `TutorConnectionString`: PostgreSQL connection string.
- `RedisConnectionString`: Redis connection string.
- `MailJetApiKey`: MailJet API key.
- `MailJetApiSecretKey`: MailJet API secret.

Application settings such as JWT issuer, audience, token lifetimes, Serilog,
and MailJet endpoint are configured in `Tutor.Api/appsettings.json`.

Do not commit production secrets to `appsettings.json`.

## Run Locally

Restore packages:

```bash
dotnet restore Tutor.Api.sln
```

Run tests:

```bash
dotnet test Tutor.Api.sln
```

Run the API:

```bash
dotnet run --project Tutor.Api/Tutor.Api.csproj
```

The development launch profile uses:

```text
http://localhost:5252
```

OpenAPI is mapped only in the `Development` environment.

## Database Migrations

Apply migrations to the configured PostgreSQL database:

```bash
dotnet ef database update --project Tutor.Api/Tutor.Api.csproj
```

Add a new migration:

```bash
dotnet ef migrations add <MigrationName> --project Tutor.Api/Tutor.Api.csproj
```

## Build and Publish Manually

Publish a Release build:

```bash
dotnet publish Tutor.Api/Tutor.Api.csproj --configuration Release --output publish
```

Publish with an explicit version:

```bash
dotnet publish Tutor.Api/Tutor.Api.csproj \
  --configuration Release \
  --output publish \
  -p:Version=1.0.0.123 \
  -p:AssemblyVersion=1.0.0.123 \
  -p:FileVersion=1.0.0.123 \
  -p:InformationalVersion=1.0.0.123
```

Run the published app:

```bash
dotnet publish/Tutor.Api.dll
```

## Publish Workflow

The publish workflow is defined in:

```text
Tutor.Api/github/workflows/publish.yml
```

It is configured for manual execution with `workflow_dispatch`.

The workflow:

1. Checks out the repository.
2. Installs .NET SDK `10.0.x`.
3. Runs the test suite in Release configuration.
4. Publishes `Tutor.Api/Tutor.Api.csproj`.
5. Stamps the application version from the GitHub run number.
6. Packages the published output as a `.tar.gz` artifact.
7. Uploads the artifact to the workflow run.

Version and artifact naming use this format:

```text
Version: 1.0.0.<github-run-number>
Name: Tutor 1.0.0.<github-run-number>
Package: Tutor 1.0.0.<github-run-number>.tar.gz
```

The workflow passes the version into these MSBuild properties:

- `Version`
- `AssemblyVersion`
- `FileVersion`
- `InformationalVersion`

Important: GitHub Actions only discovers workflow files from
`.github/workflows` at the repository root. If this workflow should run in
GitHub Actions, place it at:

```text
.github/workflows/publish.yml
```

## Server Deployment Notes

The current workflow builds and uploads an artifact. To deploy the artifact to
a Linux server, the server should have:

- .NET runtime compatible with `net10.0`
- PostgreSQL connectivity
- Redis connectivity
- Required production environment variables
- A systemd service for the API

Example systemd service:

```ini
[Unit]
Description=Tutor API
After=network.target

[Service]
WorkingDirectory=/var/www/tutor-api/current
ExecStart=/usr/bin/dotnet /var/www/tutor-api/current/Tutor.Api.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=TutorConnectionString=Host=localhost;Port=5432;Database=tutor_api;Username=tutor;Password=change-me;
Environment=RedisConnectionString=localhost:6379,password=change-me,abortConnect=false
Environment=MailJetApiKey=change-me
Environment=MailJetApiSecretKey=change-me

[Install]
WantedBy=multi-user.target
```

When using a release directory layout, publish each version into:

```text
/var/www/tutor-api/releases/<version-or-commit>
```

Then point the active symlink at the selected release:

```bash
ln -sfn /var/www/tutor-api/releases/<version-or-commit> /var/www/tutor-api/current
sudo systemctl restart tutor-api
```

## Useful Commands

Run Release tests:

```bash
dotnet test Tutor.Api.sln --configuration Release --no-restore
```

Create a packaged artifact locally:

```bash
dotnet publish Tutor.Api/Tutor.Api.csproj --configuration Release --output publish
tar -czf "Tutor 1.0.0.local.tar.gz" -C publish .
```

Check repository changes:

```bash
git status --short
```
