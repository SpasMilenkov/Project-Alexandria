# ===== BASE IMAGE =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-resolute-amd64@sha256:67c94b99341ac046603156a7025ea5ea22a41949ec2cef126d22d3a7bbe21963 AS base
WORKDIR /app

# Install FFmpeg for media processing
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    curl \
    && rm -rf /var/lib/apt/lists/*

# ===== BUILD IMAGE =====
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-resolute-amd64@sha256:a93ae0a0716bc411e934c6b9468901e5c0e1025e4925b6a2d96fbb02bc0cd871 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all transitive csproj dependencies for layer caching
COPY ["Alexandria.Workers.MediaTranspilation/Alexandria.Workers.MediaTranspilation.csproj", "Alexandria.Workers.MediaTranspilation/"]
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

RUN dotnet restore "Alexandria.Workers.MediaTranspilation/Alexandria.Workers.MediaTranspilation.csproj"

COPY . .

WORKDIR "/src/Alexandria.Workers.MediaTranspilation"
RUN dotnet build "./Alexandria.Workers.MediaTranspilation.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ===== PUBLISH STAGE =====
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Alexandria.Workers.MediaTranspilation.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ===== FINAL RUNTIME =====
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN groupadd --gid 1001 appgroup && useradd --uid 1001 --gid appgroup --no-create-home appuser
RUN mkdir -p /tmp/worker-scratch && chown appuser:appgroup /tmp/worker-scratch
USER appuser

ENTRYPOINT ["dotnet", "Alexandria.Workers.MediaTranspilation.dll"]
