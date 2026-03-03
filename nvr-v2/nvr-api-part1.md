# NVR API Reference — Part 1 of 4
## Authentication · Users · Cameras · PTZ

---

## Base Configuration

| Key | Value |
|---|---|
| Base URL | `http://localhost:5000` |
| Content-Type | `application/json` |
| Auth Header | `Authorization: Bearer <accessToken>` |
| Swagger | `http://localhost:5000/swagger` |

### Default Seed Users

| Username | Password | Role |
|---|---|---|
| admin | Admin@123 | Admin |
| operator | Operator@123 | Operator |
| viewer | Viewer@123 | Viewer |

---

# 1. Authentication

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| POST | `/api/auth/login` | Public |
| POST | `/api/auth/refresh` | Public |
| POST | `/api/auth/register` | Admin only |
| POST | `/api/auth/logout` | Any user |
| GET | `/api/auth/me` | Any user |

---

## POST /api/auth/login

### Request Body

```json
{
  "username": "admin",
  "password": "Admin@123"
}
```

| Field | Type | Required |
|---|---|---|
| username | string | Yes |
| password | string | Yes |

### Response 200

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "c3a4b5d6e7f8a9b0c1d2e3f4a5b6c7d8...",
  "expiresAt": "2026-03-01T15:00:00Z",
  "user": {
    "id": "user-guid",
    "username": "admin",
    "email": "admin@nvr.local",
    "role": "Admin",
    "lastLoginAt": "2026-03-01T14:00:00Z"
  }
}
```

### Error Responses

| Status | Body |
|---|---|
| 401 | `{ "message": "Invalid credentials" }` |

### TypeScript Example

```typescript
const res = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username, password })
});
if (!res.ok) throw new Error('Invalid credentials');
const data = await res.json();
// Store tokens
sessionStorage.setItem('accessToken', data.accessToken);
sessionStorage.setItem('refreshToken', data.refreshToken);
```

---

## POST /api/auth/refresh

Exchange a refresh token for a new token pair. Call when `accessToken` expires (1 hour) or on receiving 401.

### Request Body

```json
{
  "refreshToken": "c3a4b5d6..."
}
```

| Field | Type | Required |
|---|---|---|
| refreshToken | string | Yes |

### Response 200

Same structure as login — new `accessToken`, `refreshToken`, `expiresAt`.

### Error

| Status | Body |
|---|---|
| 401 | `{ "message": "Invalid refresh token" }` |

### Auto-Refresh Pattern (Axios Interceptor)

```typescript
axiosInstance.interceptors.response.use(
  res => res,
  async err => {
    if (err.response?.status === 401 && !err.config._retry) {
      err.config._retry = true;
      const rt = sessionStorage.getItem('refreshToken');
      if (rt) {
        const { data } = await axios.post('/api/auth/refresh', { refreshToken: rt });
        sessionStorage.setItem('accessToken', data.accessToken);
        err.config.headers['Authorization'] = 'Bearer ' + data.accessToken;
        return axiosInstance(err.config);
      }
    }
    return Promise.reject(err);
  }
);
```

---

## POST /api/auth/register

**Requires Admin role.**

### Request Body

```json
{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "Secure@123",
  "role": "Operator"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| username | string | Yes | Unique, max 50 chars |
| email | string | Yes | Valid email address |
| password | string | Yes | Min 8 characters |
| role | string | No | `Admin` \| `Operator` \| `Viewer` — default: `Viewer` |

### Response 200

```json
{
  "id": "user-guid",
  "username": "john_doe",
  "email": "john@example.com",
  "role": "Operator"
}
```

### Errors

```
409 — { "message": "Username already exists" }
401 — Missing/invalid Bearer token
403 — Caller is not Admin
```

---

## POST /api/auth/logout

No body. Requires `Authorization: Bearer <token>`.

**Response: 204 No Content**

Clear `accessToken` and `refreshToken` from client state on success.

---

## GET /api/auth/me

Returns current user's profile.

### Response 200

```json
{
  "id": "user-guid",
  "username": "admin",
  "email": "admin@nvr.local",
  "role": "Admin",
  "lastLoginAt": "2026-03-01T14:00:00Z"
}
```

---

# 2. User Management

**All endpoints require Admin role.**

## Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/users` | List all users |
| PUT | `/api/users/{id}` | Update user |
| DELETE | `/api/users/{id}` | Delete user |

---

## GET /api/users

### Response 200

```json
[
  {
    "id": "user-guid-1",
    "username": "admin",
    "email": "admin@nvr.local",
    "role": "Admin",
    "lastLoginAt": "2026-03-01T14:00:00Z"
  },
  {
    "id": "user-guid-2",
    "username": "operator",
    "email": "operator@nvr.local",
    "role": "Operator",
    "lastLoginAt": "2026-03-01T10:30:00Z"
  }
]
```

---

## PUT /api/users/{id}

All fields optional.

### Request Body

```json
{
  "username": "new_name",
  "email": "new@email.com",
  "password": "NewPass@123",
  "role": "Operator",
  "isActive": true
}
```

| Field | Type | Notes |
|---|---|---|
| username | string? | New username |
| email | string? | New email |
| password | string? | Min 8 chars |
| role | string? | `Admin` \| `Operator` \| `Viewer` |
| isActive | bool? | Enable/disable account |

### Response 200

Returns updated `UserDto` (id, username, email, role).

---

## DELETE /api/users/{id}

Cannot delete your own account.

**Response: 204 No Content**

---

# 3. Camera Management

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/cameras` | Any authenticated |
| GET | `/api/cameras/{id}` | Any authenticated |
| POST | `/api/cameras` | Admin or Operator |
| PUT | `/api/cameras/{id}` | Admin or Operator |
| DELETE | `/api/cameras/{id}` | Admin only |
| GET | `/api/cameras/discover` | Admin or Operator |
| POST | `/api/cameras/{id}/recording/start` | Admin or Operator |
| POST | `/api/cameras/{id}/recording/stop` | Admin or Operator |
| GET | `/api/cameras/{id}/snapshot` | Any authenticated |

---

## Camera Object (CameraDto)

```typescript
interface CameraDto {
  id: string;                  // GUID
  name: string;
  ipAddress: string;
  port: number;                // HTTP/ONVIF port (default 80)
  rtspUrl: string;             // Full RTSP stream URL
  onvifServiceUrl: string;     // ONVIF device service URL
  manufacturer: string;        // e.g. "Hikvision" — auto-discovered
  model: string;               // e.g. "DS-2CD2143G2-I" — auto-discovered
  status: 'Online' | 'Offline' | 'Error' | 'Unknown';
  isOnline: boolean;
  isRecording: boolean;
  ptzCapable: boolean;
  audioEnabled: boolean;
  resolution_Width: number;
  resolution_Height: number;
  framerate: number;
  codec: string;               // "H264" | "H265" | "MJPEG"
  gridPosition: number;        // 0-based grid cell index
  storageProfileId: string | null;
  createdAt: string;           // ISO DateTime
  lastSeenAt: string | null;
  ptzPresets: Array<{
    id: string;
    name: string;
    onvifToken: string;
    panPosition: number | null;
    tiltPosition: number | null;
    zoomPosition: number | null;
  }>;
}
```

---

## GET /api/cameras

Returns all cameras ordered by name.

### Response 200

```json
[
  {
    "id": "cam-guid-1",
    "name": "Front Entrance",
    "ipAddress": "192.168.1.100",
    "port": 80,
    "rtspUrl": "rtsp://admin:pass@192.168.1.100:554/stream1",
    "manufacturer": "Hikvision",
    "model": "DS-2CD2143G2-I",
    "status": "Online",
    "isOnline": true,
    "isRecording": true,
    "ptzCapable": false,
    "resolution_Width": 2560,
    "resolution_Height": 1440,
    "framerate": 25,
    "codec": "H265",
    "gridPosition": 0,
    "lastSeenAt": "2026-03-01T14:55:00Z",
    "ptzPresets": []
  }
]
```

---

## GET /api/cameras/{id}

Returns single `CameraDto` or `404 Not Found`.

---

## POST /api/cameras

`autoDiscover: true` (default) probes ONVIF to fill `manufacturer`, `model`, `rtspUrl`, `codec` automatically.

### Request Body

```json
{
  "name": "Front Gate",
  "ipAddress": "192.168.1.100",
  "port": 80,
  "username": "admin",
  "password": "camera123",
  "rtspUrl": null,
  "storageProfileId": "storage-guid",
  "autoDiscover": true
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| name | string | Yes | Display name |
| ipAddress | string | Yes | Camera IP |
| port | int | No | Default: 80 |
| username | string | Yes | Camera login username |
| password | string | Yes | Camera login password |
| rtspUrl | string? | No | Manual RTSP — skips ONVIF |
| storageProfileId | Guid? | No | Storage profile for recordings |
| autoDiscover | bool | No | Default: true |

### Response 201

Returns full `CameraDto`. If `status` is `"Error"`, ONVIF discovery failed but camera was saved with manual RTSP.

---

## PUT /api/cameras/{id}

All fields optional.

### Request Body

```json
{
  "name": "Rear Car Park",
  "username": "newuser",
  "password": "newpass",
  "rtspUrl": "rtsp://...",
  "storageProfileId": "storage-guid",
  "audioEnabled": true
}
```

| Field | Type | Notes |
|---|---|---|
| name | string? | New display name |
| username | string? | Camera username |
| password | string? | Camera password |
| rtspUrl | string? | Override RTSP URL |
| storageProfileId | Guid? | Change storage profile |
| audioEnabled | bool? | Enable/disable audio |

### Response 200 — Updated `CameraDto`

---

## DELETE /api/cameras/{id}

Stops active recording, deletes recording files from storage, removes from DB.

**Response: 204 No Content**

---

## GET /api/cameras/discover

Scans network via ONVIF WS-Discovery multicast (UDP 3702). Times out after 5 seconds.

### Response 200

```json
[
  {
    "xAddr": "http://192.168.1.101/onvif/device_service",
    "ipAddress": "192.168.1.101",
    "port": 80,
    "manufacturer": "Axis",
    "model": "P3245-V",
    "isAlreadyAdded": false
  }
]
```

| Field | Notes |
|---|---|
| xAddr | Full ONVIF device service URL |
| isAlreadyAdded | True if this IP is already in the system |

---

## POST /api/cameras/{id}/recording/start

Starts manual recording in MPEG-TS NCP format (60-second chunks with SHA256 checksums).

**Response 200:** `{ "message": "Recording started" }`

---

## POST /api/cameras/{id}/recording/stop

Finalizes the current chunk and stops recording.

**Response 200:** `{ "message": "Recording stopped" }`

---

## GET /api/cameras/{id}/snapshot

Returns a single JPEG frame via ONVIF GetSnapshotUri.

**Response:** `Content-Type: image/jpeg` — raw binary

```typescript
const res = await fetch(`/api/cameras/${id}/snapshot`, {
  headers: { Authorization: `Bearer ${token}` }
});
const blob = await res.blob();
img.src = URL.createObjectURL(blob);
```

---

# 4. PTZ Control (REST)

For real-time joystick control use the **SignalR `PtzCommand` method** (see Part 4). Use these REST endpoints for preset management.

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| POST | `/api/cameras/{id}/ptz/move` | Admin or Operator |
| POST | `/api/cameras/{id}/ptz/stop` | Admin or Operator |
| GET | `/api/cameras/{id}/ptz/presets` | Any authenticated |
| POST | `/api/cameras/{id}/ptz/presets/{presetId}/goto` | Admin or Operator |
| POST | `/api/cameras/{id}/ptz/presets` | Admin or Operator |
| DELETE | `/api/cameras/{id}/ptz/presets/{presetId}` | Admin or Operator |

---

## POST /api/cameras/{id}/ptz/move

### Request Body

```json
{
  "pan": 0.5,
  "tilt": 0.3,
  "zoom": 0.0,
  "moveType": "Continuous"
}
```

| Field | Type | Notes |
|---|---|---|
| pan | float | -1.0 (left) to 1.0 (right). 0 = no pan |
| tilt | float | -1.0 (down) to 1.0 (up). 0 = no tilt |
| zoom | float | 0.0 (wide) to 1.0 (tele). 0 = no zoom |
| moveType | string | `Continuous` \| `Absolute` \| `Relative` — default: Continuous |

| MoveType | Behaviour |
|---|---|
| `Continuous` | Camera moves at speed until Stop is called. Best for joystick. |
| `Absolute` | Camera moves to exact pan/tilt/zoom position. |
| `Relative` | Camera moves by delta relative to current position. |

**Response: 200 OK**

---

## POST /api/cameras/{id}/ptz/stop

Stops all PTZ movement immediately.

**Response: 200 OK**

---

## GET /api/cameras/{id}/ptz/presets

### Response 200

```json
[
  {
    "id": "preset-guid",
    "name": "Entrance View",
    "onvifToken": "001",
    "panPosition": 0.15,
    "tiltPosition": -0.05,
    "zoomPosition": 0.0
  }
]
```

---

## POST /api/cameras/{id}/ptz/presets/{presetId}/goto

Move camera to a saved preset. No body.

**Response: 200 OK**

---

## POST /api/cameras/{id}/ptz/presets

Save current camera position as a named preset on the ONVIF device and in DB.

### Request Body

```
"Entrance View"
```

Plain string — just the preset name.

**Response 200** — returns saved `PtzPresetDto`

---

## DELETE /api/cameras/{id}/ptz/presets/{presetId}

Removes from ONVIF device and database.

**Response: 204 No Content**

---

# 5. My Cameras (Role-Filtered)

These endpoints return only cameras the current user has access to, with effective permission level.

## Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/my/cameras` | My accessible cameras |
| GET | `/api/my/layout` | My layout (filtered) |

---

## GET /api/my/cameras

### Response 200

```json
[
  {
    "id": "cam-guid",
    "name": "Front Entrance",
    "ipAddress": "192.168.1.100",
    "status": "Online",
    "isOnline": true,
    "isRecording": true,
    "ptzCapable": true,
    "resolution_Width": 1920,
    "resolution_Height": 1080,
    "framerate": 25,
    "gridPosition": 0,
    "lastSeenAt": "2026-03-01T14:55:00Z",
    "permission": "Control",
    "canControl": true,
    "canRecord": false,
    "ptzPresets": [
      { "id": "preset-guid", "name": "Main View", "onvifToken": "001" }
    ]
  }
]
```

---

## GET /api/my/layout

### Query Parameters

| Param | Default | Notes |
|---|---|---|
| layoutName | `"Default"` | Layout name to load |

### Response 200

```json
{
  "layoutName": "Default",
  "gridColumns": 4,
  "positions": [
    { "cameraId": "cam-guid-1", "gridPosition": 0 },
    { "cameraId": "cam-guid-2", "gridPosition": 1 }
  ]
}
```

---

## Recommended HTTP Client

```typescript
// api/client.ts
import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  headers: { 'Content-Type': 'application/json' }
});

api.interceptors.request.use(config => {
  const token = sessionStorage.getItem('accessToken');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  res => res,
  async err => {
    if (err.response?.status === 401 && !err.config._retry) {
      err.config._retry = true;
      const rt = sessionStorage.getItem('refreshToken');
      if (rt) {
        const { data } = await axios.post('/api/auth/refresh', { refreshToken: rt });
        sessionStorage.setItem('accessToken', data.accessToken);
        err.config.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(err.config);
      }
    }
    return Promise.reject(err);
  }
);

export default api;
```
