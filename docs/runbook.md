# Runbook (development)

This runbook lists basic commands to run the app locally and manage migrations.

Run API locally:

dotnet run --project FinanceApp.Api/FinanceApp.Api.csproj

Build UI locally:

cd finance-app-ui
npm install
npm start

Apply EF migrations (dev):

dotnet tool install --global dotnet-ef --version 8.0.22
dotnet ef migrations add MyMigration --project FinanceApp.Api.csproj --startup-project FinanceApp.Api.csproj --context AppIdentityDbContext
dotnet ef database update --project FinanceApp.Api.csproj --startup-project FinanceApp.Api.csproj --context AppIdentityDbContext

Secrets: set `Jwt__Key`, `Admin__Email`, `Admin__Password` as environment variables in production. Use Key Vault or secret manager in CI/CD.
