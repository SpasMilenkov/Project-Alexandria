# ===== BASE IMAGE =====
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
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
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DocumentWorker.Service/DocumentWorker.Service.csproj", "DocumentWorker.Service/"]
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

ENTRYPOINT ["dotnet", "DocumentWorker.Service.dll"]
