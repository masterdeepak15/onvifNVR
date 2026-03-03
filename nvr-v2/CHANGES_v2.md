# NVR Backend v2 — Changes & SignalR API Reference

## What Changed in v2

### 1. SignalR Authentication (ENFORCED)
```
[Authorize] attribute on NvrHub class — every connection requires valid JWT.

Connection:
  wss://host/hubs/nvr?access_token=<JWT>

The JWT is extracted from query string in JwtBearerEvents.OnMessageReceived.
Standard SignalR pattern: browsers cannot send custom headers on WS upgrade.
```

### 2. Role-Based Camera Access
```
Roles:
  Admin    → Full access to all cameras + all operations
  Operator → Default Control-level on all cameras
             (Record/Admin ops need explicit grant)
  Viewer   → Only cameras explicitly granted via CameraUserAccess table

Permission Levels (ordered):
  View    → Watch live + playback
  Control → View + PTZ + snapshot
  Record  → Control + start/stop recording
  Admin   → Record + edit/delete camera config

Every SignalR method checks permission before executing.
Every API controller checks permission before returning data.
```

### 3. Full NVR Stream Controls (SignalR)
All commands sent as `StreamControl(StreamControlCommand)`:

| Command     | Effect                               | Permission |
|-------------|--------------------------------------|------------|
| Play        | Start live stream or playback        | View       |
| Pause       | Freeze frame, stop frame delivery    | View       |
| Resume      | Continue from paused position        | View       |
| Stop        | End stream, clear session            | View       |
| SetSpeed    | Change playback speed (0.25–8x)     | View       |
| Seek        | Jump to timestamp in recording       | View       |
| ZoomIn      | Digital zoom in (client-side)        | View       |
| ZoomOut     | Digital zoom out                     | View       |
| ZoomReset   | Reset digital zoom                   | View       |
| GoLive      | Switch back to live from playback    | View       |

### 4. Full PTZ Commands (SignalR)
All commands sent as `PtzCommand(PtzCommandDto)`:

| Action         | Description                         | Permission |
|----------------|-------------------------------------|------------|
| MoveUp/Down/Left/Right | 4-direction continuous move  | Control    |
| MoveUpLeft/UpRight/DownLeft/DownRight | Diagonal | Control |
| ZoomIn/ZoomOut | Optical zoom in/out                  | Control    |
| Stop           | Stop all PTZ movement                | Control    |
| Home           | Go to home position (0,0,0)         | Control    |
| AbsoluteMove   | Move to absolute position            | Control    |
| RelativeMove   | Move relative to current position    | Control    |
| ContinuousMove | Continuous speed-based move          | Control    |
| GoToPreset     | Jump to saved preset                 | Control    |
| SavePreset     | Save current position as preset      | Control    |
| DeletePreset   | Delete a preset                      | Control    |
| FocusNear/Far/Auto | Focus control                    | Control    |
| IrisOpen/Close/Auto | Iris control                    | Control    |

### 5. Analytics (NVR-specific)
New `/api/analytics/` endpoints:

| Endpoint                              | Description                      |
|---------------------------------------|----------------------------------|
| GET /api/analytics/summary            | Full NVR dashboard summary       |
| GET /api/analytics/cameras/{id}       | Per-camera analytics             |
| GET /api/analytics/alerts/trend       | Hourly alert trend chart data    |
| GET /api/analytics/recordings/trend   | Hourly recording trend data      |
| GET /api/analytics/cameras/{id}/uptime | Uptime/downtime report          |
| GET /api/analytics/cameras/{id}/heatmap | Recording heatmap (30 days)    |
| GET /api/analytics/viewers            | Active live viewer count         |
| GET /api/analytics/storage            | Storage usage per profile        |
| GET /api/analytics/events/recent      | Recent event log with filters    |
| POST /api/analytics/events/acknowledge | Bulk acknowledge events         |
| GET /api/analytics/recordings/by-camera | Recording stats per camera     |
| GET /api/analytics/health             | System health report             |
| GET /api/analytics/motion/summary     | Motion activity summary          |
| GET /api/analytics/bandwidth          | Bitrate stats per camera         |

---

## Full SignalR API Reference

### CLIENT → SERVER Methods

```typescript
// Connect
const connection = new HubConnectionBuilder()
  .withUrl("/hubs/nvr?access_token=" + jwtToken)
  .withAutomaticReconnect()
  .build();

await connection.start();
```

#### Stream Subscriptions
```typescript
// Subscribe to live camera feed
await connection.invoke("SubscribeToCamera", cameraId: string);

// Unsubscribe
await connection.invoke("UnsubscribeFromCamera", cameraId: string);

// Subscribe to system events (alerts, status changes, analytics updates)
await connection.invoke("SubscribeToEvents");

// Request a single snapshot (latest JPEG frame)
await connection.invoke("RequestSnapshot", cameraId: string);
```

#### Stream Controls
```typescript
// Play live stream
await connection.invoke("StreamControl", {
  CameraId: cameraId,
  Command: "Play"
});

// Play recording from timestamp
await connection.invoke("StreamControl", {
  CameraId: cameraId,
  Command: "Play",
  SeekTo: "2024-01-15T14:30:00Z",
  Speed: 1.0
});

// Pause
await connection.invoke("StreamControl", { CameraId: cameraId, Command: "Pause" });

// Resume from pause
await connection.invoke("StreamControl", { CameraId: cameraId, Command: "Resume" });

// Stop
await connection.invoke("StreamControl", { CameraId: cameraId, Command: "Stop" });

// Set playback speed (0.25, 0.5, 1, 2, 4, 8)
await connection.invoke("StreamControl", { CameraId: cameraId, Command: "SetSpeed", Speed: 2.0 });

// Seek to timestamp
await connection.invoke("StreamControl", {
  CameraId: cameraId,
  Command: "Seek",
  SeekTo: "2024-01-15T14:45:00Z"
});

// Digital zoom (UI applies CSS transform)
await connection.invoke("StreamControl", { CameraId: cameraId, Command: "ZoomIn" });
await connection.invoke("StreamControl", { CameraId: cameraId, Command: "ZoomOut" });
await connection.invoke("StreamControl", { CameraId: cameraId, Command: "ZoomReset" });

// Switch back to live from playback
await connection.invoke("StreamControl", { CameraId: cameraId, Command: "GoLive" });
```

#### Recording Control
```typescript
await connection.invoke("StartRecording", cameraId);
await connection.invoke("StopRecording", cameraId);
```

#### PTZ Control
```typescript
// Directional move (continuous)
await connection.invoke("PtzCommand", { CameraId: cameraId, Action: "MoveUp", Speed: 0.5 });
await connection.invoke("PtzCommand", { CameraId: cameraId, Action: "Stop" });

// Zoom
await connection.invoke("PtzCommand", { CameraId: cameraId, Action: "ZoomIn", Speed: 0.3 });

// Go to preset
await connection.invoke("PtzCommand", {
  CameraId: cameraId,
  Action: "GoToPreset",
  PresetToken: "001"
});

// Save current position as preset
await connection.invoke("PtzCommand", {
  CameraId: cameraId,
  Action: "SavePreset",
  PresetName: "Entrance View"
});

// Absolute move (all values -1.0 to 1.0)
await connection.invoke("PtzCommand", {
  CameraId: cameraId,
  Action: "AbsoluteMove",
  Pan: 0.2,
  Tilt: -0.1,
  Zoom: 0.5
});

// Get current PTZ status + presets
await connection.invoke("GetPtzStatus", cameraId);
```

#### Alerts
```typescript
await connection.invoke("AcknowledgeAlert", alertId);
```

---

### SERVER → CLIENT Events

```typescript
// Live frame (JPEG as base64)
connection.on("CameraFrame", (payload) => {
  // payload: { cameraId, frame: "base64...", timestampMs, width, height, fps, streamState }
  img.src = `data:image/jpeg;base64,${payload.frame}`;
});

// Stream state changes
connection.on("StreamState", (state) => {
  // state: { cameraId, state: "Live|Playing|Paused|Stopped|Error", isLive, speed, playbackPosition }
});

// Playback position updates (while playing recording)
connection.on("PlaybackPosition", (pos) => {
  // pos: { cameraId, position: Date, speed }
  updateTimeline(pos.position);
});

// Zoom change (digital zoom — client applies CSS transform)
connection.on("ZoomChanged", (zoom) => {
  // zoom: { cameraId, zoomDelta, reset }
});

// Seek completed
connection.on("SeekComplete", (data) => {
  // data: { cameraId, position }
});

// Playback ready (recording found for timestamp)
connection.on("PlaybackReady", (data) => {
  // data: { cameraId, startPosition, chunkCount, speed }
});

// Camera status (online/offline/recording changes)
connection.on("CameraStatus", (status) => {
  // status: { cameraId, status, isOnline, isRecording, lastSeenAt, activeViewers }
});

// Recording status changes
connection.on("RecordingStatus", (rec) => {
  // rec: { cameraId, recordingId, status, chunkNumber, fileSizeBytes, timestamp }
  // status: Started | Stopped | Chunk | Error
});

// PTZ feedback after command
connection.on("PtzFeedback", (fb) => {
  // fb: { cameraId, action, success, error?, pan, tilt, zoom, moveStatus }
});

// PTZ status response
connection.on("PtzStatus", (status) => {
  // status: { cameraId, pan, tilt, zoom, moveStatus, presets: [{id, name, token}] }
});

// Preset saved confirmation
connection.on("PresetSaved", (preset) => {
  // preset: { id, name, onvifToken, panPosition, tiltPosition, zoomPosition }
});

// Single snapshot response
connection.on("Snapshot", (snap) => {
  // snap: { cameraId, frame: "base64...", timestamp }
});

// System-wide alert
connection.on("Alert", (alert) => {
  // alert: { alertId, cameraId, cameraName, type, message, severity, timestamp, snapshotBase64? }
});

// Storage space warning
connection.on("StorageAlert", (sa) => {
  // sa: { storageProfileId, usagePercent, message, severity, timestamp }
});

// Periodic analytics push (every hour)
connection.on("AnalyticsUpdate", (update) => {
  // update: { onlineCameras, recordingCameras, activeViewers, storageUsagePercent, unacknowledgedAlerts }
});

// Access denied
connection.on("AccessDenied", (denial) => {
  // denial: { cameraId, requiredPermission, message }
});

// Error
connection.on("Error", (err) => {
  // err: { cameraId?, message }
});
connection.on("StreamError", (err) => {
  // err: { cameraId, error, timestamp }
});

// Alert acknowledged (broadcast to all)
connection.on("AlertAcknowledged", (ack) => {
  // ack: { alertId, acknowledgedBy }
});

// Initial state on connect
connection.on("SystemState", (state) => {
  // state: { cameras: [...], serverTime }
});

// User-targeted notification
connection.on("UserNotification", (notif) => {
  // notif: { message, severity, timestamp }
});
```

---

## Camera Access API

```
# Grant user access to camera
POST /api/cameras/{cameraId}/access
Body: { "userId": "...", "permission": "Control", "expiresAt": null }

# List who has access to a camera
GET  /api/cameras/{cameraId}/access

# Update permission
PUT  /api/cameras/{cameraId}/access/{accessId}
Body: "Record"

# Revoke access
DELETE /api/cameras/{cameraId}/access/{accessId}

# Check my permission on a camera
GET /api/cameras/{cameraId}/access/my-permission

# Get all camera permissions for a user (Admin)
GET /api/users/{userId}/camera-permissions

# Bulk update camera permissions for a user (Admin)
PUT /api/users/{userId}/camera-permissions/bulk

# Get only MY accessible cameras (any role)
GET /api/my/cameras
```
