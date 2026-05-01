# BUILD IMAGE
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine3.23-amd64@sha256:159049d8e4bb064f23ffda1143f6bfb8e64be6b9221a365fd8e344c5eeae81b4 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all transitive csproj dependencies for layer caching
COPY ["Alexandria.Api/Alexandria.Api.csproj", "Alexandria.Api/"]
COPY ["Alexandria.Infrastructure/Alexandria.Infrastructure.csproj", "Alexandria.Infrastructure/"]
COPY ["Alexandria.Services.Preview/Alexandria.Services.Preview.csproj", "Alexandria.Services.Preview/"]
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine3.23@sha256:8c7671a6f0f984d0c102ee70d61e8010857de032b320561dea97cc5781aea5f8 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "Alexandria.Api.dll"]
