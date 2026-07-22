# Tutor API

ASP.NET Core API for Tutor account, subscription, chat, audio, and logging
features.

## Requirements

- .NET SDK `10.0.x`
- PostgreSQL
- Redis

## Run

```bash
dotnet restore Tutor.Api.sln
dotnet test Tutor.Api.sln
dotnet run --project Tutor.Api/Tutor.Api.csproj
```

Development runs on:

```text
http://localhost:5252
```

## Configuration

Development configuration is in `Tutor.Api/appsettings.Development.json`.

Production uses environment variables:

- `TutorConnectionString`
- `RedisConnectionString`
- `MailJetApiKey`
- `MailJetApiSecretKey`

## Publish

The publish workflow is:

```text
Tutor.Api/github/workflows/publish.yml
```

It runs manually with `workflow_dispatch`, tests the solution, publishes
`Tutor.Api/Tutor.Api.csproj`, and uploads a packaged artifact.

Version and artifact names use a `number.number.number` version from the commit
message. If the commit message does not include one, the workflow uses the
latest git tag with that format. The GitHub run number is appended as the build
number.

```text
Version: <version>.<github-run-number>
Name: Tutor <version>.<github-run-number>
Package: Tutor <version>.<github-run-number>.tar.gz
```

Note: GitHub Actions discovers workflows from `.github/workflows` at the
repository root.
