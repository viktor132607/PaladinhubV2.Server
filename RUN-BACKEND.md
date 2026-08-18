# Run PaladinHubV2 backend locally

## PostgreSQL only in Docker, API from Visual Studio

```powershell
docker compose up -d db
```

Start `PaladinHubV2.Server.API` from Visual Studio and open:

- `http://localhost:10000/health`

The PostgreSQL container is available on:

- host: `localhost`
- port: `5434`
- database: `paladinhubv2db`
- user: `postgres`

## API and PostgreSQL both in Docker

```powershell
docker compose up -d --build
```

Stop services:

```powershell
docker compose down
```

Delete the local database volume and recreate it:

```powershell
docker compose down -v
docker compose up -d --build
```
