# NVR API Reference — Part 3 of 4
## Camera Access (RBAC) · Analytics

---

# 12. Camera Access — Role-Based Permissions

Controls which users can access which cameras. Four permission levels exist.

## Permission Levels

| Level | What It Allows |
|---|---|
| `View` | Watch live stream + playback recordings |
| `Control` | View + send PTZ commands + request snapshots |
| `Record` | Control + manually start/stop recording |
| `Admin` | Record + edit camera settings + delete recordings |

## Default Role Behaviour

| Role | Default Camera Access |
|---|---|
| Admin | Admin-level on ALL cameras. No grants needed. |
| Operator | Control-level on ALL cameras by default. Needs explicit grant for Record/Admin. |
| Viewer | NO cameras by default. Must be explicitly granted per-camera by an Admin. |

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/cameras/{cameraId}/access` | Admin only |
| POST | `/api/cameras/{cameraId}/access` | Admin only |
| PUT | `/api/cameras/{cameraId}/access/{accessId}` | Admin only |
| DELETE | `/api/cameras/{cameraId}/access/{accessId}` | Admin only |
| GET | `/api/cameras/{cameraId}/access/my-permission` | Any authenticated |
| GET | `/api/users/{userId}/camera-permissions` | Admin only |
| PUT | `/api/users/{userId}/camera-permissions/bulk` | Admin only |

---

## CameraAccessDto

```typescript
interface CameraAccessDto {
  id: string;          // access grant GUID
  cameraId: string;
  cameraName: string;
  userId: string;
  username: string;
  permission: 'View' | 'Control' | 'Record' | 'Admin';
  grantedAt: string;   // ISO timestamp
  grantedBy: string;   // username of admin who granted
  expiresAt: string | null;  // null = permanent
  isActive: boolean;
}
```

---

## GET /api/cameras/{cameraId}/access

List all explicit access grants for a camera.

### Response 200

```json
[
  {
    "id": "access-guid",
    "cameraId": "cam-guid",
    "cameraName": "Front Entrance",
    "userId": "user-guid",
    "username": "john_doe",
    "permission": "Control",
    "grantedAt": "2026-03-01T10:00:00Z",
    "grantedBy": "admin",
    "expiresAt": null,
    "isActive": true
  }
]
```

---

## POST /api/cameras/{cameraId}/access

Grant a user access to a camera. If user already has a grant for this camera, it will be updated (upsert).

### Request Body

```json
{
  "userId": "user-guid",
  "permission": "Control",
  "expiresAt": null
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| userId | string | Yes | User ID to grant access to |
| permission | string | Yes | `View` \| `Control` \| `Record` \| `Admin` |
| expiresAt | DateTime? | No | Optional expiry. null = permanent |

### Response 200 — Returns `CameraAccessDto`

### Errors

```
400 — { "message": "..." }  (invalid userId, etc.)
```

---

## PUT /api/cameras/{cameraId}/access/{accessId}

Update permission level on an existing grant.

### Request Body

```
"Record"
```

Plain string — just the new permission level.

### Response 200 — Returns updated `CameraAccessDto`

---

## DELETE /api/cameras/{cameraId}/access/{accessId}

Revoke a user's access to this camera. Viewer role users will no longer see this camera.

**Response: 204 No Content**

---

## GET /api/cameras/{cameraId}/access/my-permission

Check current user's effective permission on a camera. Use this to show/hide UI controls.

### Response 200

```json
{
  "cameraId": "cam-guid",
  "permission": "Control",
  "canView": true,
  "canControl": true,
  "canRecord": false,
  "canAdmin": false
}
```

### UI Permission Gate Example

```typescript
const { data: perm } = useQuery(
  ['permission', cameraId],
  () => api.get(`/api/cameras/${cameraId}/access/my-permission`).then(r => r.data)
);

return (
  <div>
    <LiveStream cameraId={cameraId} />
    {perm?.canControl && <PtzControls cameraId={cameraId} />}
    {perm?.canRecord  && <RecordButton cameraId={cameraId} />}
  </div>
);
```

---

## GET /api/users/{userId}/camera-permissions

Returns all camera permissions for a user — both inherited from role and explicit grants.

### Response 200

```json
{
  "userId": "user-guid",
  "username": "john_doe",
  "globalRole": "Viewer",
  "isAdmin": false,
  "cameraPermissions": [
    {
      "cameraId": "cam-guid-1",
      "cameraName": "Front Entrance",
      "permission": "Control",
      "isExplicit": true
    },
    {
      "cameraId": "cam-guid-2",
      "cameraName": "Server Room",
      "permission": "View",
      "isExplicit": true
    }
  ]
}
```

| Field | Notes |
|---|---|
| globalRole | The user's system-wide role: Admin \| Operator \| Viewer |
| isExplicit | true = explicit grant; false = inherited from Admin/Operator role |

---

## PUT /api/users/{userId}/camera-permissions/bulk

Set multiple camera permissions for a user in one call. Each item is upserted.

### Request Body

```json
[
  { "cameraId": "cam-guid-1", "cameraName": "", "permission": "Control", "isExplicit": true },
  { "cameraId": "cam-guid-2", "cameraName": "", "permission": "View",    "isExplicit": true },
  { "cameraId": "cam-guid-3", "cameraName": "", "permission": "Record",  "isExplicit": true }
]
```

### Response 200

```json
{ "message": "Updated 3 camera permissions" }
```

---

# 13. Analytics

NVR-specific analytics endpoints. Non-admin users see only their accessible cameras.

## Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| GET | `/api/analytics/summary` | Any authenticated |
| GET | `/api/analytics/cameras/{id}` | Any (View on camera) |
| GET | `/api/analytics/alerts/trend` | Any authenticated |
| GET | `/api/analytics/recordings/trend` | Any authenticated |
| GET | `/api/analytics/cameras/{id}/uptime` | Any (View on camera) |
| GET | `/api/analytics/cameras/{id}/heatmap` | Any (View on camera) |
| GET | `/api/analytics/viewers` | Any authenticated |
| GET | `/api/analytics/storage` | Admin or Operator |
| GET | `/api/analytics/events/recent` | Any authenticated |
| POST | `/api/analytics/events/acknowledge` | Any authenticated |
| GET | `/api/analytics/recordings/by-camera` | Any authenticated |
| GET | `/api/analytics/health` | Admin or Operator |
| GET | `/api/analytics/motion/summary` | Any authenticated |
| GET | `/api/analytics/bandwidth` | Any authenticated |

---

## GET /api/analytics/summary

Full NVR dashboard analytics summary.

### Response 200

```json
{
  "totalCameras": 8,
  "onlineCameras": 7,
  "offlineCameras": 1,
  "recordingCameras": 5,
  "systemUptimePercent": 87.5,

  "totalStorageBytes": 536870912000,
  "usedStorageBytes": 161061273600,
  "storageUsagePercent": 30.0,
  "storageBytesWrittenToday": 6291456000,
  "estimatedDaysRemaining": 60,

  "totalRecordingHoursToday": 40,
  "totalRecordingHoursWeek": 280,
  "totalRecordingsToday": 24,
  "activeRecordings": 5,

  "totalAlertsToday": 12,
  "unacknowledgedAlerts": 4,
  "motionEventsToday": 9,
  "cameraErrorsToday": 1,

  "activeLiveViewers": 3,
  "activePlaybackSessions": 1,
  "peakViewersToday": 7,

  "cameraBreakdown": [
    {
      "cameraId": "cam-guid",
      "cameraName": "Front Entrance",
      "status": "Online",
      "isRecording": true,
      "uptimePercent": 100.0,
      "recordingSeconds": 28800,
      "storageBytesUsed": 720000000,
      "motionEvents": 5,
      "activeViewers": 2,
      "lastMotionAt": "2026-03-01T13:45:00Z",
      "lastSeenAt": "2026-03-01T14:55:00Z",
      "avgBitrateKbps": 2200
    }
  ],
  "storageBreakdown": [
    {
      "profileId": "storage-guid",
      "profileName": "Local HDD",
      "type": "Local",
      "totalBytes": 536870912000,
      "usedBytes": 161061273600,
      "usagePercent": 30.0,
      "retentionDays": 30,
      "estimatedDaysRemaining": 60,
      "isHealthy": true,
      "bytesWrittenToday": 6291456000
    }
  ],
  "alertTrend": [
    { "hour": "2026-03-01T08:00:00Z", "motionCount": 2, "tamperCount": 0, "errorCount": 0, "totalCount": 2 }
  ],
  "recordingTrend": [
    { "hour": "2026-03-01T08:00:00Z", "recordingCount": 5, "totalSeconds": 18000, "bytesWritten": 450000000 }
  ]
}
```

---

## GET /api/analytics/cameras/{id}

Per-camera analytics for today.

### Response 200

```json
{
  "cameraId": "cam-guid",
  "cameraName": "Front Entrance",
  "status": "Online",
  "isRecording": true,
  "uptimePercent": 100.0,
  "recordingSeconds": 28800,
  "storageBytesUsed": 720000000,
  "motionEvents": 5,
  "activeViewers": 2,
  "lastMotionAt": "2026-03-01T13:45:00Z",
  "lastSeenAt": "2026-03-01T14:55:00Z",
  "avgBitrateKbps": 2200
}
```

---

## GET /api/analytics/alerts/trend

Hourly alert counts for charting.

### Query Parameters

| Param | Default | Notes |
|---|---|---|
| from | 7 days ago | Start of period |
| to | now | End of period |

### Response 200

```json
[
  { "hour": "2026-03-01T08:00:00Z", "motionCount": 3, "tamperCount": 0, "errorCount": 1, "totalCount": 4 },
  { "hour": "2026-03-01T09:00:00Z", "motionCount": 1, "tamperCount": 0, "errorCount": 0, "totalCount": 1 }
]
```

---

## GET /api/analytics/recordings/trend

Hourly recording statistics.

### Query Parameters — same as alerts/trend (from, to)

### Response 200

```json
[
  { "hour": "2026-03-01T08:00:00Z", "recordingCount": 5, "totalSeconds": 18000, "bytesWritten": 450000000 }
]
```

---

## GET /api/analytics/cameras/{id}/uptime

Uptime/downtime report with timeline slots.

### Query Parameters

| Param | Default |
|---|---|
| from | 30 days ago |
| to | now |

### Response 200

```json
{
  "cameraId": "cam-guid",
  "cameraName": "Front Entrance",
  "from": "2026-02-01T00:00:00Z",
  "to": "2026-03-01T23:59:59Z",
  "overallUptimePercent": 98.5,
  "totalDowntimeMinutes": 63,
  "totalDowntimeEvents": 3,
  "slots": [
    { "start": "2026-02-01T00:00:00Z", "end": "2026-02-15T10:30:00Z", "status": "Online" },
    { "start": "2026-02-15T10:30:00Z", "end": "2026-02-15T11:15:00Z", "status": "Offline" },
    { "start": "2026-02-15T11:15:00Z", "end": "2026-03-01T23:59:59Z", "status": "Online" }
  ]
}
```

---

## GET /api/analytics/cameras/{id}/heatmap

30-day recording activity per day. Use to build a GitHub-style contribution heatmap.

### Query Parameters

| Param | Default | Notes |
|---|---|---|
| days | 30 | Number of days to include |

### Response 200

```json
{
  "cameraId": "cam-guid",
  "cameraName": "Front Entrance",
  "days": [
    {
      "date": "2026-02-01T00:00:00Z",
      "bytesRecorded": 720000000,
      "recordingMinutes": 480,
      "motionEvents": 12,
      "hasRecording": true
    },
    {
      "date": "2026-02-02T00:00:00Z",
      "bytesRecorded": 0,
      "recordingMinutes": 0,
      "motionEvents": 0,
      "hasRecording": false
    }
  ]
}
```

---

## GET /api/analytics/viewers

Count of active live viewer SignalR sessions.

### Query Parameters

| Param | Notes |
|---|---|
| cameraId (optional) | Filter to specific camera. Null = system-wide total |

### Response 200

```json
{ "count": 5, "cameraId": null, "timestamp": "2026-03-01T14:55:00Z" }
```

---

## GET /api/analytics/storage

Storage usage breakdown across all enabled profiles.

### Response 200

```json
[
  {
    "profileId": "storage-guid",
    "profileName": "Local HDD",
    "type": "Local",
    "totalBytes": 536870912000,
    "usedBytes": 161061273600,
    "usagePercent": 30.0,
    "retentionDays": 30,
    "estimatedDaysRemaining": 60,
    "isHealthy": true,
    "bytesWrittenToday": 6291456000
  }
]
```

---

## GET /api/analytics/events/recent

Paginated recent event log with rich filtering.

### Query Parameters

| Param | Default | Notes |
|---|---|---|
| count | 100 | Max events to return |
| severity | — | `Info` \| `Warning` \| `Critical` |
| eventType | — | `Motion` \| `Tamper` \| `Online` \| `Offline` \| `Error` |
| unacknowledgedOnly | false | Show only unacknowledged events |

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
    "isAcknowledged": false,
    "acknowledgedAt": null,
    "acknowledgedBy": null
  }
]
```

---

## POST /api/analytics/events/acknowledge

Mark multiple events as acknowledged in one request.

### Request Body

```json
["event-guid-1", "event-guid-2", "event-guid-3"]
```

Array of event GUIDs.

### Response 200

```json
{ "acknowledgedCount": 3 }
```

---

## GET /api/analytics/recordings/by-camera

Recording statistics per camera for a date range.

### Query Parameters

| Param | Default | Notes |
|---|---|---|
| from | today 00:00 | Start of period |
| to | now | End of period |

### Response 200

```json
[
  {
    "cameraId": "cam-guid",
    "cameraName": "Front Entrance",
    "recordingCount": 8,
    "totalSeconds": 28800,
    "totalHours": 8.0,
    "totalSizeBytes": 720000000,
    "totalSizeGB": 0.671,
    "motionTriggered": 2,
    "scheduled": 6
  }
]
```

---

## GET /api/analytics/health

System health overview — unhealthy cameras and storage.

### Response 200

```json
{
  "overallStatus": "Degraded",
  "totalCameras": 8,
  "onlineCameras": 7,
  "offlineCameras": 1,
  "recordingCameras": 5,
  "unhealthyCameras": [
    {
      "id": "cam-guid",
      "name": "Back Yard",
      "status": "Offline",
      "lastError": "Connection refused",
      "lastSeenAt": "2026-03-01T10:00:00Z"
    }
  ],
  "storageProfiles": 1,
  "unhealthyStorage": [],
  "lowSpaceStorage": [],
  "checkedAt": "2026-03-01T14:55:00Z"
}
```

| overallStatus | Meaning |
|---|---|
| Healthy | All cameras online, all storage healthy |
| Degraded | At least one camera offline or storage unhealthy |

---

## GET /api/analytics/motion/summary

Motion activity breakdown — most active cameras.

### Query Parameters

| Param | Default | Notes |
|---|---|---|
| hours | 24 | Look-back period in hours |

### Response 200

```json
{
  "totalEvents": 45,
  "periodHours": 24,
  "mostActiveCamera": {
    "cameraId": "cam-guid",
    "cameraName": "Car Park"
  },
  "byCamera": [
    {
      "cameraId": "cam-guid",
      "cameraName": "Car Park",
      "count": 18,
      "lastAt": "2026-03-01T13:00:00Z"
    }
  ],
  "hourlyBreakdown": [
    { "hour": "2026-03-01T08:00:00Z", "count": 5 }
  ]
}
```

---

## GET /api/analytics/bandwidth

Estimated bitrate per camera based on recording chunks written in the last hour.

### Response 200

```json
[
  {
    "cameraId": "cam-guid",
    "cameraName": "Front Entrance",
    "chunkCount": 60,
    "totalBytes": 450000000,
    "avgBytesPerChunk": 7500000,
    "estimatedBitrateKbps": 2200
  }
]
```

---

## Analytics Service Examples

```typescript
// services/analytics.ts
import api from './client';

export const analyticsService = {
  getSummary: () =>
    api.get('/api/analytics/summary').then(r => r.data),

  getCamera: (id: string) =>
    api.get(`/api/analytics/cameras/${id}`).then(r => r.data),

  getAlertTrend: (from?: string, to?: string) =>
    api.get('/api/analytics/alerts/trend', { params: { from, to } }).then(r => r.data),

  getRecordingTrend: (from?: string, to?: string) =>
    api.get('/api/analytics/recordings/trend', { params: { from, to } }).then(r => r.data),

  getCameraUptime: (id: string, from?: string, to?: string) =>
    api.get(`/api/analytics/cameras/${id}/uptime`, { params: { from, to } }).then(r => r.data),

  getCameraHeatmap: (id: string, days = 30) =>
    api.get(`/api/analytics/cameras/${id}/heatmap`, { params: { days } }).then(r => r.data),

  getRecentEvents: (params: {
    count?: number;
    severity?: string;
    eventType?: string;
    unacknowledgedOnly?: boolean;
  }) =>
    api.get('/api/analytics/events/recent', { params }).then(r => r.data),

  acknowledgeEvents: (eventIds: string[]) =>
    api.post('/api/analytics/events/acknowledge', eventIds).then(r => r.data),

  getHealth: () =>
    api.get('/api/analytics/health').then(r => r.data),

  getMotionSummary: (hours = 24) =>
    api.get('/api/analytics/motion/summary', { params: { hours } }).then(r => r.data),

  getBandwidth: () =>
    api.get('/api/analytics/bandwidth').then(r => r.data)
};
```
