FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080 8081

# Install FFmpeg for RTSP stream processing and recording
RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/NVR.API/NVR.API.csproj", "src/NVR.API/"]
COPY ["src/NVR.Core/NVR.Core.csproj", "src/NVR.Core/"]
COPY ["src/NVR.Infrastructure/NVR.Infrastructure.csproj", "src/NVR.Infrastructure/"]

RUN dotnet restore "src/NVR.API/NVR.API.csproj"

COPY . .

WORKDIR "/src/src/NVR.API"
RUN dotnet build "NVR.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NVR.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Create storage directories
RUN mkdir -p /var/nvr/recordings /tmp/nvr_chunks

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "NVR.API.dll"]
