# ===== BASE IMAGE =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-resolute-amd64@sha256:67c94b99341ac046603156a7025ea5ea22a41949ec2cef126d22d3a7bbe21963 AS base
WORKDIR /app

# FFmpeg for media processing. ImageMagick (plus the HEVC plugin for HEIC) is only the sandboxed
# fallback decoder for image formats the bundled libvips cannot load; --no-install-recommends
# keeps ghostscript and the other PDF/PostScript delegates out of the image.
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    curl \
    imagemagick \
    libheif-plugin-libde265 \
    && rm -rf /var/lib/apt/lists/*

COPY ["Alexandria.Workers.Media/ImageMagick/policy.xml", "/etc/ImageMagick-7/policy.xml"]

# glibc arena fragmentation makes libvips RSS creep in long-running processes.
ENV MALLOC_ARENA_MAX=2 \
    VIPS_BLOCK_UNTRUSTED=1 \
    MAGICK_THREAD_LIMIT=1 \
    MAGICK_TEMPORARY_PATH=/tmp/worker-scratch

# ===== BUILD IMAGE =====
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-resolute-amd64@sha256:a93ae0a0716bc411e934c6b9468901e5c0e1025e4925b6a2d96fbb02bc0cd871 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all transitive csproj dependencies for layer caching
COPY ["Alexandria.Workers.Media/Alexandria.Workers.Media.csproj", "Alexandria.Workers.Media/"]
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

RUN dotnet restore "Alexandria.Workers.Media/Alexandria.Workers.Media.csproj"

COPY . .

WORKDIR "/src/Alexandria.Workers.Media"
RUN dotnet build "./Alexandria.Workers.Media.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ===== PUBLISH STAGE =====
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Alexandria.Workers.Media.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ===== FINAL RUNTIME =====
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN groupadd --gid 1001 appgroup && useradd --uid 1001 --gid appgroup --no-create-home appuser
RUN mkdir -p /tmp/worker-scratch && chown appuser:appgroup /tmp/worker-scratch
USER appuser

ENTRYPOINT ["dotnet", "Alexandria.Workers.Media.dll"]
