# NVR API Reference — Part 2 of 4
## Recordings · Storage · Layout · Schedules · Dashboard · Settings

---

# 6. Recordings

Recordings are stored as MPEG-TS chunks (NCP format, 60s per chunk). Searchable by camera, time range, and trigger type.

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/recordings` | Any authenticated |
| GET | `/api/recordings/{id}` | Any authenticated |
| POST | `/api/recordings/playback` | Any authenticated |
| GET | `/api/recordings/{id}/stream` | Any authenticated |
| DELETE | `/api/recordings/{id}` | Admin or Operator |

---

## Recording Object (RecordingDto)

```typescript
interface RecordingDto {
  id: string;
  cameraId: string;
  cameraName: string;
  startTime: string;           // ISO UTC timestamp
  endTime: string | null;      // null if still recording
  fileSizeBytes: number;       // total bytes across all chunks
  durationSeconds: number;
  status: 'Recording' | 'Completed' | 'Deleted' | 'Error';
  triggerType: 'Manual' | 'Scheduled' | 'Motion' | 'Continuous';
  thumbnailPath: string;
  width: number;
  height: number;
  chunkCount: number;          // number of 60s MPEG-TS chunks
}
```

---

## GET /api/recordings

Search and paginate recordings. All query params optional.

### Query Parameters

| Param | Type | Default | Notes |
|---|---|---|---|
| cameraId | Guid? | — | Filter by single camera |
| cameraIds | Guid[]? | — | Filter by multiple cameras (comma-separated) |
| startTime | DateTime? | — | Recordings starting after this timestamp |
| endTime | DateTime? | — | Recordings starting before this timestamp |
| triggerType | string? | — | `Manual` \| `Scheduled` \| `Motion` \| `Continuous` |
| page | int | 1 | Page number (1-based) |
| pageSize | int | 50 | Results per page |

### Response 200 (PagedResult\<RecordingDto\>)

```json
{
  "items": [
    {
      "id": "rec-guid-1",
      "cameraId": "cam-guid-1",
      "cameraName": "Front Entrance",
      "startTime": "2026-03-01T08:00:00Z",
      "endTime": "2026-03-01T08:30:00Z",
      "fileSizeBytes": 180000000,
      "durationSeconds": 1800,
      "status": "Completed",
      "triggerType": "Scheduled",
      "width": 1920,
      "height": 1080,
      "chunkCount": 30
    }
  ],
  "totalCount": 142,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3
}
```

---

## GET /api/recordings/{id}

Returns single `RecordingDto` or `404 Not Found`.

---

## POST /api/recordings/playback

Create a multi-camera synchronized playback session. Returns stream URLs and timeline data for each camera. Use the `timeline` array to render the scrubber bar.

### Request Body

```json
{
  "cameraIds": ["cam-guid-1", "cam-guid-2"],
  "timestamp": "2026-03-01T14:30:00Z",
  "speed": 1.0
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| cameraIds | Guid[] | Yes | Cameras to play simultaneously |
| timestamp | DateTime | Yes | UTC timestamp to start playback from |
| speed | float | No | Playback speed multiplier — default: 1.0 |

### Response 200

```json
{
  "sessionId": "session-guid",
  "startTimestamp": "2026-03-01T14:30:00Z",
  "cameraStreams": [
    {
      "cameraId": "cam-guid-1",
      "cameraName": "Front Entrance",
      "recordingId": "rec-guid-1",
      "hasRecording": true,
      "streamUrl": "/api/recordings/rec-guid-1/stream?t=2026-03-01T14:30:00Z",
      "timeline": [
        {
          "start": "2026-03-01T08:00:00Z",
          "end": "2026-03-01T08:30:00Z",
          "triggerType": "Scheduled",
          "hasMotion": false
        }
      ]
    },
    {
      "cameraId": "cam-guid-2",
      "cameraName": "Back Door",
      "hasRecording": false,
      "streamUrl": null,
      "timeline": []
    }
  ]
}
```

---

## GET /api/recordings/{id}/stream

Streams the MPEG-TS recording file. Supports HTTP Range requests for seeking.

### Query Parameters

| Param | Type | Notes |
|---|---|---|
| t | DateTime? | Seek to chunk containing this timestamp |

**Response:** `Content-Type: video/mp2t` — supports `Range: bytes=X-Y`

```typescript
const response = await fetch(`/api/recordings/${id}/stream`, {
  headers: {
    Authorization: `Bearer ${token}`,
    Range: 'bytes=0-'
  }
});
```

---

## DELETE /api/recordings/{id}

Deletes recording, all chunks from storage, and database records.

**Response: 204 No Content**

---

# 7. Storage Profiles

Defines where recordings are saved. Supported types: `Local`, `NAS_SMB`, `S3`, `AzureBlob`.

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/storage` | Admin only |
| GET | `/api/storage/types` | Admin only |
| POST | `/api/storage` | Admin only |
| POST | `/api/storage/{id}/test` | Admin only |
| DELETE | `/api/storage/{id}` | Admin only |

---

## Storage Profile Object (StorageProfileDto)

```typescript
interface StorageProfileDto {
  id: string;
  name: string;
  type: 'Local' | 'NAS_SMB' | 'S3' | 'AzureBlob';
  isDefault: boolean;          // used when camera has no profile assigned
  isEnabled: boolean;
  host: string | null;         // NAS IP or hostname
  port: number | null;
  username: string | null;
  basePath: string | null;     // storage root directory
  shareName: string | null;    // SMB share name
  region: string | null;       // AWS region (S3 only)
  containerName: string | null;// Azure blob container
  maxStorageBytes: number;     // quota
  usedStorageBytes: number;    // currently used
  retentionDays: number;       // auto-delete threshold
  autoDeleteEnabled: boolean;
  isHealthy: boolean;
  lastHealthCheck: string | null;
  healthError: string | null;
  usagePercent: number;        // calculated: usedBytes / maxBytes * 100
}
```

---

## GET /api/storage

### Response 200 — Array of `StorageProfileDto`

---

## GET /api/storage/types

### Response 200

```json
["Local", "NAS_SMB", "S3", "AzureBlob"]
```

---

## POST /api/storage

### Request Body

```json
{
  "name": "Local HDD",
  "type": "Local",
  "isDefault": true,
  "basePath": "/var/nvr/recordings",
  "maxStorageBytes": 536870912000,
  "retentionDays": 30,
  "autoDeleteEnabled": true
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| name | string | Yes | Display name |
| type | string | Yes | `Local` \| `NAS_SMB` \| `S3` \| `AzureBlob` |
| isDefault | bool | No | Default: false |
| host | string? | No | NAS/S3/Azure hostname |
| port | int? | No | Connection port |
| username | string? | No | Username / Access Key ID |
| password | string? | No | Password / Secret Key (never returned in responses) |
| basePath | string? | No | Base storage path |
| shareName | string? | No | SMB share name |
| region | string? | No | AWS region (S3) |
| accessKey | string? | No | AWS/Azure access key |
| secretKey | string? | No | AWS/Azure secret key |
| containerName | string? | No | Azure blob container |
| connectionString | string? | No | Azure full connection string |
| maxStorageBytes | long | No | Default: 500 GB |
| retentionDays | int | No | Default: 30 |
| autoDeleteEnabled | bool | No | Default: true |

### Examples by Type

```json
// LOCAL
{ "name": "Local HDD", "type": "Local",
  "basePath": "/var/nvr/recordings", "maxStorageBytes": 536870912000 }

// NAS/SMB
{ "name": "NAS Drive", "type": "NAS_SMB",
  "host": "192.168.1.200", "username": "nasuser",
  "password": "pass", "shareName": "nvr" }

// AWS S3
{ "name": "S3 Backup", "type": "S3",
  "region": "ap-south-1", "accessKey": "AKIA...",
  "secretKey": "...", "basePath": "my-bucket/recordings" }

// Azure Blob
{ "name": "Azure", "type": "AzureBlob",
  "containerName": "nvr",
  "connectionString": "DefaultEndpointsProtocol=https;AccountName=..." }
```

### Response 201 — Returns created `StorageProfileDto`

---

## POST /api/storage/{id}/test

Tests the connection to the storage provider.

### Response 200

```json
{ "success": true, "message": "Connection successful" }
```

---

## DELETE /api/storage/{id}

Returns `409 Conflict` if cameras are still assigned to this profile.

**Response: 204 No Content**

---

# 8. Grid Layout

Each user has their own named grid layouts. A layout maps cameras to grid cell positions (0-based index). `gridColumns` controls cameras per row.

## Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/layout` | Get layout by name |
| POST | `/api/layout` | Save / overwrite layout |
| GET | `/api/layout/names` | List all saved layout names |

---

## GET /api/layout

### Query Parameters

| Param | Default | Notes |
|---|---|---|
| layoutName | `"Default"` | Name of layout to load |

### Response 200

```json
{
  "layoutName": "Default",
  "gridColumns": 4,
  "positions": [
    { "cameraId": "cam-guid-1", "gridPosition": 0 },
    { "cameraId": "cam-guid-2", "gridPosition": 1 },
    { "cameraId": "cam-guid-3", "gridPosition": 4 }
  ]
}
```

---

## POST /api/layout

Overwrites any existing layout with same name for the current user.

### Request Body

```json
{
  "layoutName": "Security Desk",
  "gridColumns": 4,
  "positions": [
    { "cameraId": "cam-guid-1", "gridPosition": 0 },
    { "cameraId": "cam-guid-2", "gridPosition": 1 },
    { "cameraId": "cam-guid-3", "gridPosition": 2 },
    { "cameraId": "cam-guid-4", "gridPosition": 3 }
  ]
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| layoutName | string | Yes | Any string name |
| gridColumns | int | Yes | Cameras per row: 2, 3, 4, 6, 8 |
| positions | array | Yes | Array of `{ cameraId, gridPosition }` |

### Grid Position Index (gridColumns = 4)

```
[ 0 ][ 1 ][ 2 ][ 3 ]
[ 4 ][ 5 ][ 6 ][ 7 ]
[ 8 ][ 9 ][10 ][11 ]
```

**Response: 200 OK**

---

## GET /api/layout/names

### Response 200

```json
["Default", "Security Desk", "Night Mode"]
```

---

# 9. Recording Schedules

Schedules define time windows when recording automatically starts and stops.

`daysOfWeek` is a **bitmask**: Sun=1, Mon=2, Tue=4, Wed=8, Thu=16, Fri=32, Sat=64

| Common pattern | Value |
|---|---|
| Mon–Fri | 62 |
| All week | 127 |
| Weekends | 65 |
| Mon+Wed+Fri | 42 |

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/cameras/{id}/schedules` | Any authenticated |
| POST | `/api/cameras/{id}/schedules` | Admin or Operator |
| DELETE | `/api/cameras/{id}/schedules/{scheduleId}` | Admin or Operator |

---

## GET /api/cameras/{id}/schedules

### Response 200

```json
[
  {
    "id": "sched-guid",
    "cameraId": "cam-guid",
    "name": "Business Hours",
    "isEnabled": true,
    "daysOfWeek": 62,
    "startTime": "09:00:00",
    "endTime": "18:00:00",
    "recordingMode": "Continuous",
    "chunkDurationSeconds": 60,
    "bitrateKbps": 2000,
    "quality": "High"
  }
]
```

---

## POST /api/cameras/{id}/schedules

### Request Body

```json
{
  "name": "Night Watch",
  "isEnabled": true,
  "daysOfWeek": 127,
  "startTime": "20:00:00",
  "endTime": "08:00:00",
  "recordingMode": "Continuous",
  "chunkDurationSeconds": 60,
  "bitrateKbps": 2000,
  "quality": "High"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| name | string | Yes | Display name |
| isEnabled | bool | No | Default: true |
| daysOfWeek | int | Yes | Bitmask — All week = 127 |
| startTime | TimeSpan | Yes | Format: `HH:mm:ss` |
| endTime | TimeSpan | Yes | Format: `HH:mm:ss` |
| recordingMode | string | No | `Continuous` \| `Motion` — default: Continuous |
| chunkDurationSeconds | int | No | Default: 60 |
| bitrateKbps | int | No | Default: 2000 |
| quality | string | No | `Low` \| `Medium` \| `High` \| `Ultra` — default: High |

**Response: 201 Created**

---

## DELETE /api/cameras/{id}/schedules/{scheduleId}

**Response: 204 No Content**

---

# 10. Dashboard

Quick summary for the main NVR dashboard page.

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/dashboard` | Any authenticated |
| GET | `/api/dashboard/events` | Any authenticated |

---

## GET /api/dashboard

### Response 200

```json
{
  "totalCameras": 8,
  "onlineCameras": 7,
  "recordingCameras": 5,
  "offlineCameras": 1,
  "totalStorageBytes": 536870912000,
  "usedStorageBytes": 161061273600,
  "activeAlerts": 3,
  "todayRecordingCount": 24,
  "storageSummaries": [
    {
      "id": "storage-guid",
      "name": "Local HDD",
      "type": "Local",
      "maxStorageBytes": 536870912000,
      "usedStorageBytes": 161061273600,
      "isHealthy": true
    }
  ]
}
```

---

## GET /api/dashboard/events

Returns most recent camera events.

### Query Parameters

| Param | Default | Notes |
|---|---|---|
| count | 50 | Max events to return |

### Response 200

```json
[
  {
    "id": "event-guid",
    "cameraId": "cam-guid",
    "cameraName": "Front Entrance",
    "eventType": "Motion",
    "severity": "Info",
    "timestamp": "2026-03-01T14:55:30Z",
    "details": "Motion detected in zone 1",
    "isAcknowledged": false
  }
]
```

| eventType | Meaning |
|---|---|
| Motion | Motion detection triggered |
| Tamper | Camera tamper detected |
| Online | Camera came back online |
| Offline | Camera went offline |
| Error | Camera or system error |

| severity | Meaning |
|---|---|
| Info | Informational |
| Warning | Should be reviewed |
| Critical | Immediate attention needed |

---

# 11. System Settings

Key-value configuration store. **Admin only.**

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/settings` | Admin only |
| PUT | `/api/settings` | Admin only |

---

## GET /api/settings

### Response 200 (Dictionary\<string, string\>)

```json
{
  "system.name": "NVR System",
  "recording.default_chunk_seconds": "60",
  "recording.default_bitrate_kbps": "2000",
  "storage.low_space_warning_percent": "85",
  "stream.max_cameras": "64",
  "stream.default_fps": "15",
  "analytics.hourly_snapshot_enabled": "true"
}
```

---

## PUT /api/settings

Send only keys you want to change. New keys will be created.

### Request Body

```json
{
  "recording.default_bitrate_kbps": "4000",
  "storage.low_space_warning_percent": "90"
}
```

**Response: 200 OK**

---

## Known Settings Keys

| Key | Default | Description |
|---|---|---|
| system.name | NVR System | System display name |
| recording.default_chunk_seconds | 60 | NCP chunk duration |
| recording.default_bitrate_kbps | 2000 | Default H.264 recording bitrate |
| storage.low_space_warning_percent | 85 | Alert threshold % for storage |
| stream.max_cameras | 64 | Max simultaneous live streams |
| stream.default_fps | 15 | Target FPS for live streaming |
| analytics.hourly_snapshot_enabled | true | Enable hourly analytics snapshots |

---

## Service Layer Examples

```typescript
// services/recordings.ts
import api from './client';

export const recordingService = {
  search: (params: {
    cameraId?: string;
    startTime?: string;
    endTime?: string;
    triggerType?: string;
    page?: number;
    pageSize?: number;
  }) => api.get('/api/recordings', { params }).then(r => r.data),

  getById: (id: string) =>
    api.get(`/api/recordings/${id}`).then(r => r.data),

  startPlayback: (cameraIds: string[], timestamp: string, speed = 1.0) =>
    api.post('/api/recordings/playback', { cameraIds, timestamp, speed }).then(r => r.data),

  getStreamUrl: (id: string, timestamp?: string) => {
    const t = timestamp ? `?t=${encodeURIComponent(timestamp)}` : '';
    return `/api/recordings/${id}/stream${t}`;
  },

  delete: (id: string) => api.delete(`/api/recordings/${id}`)
};

// services/storage.ts
export const storageService = {
  getAll: () => api.get('/api/storage').then(r => r.data),
  getTypes: () => api.get('/api/storage/types').then(r => r.data),
  create: (data: object) => api.post('/api/storage', data).then(r => r.data),
  test: (id: string) => api.post(`/api/storage/${id}/test`).then(r => r.data),
  delete: (id: string) => api.delete(`/api/storage/${id}`)
};

// services/layout.ts
export const layoutService = {
  get: (name = 'Default') =>
    api.get('/api/layout', { params: { layoutName: name } }).then(r => r.data),
  save: (layout: object) => api.post('/api/layout', layout),
  getNames: () => api.get('/api/layout/names').then(r => r.data)
};
```
