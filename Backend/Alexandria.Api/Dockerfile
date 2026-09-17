# BUILD IMAGE
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-alpine3.24-amd64@sha256:72d2c98e9b40abb6c19ee10893c1a86ec90dc2c2d86bd84c28622458d4f5bf5c AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all transitive csproj dependencies for layer caching
COPY ["Alexandria.Api/Alexandria.Api.csproj", "Alexandria.Api/"]
COPY ["Alexandria.Infrastructure/Alexandria.Infrastructure.csproj", "Alexandria.Infrastructure/"]
COPY ["Alexandria.Services.Preview/Alexandria.Services.Preview.csproj", "Alexandria.Services.Preview/"]
COPY ["Alexandria.Services.Streaming/Alexandria.Services.Streaming.csproj", "Alexandria.Services.Streaming/"]
COPY ["Alexandria.Common/Alexandria.Common.csproj", "Alexandria.Common/"]
COPY ["Alexandria.Data/Alexandria.Data.csproj", "Alexandria.Data/"]
COPY ["Alexandria.Data.Models/Alexandria.Data.Models.csproj", "Alexandria.Data.Models/"]
COPY ["Alexandria.Repositories/Alexandria.Repositories.csproj", "Alexandria.Repositories/"]
COPY ["Alexandria.Services.Storage/Alexandria.Services.Storage.csproj", "Alexandria.Services.Storage/"]
COPY ["Alexandria.Services.User/Alexandria.Services.User.csproj", "Alexandria.Services.User/"]
COPY ["Alexandria.Dto/Alexandria.Dto.csproj", "Alexandria.Dto/"]

COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["nuget.config", "."]

RUN dotnet restore "Alexandria.Api/Alexandria.Api.csproj"

COPY . .

WORKDIR "/src/Alexandria.Api"
RUN dotnet build "./Alexandria.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# PUBLISH STAGE
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Alexandria.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# FINAL RUNTIME
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.24-amd64@sha256:0eb381be9d708a3c190c4c3e16c5270529078653888578b84d6f479c2f9694f4 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "Alexandria.Api.dll"]
