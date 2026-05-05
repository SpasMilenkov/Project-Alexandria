# ===== BASE IMAGE =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:ccdca44cd4f256d50187f920dc8ccc2a9ea7a8a4597ac1d51e08fddb2e3b3205 AS base
WORKDIR /app

#   Third Party tools used
#   - libreoffice: for office document conversions (headless mode)
#   - poppler-utils: utils for pdf processing
#   - ghostscript: PDF manipulation/conversion backend
#   - fonts-dejavu: ensures common fonts exist for conversions
RUN apt-get update && apt-get install -y --no-install-recommends \
    libreoffice \
    poppler-utils \
    ghostscript \
    fonts-dejavu-core \
    curl \
    && rm -rf /var/lib/apt/lists/*

ENV HOME=/tmp \
    LANG=en_US.UTF-8 \
    LC_ALL=en_US.UTF-8 \
    PATH="$PATH:/usr/lib/libreoffice/program"

# ===== BUILD IMAGE =====
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:f061e5a7532b36fa1d1b684857fe1f504ba92115b9934f154643266613c44c62 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy all transitive csproj dependencies for layer caching
COPY ["Alexandria.Workers.Document/Alexandria.Workers.Document.csproj", "Alexandria.Workers.Document/"]
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

RUN dotnet restore "Alexandria.Workers.Document/Alexandria.Workers.Document.csproj"

COPY . .

WORKDIR "/src/Alexandria.Workers.Document"
RUN dotnet build "./Alexandria.Workers.Document.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ===== PUBLISH STAGE =====
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Alexandria.Workers.Document.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ===== FINAL RUNTIME =====
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN groupadd --gid 1001 appgroup && useradd --uid 1001 --gid appgroup --no-create-home appuser
USER appuser

ENTRYPOINT ["dotnet", "Alexandria.Workers.Document.dll"]
