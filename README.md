# onvifNVR

A self-hosted Network Video Recorder (NVR) system built with **ASP.NET Core 8** (backend) and **React + Vite** (frontend). It connects to ONVIF-compatible IP cameras for live streaming, recording, PTZ control, and real-time analytics — all delivered via Docker.

---

## Features

- **Live View** — JPEG-over-WebSocket streams via SignalR for low-latency camera feeds
- **Recording** — Chunked FFmpeg recording with configurable bitrate and duration
- **Playback** — Seek, pause, speed control (0.25×–8×) on recorded footage
- **PTZ Control** — Full Pan/Tilt/Zoom, presets, focus and iris control over ONVIF
- **Role-Based Access** — Admin / Operator / Viewer roles with per-camera permission grants
- **Analytics** — Dashboard summary, per-camera stats, alert trends, storage usage
- **Multi-Database** — SQLite (default, zero-config) or SQL Server
- **Multi-Arch Docker** — Images built for `linux/amd64` and `linux/arm64`

---

## Architecture

```
┌─────────────┐        nginx proxy          ┌──────────────────┐
│  nvr-ui     │  ──── /api/, /hubs/ ──────► │  nvr-api         │
│  React/Vite │                             │  ASP.NET Core 8  │
│  port 3000  │                             │  port 8080       │
└─────────────┘                             └────────┬─────────┘
                                                     │
                                          ┌──────────┴──────────┐
                                          │  FFmpeg  │  SQLite  │
                                          │  (RTSP)  │  / MSSQL │
                                          └──────────┴──────────┘
```

---

## Quick Start

### Prerequisites
- [Docker](https://docs.docker.com/get-docker/) & Docker Compose v2
- ONVIF-compatible IP camera(s)

### 1. Clone the repo

```bash
git clone https://github.com/masterdeepak15/onvifNVR.git
cd onvifNVR
```

### 2. Configure environment

```bash
cp .env.example .env
# Edit .env — at minimum change JWT_SECRET before any production use
```

### 3. Run (SQLite — no external database needed)

```bash
docker compose up -d
```

| Service   | URL                        |
|-----------|----------------------------|
| Frontend  | http://localhost:3000      |
| API       | http://localhost:5000      |
| API docs  | http://localhost:5000/swagger |

### 4. Run with SQL Server

```bash
DATABASE_PROVIDER=SqlServer docker compose --profile sqlserver up -d
```

---

## Pre-built Docker Images

Images are published to the GitHub Container Registry on every push to `main`:

```bash
# Backend
docker pull ghcr.io/masterdeepak15/onvifnvr/nvr-api:latest

# Frontend
docker pull ghcr.io/masterdeepak15/onvifnvr/nvr-ui:latest
```

---

## Project Structure

```
onvifNVR/
├── .github/workflows/
│   ├── docker-backend.yml     # CI: build & push nvr-api image
│   └── docker-frontend.yml    # CI: build & push nvr-ui image
├── nvr-v2/                    # ASP.NET Core 8 backend
│   ├── src/
│   │   ├── NVR.API/           # Controllers, SignalR Hubs, Program.cs
│   │   ├── NVR.Core/          # Domain entities, interfaces, DTOs
│   │   └── NVR.Infrastructure/# EF Core, FFmpeg services, storage
│   └── Dockerfile
├── nvr-ui/                    # React + Vite + Tailwind frontend
│   ├── src/
│   │   ├── pages/             # Dashboard, LiveView, Playback, Settings
│   │   ├── components/        # UI components (shadcn/ui)
│   │   ├── contexts/          # Auth, Theme
│   │   └── hooks/             # useNvrHub (SignalR), use-toast, etc.
│   └── Dockerfile
├── docker-compose.yml         # Production compose (SQLite + optional SQL Server)
└── .env.example               # Environment variable template
```

---

## Configuration Reference

All settings are driven by environment variables. See [`.env.example`](.env.example) for the full list with descriptions.

| Variable | Default | Description |
|---|---|---|
| `DATABASE_PROVIDER` | `SQLite` | `SQLite` or `SqlServer` |
| `JWT_SECRET` | *(insecure default)* | **Change in production** — min 32 chars |
| `JWT_EXPIRY_HOURS` | `1` | Token lifetime in hours |
| `API_PORT` | `5000` | Host port for the backend |
| `UI_PORT` | `3000` | Host port for the frontend |
| `MAX_CAMERAS` | `64` | Max concurrent camera streams |
| `HW_ACCEL` | `none` | FFmpeg HW accel: `none`, `vaapi`, `nvenc`, `videotoolbox` |
| `RECORDINGS_PATH` | *(docker volume)* | Override with host path for NAS storage |

---

## CI / CD

Two GitHub Actions workflows run on push to `main` (path-filtered):

| Workflow | Trigger path | Image |
|---|---|---|
| `docker-backend.yml` | `nvr-v2/**` | `ghcr.io/.../nvr-api` |
| `docker-frontend.yml` | `nvr-ui/**` | `ghcr.io/.../nvr-ui` |

Both workflows support `workflow_dispatch` with a custom image tag input.  
Pull requests trigger a build-only run (no push).

---

## Development

### Backend

```bash
cd nvr-v2
dotnet restore
dotnet run --project src/NVR.API
# API available at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### Frontend

```bash
cd nvr-ui
npm install
npm run dev
# UI available at http://localhost:5173
```

### Running tests

```bash
# Frontend unit tests
cd nvr-ui && npm test
```

---

## SignalR API

The backend exposes a SignalR hub at `/hubs/nvr`. Authentication uses JWT passed as a query parameter:

```typescript
const connection = new HubConnectionBuilder()
  .withUrl("/hubs/nvr?access_token=" + jwtToken)
  .withAutomaticReconnect()
  .build();

await connection.start();
```

See [`nvr-v2/CHANGES_v2.md`](nvr-v2/CHANGES_v2.md) for the full SignalR method reference including stream controls, PTZ commands, recording control, and server-to-client events.

---

## License

MIT — see [LICENSE](LICENSE) if present in the repository.
