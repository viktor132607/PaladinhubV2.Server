FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY global.json ./
COPY PaladinHubV2.Server.API/PaladinHubV2.Server.API.csproj PaladinHubV2.Server.API/
COPY PaladinHubV2.Server.Data/PaladinHubV2.Server.Data.csproj PaladinHubV2.Server.Data/
COPY PaladinHubV2.Server.Domain/PaladinHubV2.Server.Domain.csproj PaladinHubV2.Server.Domain/
COPY PaladinHubV2.Server.Core/PaladinHubV2.Server.Core.csproj PaladinHubV2.Server.Core/
COPY PaladinHubV2.Server.Common/PaladinHubV2.Server.Common.csproj PaladinHubV2.Server.Common/

RUN dotnet restore PaladinHubV2.Server.API/PaladinHubV2.Server.API.csproj

COPY . .
RUN dotnet publish PaladinHubV2.Server.API/PaladinHubV2.Server.API.csproj     --configuration Release     --output /app/publish     --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "PaladinHubV2.Server.API.dll"]
