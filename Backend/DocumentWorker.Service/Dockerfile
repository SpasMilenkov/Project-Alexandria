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
COPY ["DocumentWorker.Service/DocumentWorker.Service.csproj", "DocumentWorker.Service/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["PreviewService/PreviewService.csproj", "PreviewService/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Models/Models.csproj", "Models/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Storage/Storage.csproj", "Storage/"]
COPY ["User.Service/User.Service.csproj", "User.Service/"]
COPY ["DTO/DTO.csproj", "DTO/"]
RUN dotnet restore "DocumentWorker.Service/DocumentWorker.Service.csproj"

COPY . .

WORKDIR "/src/DocumentWorker.Service"
RUN dotnet build "./DocumentWorker.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ===== PUBLISH STAGE =====
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DocumentWorker.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ===== FINAL RUNTIME =====
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN groupadd --gid 1001 appgroup && useradd --uid 1001 --gid appgroup --no-create-home appuser
USER appuser

ENTRYPOINT ["dotnet", "DocumentWorker.Service.dll"]
