# Voices of Wynn API

.NET 10 backend.

## Guidelines

- No repositories should be injected into controllers. Use services instead.
- Keep a clean structure.
- If a service grows too big, split it up. Avoid 1k-line files.
- Database structure is managed by Liquibase in the PHP app.

## Local Workflow

To simply restart the API service from the repository root:

```sh
podman compose -f docker-compose.dev.yml restart vow-api
```

After making API source code changes, run from the repository root:

```sh
cd vow-api
dotnet build src/VoW.Api/VoW.Api.csproj
cd ..
docker-compose -f docker-compose.dev.yml build vow-api
docker-compose -f docker-compose.dev.yml up -d vow-api
```

The dev service mounts the Debug DLL from the local build output, so source edits won't take effect in the container without `dotnet build`.

If `up -d vow-api` fails because an existing container name is already in use, run:

```sh
docker-compose -f docker-compose.dev.yml up -d --force-recreate vow-api
```
