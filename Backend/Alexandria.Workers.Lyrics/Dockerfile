FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-resolute-amd64@sha256:67c94b99341ac046603156a7025ea5ea22a41949ec2cef126d22d3a7bbe21963 AS base
WORKDIR /app
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0.401-resolute-amd64@sha256:a93ae0a0716bc411e934c6b9468901e5c0e1025e4925b6a2d96fbb02bc0cd871 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["nuget.config", "."]
COPY ["Alexandria.Workers.Lyrics/Alexandria.Workers.Lyrics.csproj", "Alexandria.Workers.Lyrics/"]
COPY ["Alexandria.Infrastructure/Alexandria.Infrastructure.csproj", "Alexandria.Infrastructure/"]
COPY ["Alexandria.Services.Streaming/Alexandria.Services.Streaming.csproj", "Alexandria.Services.Streaming/"]
COPY ["Alexandria.Services.Preview/Alexandria.Services.Preview.csproj", "Alexandria.Services.Preview/"]
COPY ["Alexandria.Common/Alexandria.Common.csproj", "Alexandria.Common/"]
COPY ["Alexandria.Data/Alexandria.Data.csproj", "Alexandria.Data/"]
COPY ["Alexandria.Data.Models/Alexandria.Data.Models.csproj", "Alexandria.Data.Models/"]
COPY ["Alexandria.Repositories/Alexandria.Repositories.csproj", "Alexandria.Repositories/"]
COPY ["Alexandria.Services.Storage/Alexandria.Services.Storage.csproj", "Alexandria.Services.Storage/"]
COPY ["Alexandria.Services.User/Alexandria.Services.User.csproj", "Alexandria.Services.User/"]
COPY ["Alexandria.Dto/Alexandria.Dto.csproj", "Alexandria.Dto/"]
RUN dotnet restore "Alexandria.Workers.Lyrics/Alexandria.Workers.Lyrics.csproj"
COPY . .
WORKDIR "/src/Alexandria.Workers.Lyrics"
RUN dotnet build "./Alexandria.Workers.Lyrics.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Alexandria.Workers.Lyrics.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Alexandria.Workers.Lyrics.dll"]
