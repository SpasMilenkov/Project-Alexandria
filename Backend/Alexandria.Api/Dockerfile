# BUILD IMAGE
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine3.23-amd64@sha256:159049d8e4bb064f23ffda1143f6bfb8e64be6b9221a365fd8e344c5eeae81b4 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all transitive csproj dependencies for layer caching
COPY ["API/API.csproj", "API/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["PreviewService/PreviewService.csproj", "PreviewService/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Models/Models.csproj", "Models/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Storage/Storage.csproj", "Storage/"]
COPY ["User.Service/User.Service.csproj", "User.Service/"]
COPY ["DTO/DTO.csproj", "DTO/"]
RUN dotnet restore "API/API.csproj"

COPY . .

WORKDIR "/src/API"
RUN dotnet build "./API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# PUBLISH STAGE
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# FINAL RUNTIME
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine3.23@sha256:8c7671a6f0f984d0c102ee70d61e8010857de032b320561dea97cc5781aea5f8 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "API.dll"]
