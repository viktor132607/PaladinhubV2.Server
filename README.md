# PaladinHubV2.Server Repository

## Local configuration

The API now uses `appsettings.json` and `appsettings.Development.json` for normal local development.

- Local database connection: `ConnectionStrings:DefaultConnection`
- JWT settings: `Jwt`
- `Jwt:Secret` must be longer than 64 bytes because the API signs access tokens with `HS512`
- Render should not use `generateValue: true` for `Jwt__Secret`, because Render generates a random 256-bit value that is too short for `HS512`
- frontend base URL: `ClientApp:BaseUrl`
- CORS origins: `Cors:AllowedOrigins`
- payment/email/local feature toggles: `Payments`, `Email`, `DevelopmentSettings`

Run the API with the `https` or `http` launch profile. Both launch profiles open Scalar by default at `scalar/v1`.

The default local development database port is `5434` so the app can run alongside an existing PostgreSQL instance on `5432`. For Docker-based local setup, copy `.env.example` to `.env` in this folder and run `docker compose up -d db`.
