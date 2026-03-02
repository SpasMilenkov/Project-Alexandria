# ===== BASE IMAGE =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Install FFmpeg for media processing
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# ===== BUILD IMAGE =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all transitive csproj dependencies for layer caching
COPY ["MediaWorkerService/MediaWorkerService.csproj", "MediaWorkerService/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["PreviewService/PreviewService.csproj", "PreviewService/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Models/Models.csproj", "Models/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Storage/Storage.csproj", "Storage/"]
COPY ["User.Service/User.Service.csproj", "User.Service/"]
COPY ["DTO/DTO.csproj", "DTO/"]
RUN dotnet restore "MediaWorkerService/MediaWorkerService.csproj"

COPY . .
WORKDIR "/src/MediaWorkerService"
RUN dotnet build "./MediaWorkerService.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ===== PUBLISH STAGE =====
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MediaWorkerService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ===== FINAL RUNTIME =====
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "MediaWorkerService.dll"]
