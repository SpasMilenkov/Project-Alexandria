FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AlexandriaW.Workers.MediaMetadata/AlexandriaW.Workers.MediaMetadata.csproj", "AlexandriaW.Workers.MediaMetadata/"]
RUN dotnet restore "AlexandriaW.Workers.MediaMetadata/AlexandriaW.Workers.MediaMetadata.csproj"
COPY . .
WORKDIR "/src/AlexandriaW.Workers.MediaMetadata"
RUN dotnet build "./AlexandriaW.Workers.MediaMetadata.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AlexandriaW.Workers.MediaMetadata.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AlexandriaW.Workers.MediaMetadata.dll"]
