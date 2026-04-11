# ─── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/NVR.API/NVR.API.csproj", "src/NVR.API/"]
COPY ["src/NVR.Core/NVR.Core.csproj", "src/NVR.Core/"]
COPY ["src/NVR.Infrastructure/NVR.Infrastructure.csproj", "src/NVR.Infrastructure/"]

RUN dotnet restore "src/NVR.API/NVR.API.csproj"

COPY . .

WORKDIR "/src/src/NVR.API"
RUN dotnet publish "NVR.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ─── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# FFmpeg for RTSP stream processing and recording
# curl for Docker healthcheck
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl \
    && rm -rf /var/lib/apt/lists/*

# Storage directories:
#   /var/nvr/data       – SQLite DB file   (mount nvr-data volume here)
#   /var/nvr/recordings – Recording files  (mount nvr-recordings volume here)
#   /tmp/nvr_chunks     – Temp FFmpeg chunks (tmpfs recommended)
RUN mkdir -p /var/nvr/data /var/nvr/recordings /tmp/nvr_chunks

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NVR.API.dll"]
