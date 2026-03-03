# NVR API Reference — Part 4 of 4
## SignalR WebSocket Hub — Real-Time Streaming & Control

---

# SignalR Hub Overview

The hub is the core of all real-time NVR operations: live video frames, playback control, PTZ joystick, recording control, and event feeds — all over a single persistent WebSocket connection.

## Connection

| Key | Value |
|---|---|
| URL | `wss://host/hubs/nvr?access_token=<JWT>` |
| Auth | JWT passed as query string. Connection is **rejected** if missing or expired. |
| Library | `npm install @microsoft/signalr` |
| Max Message | 10 MB |
| Keep-Alive | Every 15 seconds |
| Client Timeout | 30 seconds |

> **Why query string?** Browsers cannot send custom headers during a WebSocket upgrade request. The server reads the token from `?access_token=` and validates it as a normal JWT.

---

## Connection Setup

```typescript
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export function createNvrConnection(token: string) {
  const connection = new HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_API_URL}/hubs/nvr?access_token=${token}`)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // retry after ms
    .configureLogging(LogLevel.Warning)
    .build();

  connection.onreconnecting(() => console.log('SignalR: reconnecting...'));
  connection.onreconnected(() => {
    console.log('SignalR: reconnected');
    resubscribeAllCameras(); // Re-subscribe after reconnect
  });
  connection.onclose(() => console.log('SignalR: disconnected'));

  return connection;
}

// Start
const conn = createNvrConnection(accessToken);
await conn.start();
console.log('Connected. ID:', conn.connectionId);
```

---

## On Connect — SystemState Event

Immediately after connecting, the server sends the current system state.

```typescript
connection.on('SystemState', (state: {
  cameras: Array<{
    id: string;
    name: string;
    status: string;
    isOnline: boolean;
    isRecording: boolean;
    lastSeenAt: string | null;
  }>;
  serverTime: string;
}) => {
  initializeCameraList(state.cameras);
});
```

---

# Client → Server Methods

Call with `connection.invoke('MethodName', ...args)`.

## Method Summary

| Method | Minimum Permission |
|---|---|
| `SubscribeToCamera(cameraId)` | View |
| `UnsubscribeFromCamera(cameraId)` | View |
| `SubscribeToEvents()` | Any |
| `StreamControl(cmd)` | View |
| `StartRecording(cameraId)` | Record |
| `StopRecording(cameraId)` | Record |
| `PtzCommand(cmd)` | Control |
| `GetPtzStatus(cameraId)` | View |
| `RequestSnapshot(cameraId)` | View |
| `AcknowledgeAlert(alertId)` | View |

---

## SubscribeToCamera

Start receiving live JPEG frames from a camera. Server pushes `CameraFrame` events every ~66ms (≈15 fps). Multiple cameras can be subscribed simultaneously.

```typescript
await connection.invoke('SubscribeToCamera', cameraId);

connection.on('CameraFrame', (payload) => {
  const img = document.getElementById(`cam-${payload.cameraId}`) as HTMLImageElement;
  if (img) img.src = `data:image/jpeg;base64,${payload.frame}`;
});
```

**Server also sends `StreamState`** with `state: "Live"` immediately after subscribe.

---

## UnsubscribeFromCamera

```typescript
await connection.invoke('UnsubscribeFromCamera', cameraId);
```

---

## SubscribeToEvents

Subscribe to system-wide events: alerts, camera status changes, analytics updates, storage alerts.

```typescript
await connection.invoke('SubscribeToEvents');
```

Call this once after connecting to start receiving `Alert`, `CameraStatus`, `AnalyticsUpdate`, `StorageAlert` events.

---

## StreamControl

Universal NVR stream control. All playback commands use this single method.

### StreamControlCommand

```typescript
interface StreamControlCommand {
  cameraId: string;     // GUID — required
  command:  string;     // required — see commands below
  speed?:   number;     // for SetSpeed: 0.25 | 0.5 | 1.0 | 2.0 | 4.0 | 8.0
  seekTo?:  string;     // for Seek/Play: ISO UTC timestamp
  zoomLevel?: number;   // for zoom: 0.0–1.0
}
```

### Commands

| Command | Description |
|---|---|
| `Play` | Start live stream. If `seekTo` is provided, start playback from that timestamp. |
| `Pause` | Freeze last frame. Frame delivery stops; client holds the last received image. |
| `Resume` | Continue from where paused. |
| `Stop` | End stream entirely. Cleans up server resources. |
| `SetSpeed` | Change playback speed. Set `speed` field. Valid: 0.25, 0.5, 1.0, 2.0, 4.0, 8.0. |
| `Seek` | Jump to timestamp in recording. Set `seekTo` field. |
| `ZoomIn` | Digital zoom in (delta +0.1). Client applies CSS scale transform. |
| `ZoomOut` | Digital zoom out (delta -0.1). |
| `ZoomReset` | Reset digital zoom to 1×. |
| `GoLive` | Switch from playback back to live stream. |

### Examples

```typescript
const base = { CameraId: cameraId };

// Start live
await conn.invoke('StreamControl', { ...base, Command: 'Play' });

// Start recording playback from timestamp
await conn.invoke('StreamControl', {
  ...base,
  Command: 'Play',
  SeekTo: '2026-03-01T14:30:00Z',
  Speed: 1.0
});

// Pause (freeze frame)
await conn.invoke('StreamControl', { ...base, Command: 'Pause' });

// Resume after pause
await conn.invoke('StreamControl', { ...base, Command: 'Resume' });

// Stop
await conn.invoke('StreamControl', { ...base, Command: 'Stop' });

// Set speed to 2x
await conn.invoke('StreamControl', { ...base, Command: 'SetSpeed', Speed: 2.0 });

// Seek to specific time
await conn.invoke('StreamControl', {
  ...base, Command: 'Seek', SeekTo: '2026-03-01T15:00:00Z'
});

// Digital zoom
await conn.invoke('StreamControl', { ...base, Command: 'ZoomIn' });
await conn.invoke('StreamControl', { ...base, Command: 'ZoomOut' });
await conn.invoke('StreamControl', { ...base, Command: 'ZoomReset' });

// Return to live from playback
await conn.invoke('StreamControl', { ...base, Command: 'GoLive' });
```

---

## StartRecording / StopRecording

**Requires `Record` permission on camera.**

```typescript
await conn.invoke('StartRecording', cameraId);
await conn.invoke('StopRecording', cameraId);
```

Server sends `RecordingStatus` and broadcasts `CameraStatus` to all viewers of that camera.

---

## PtzCommand

Real-time PTZ control. All PTZ operations use this single method.

### PtzCommandDto

```typescript
interface PtzCommandDto {
  cameraId:    string;   // GUID — required
  action:      string;   // required — see actions below
  speed:       number;   // movement speed 0.0–1.0 — default: 0.5
  pan:         number;   // -1.0 to 1.0 (for AbsoluteMove/RelativeMove/ContinuousMove)
  tilt:        number;   // -1.0 to 1.0
  zoom:        number;   // 0.0 to 1.0
  presetToken?: string;  // ONVIF preset token (GoToPreset, DeletePreset)
  presetName?:  string;  // name for SavePreset
}
```

### PTZ Actions

| Action | Description |
|---|---|
| `MoveUp` | Continuous tilt up. Call `Stop` on pointer-up. |
| `MoveDown` | Continuous tilt down. |
| `MoveLeft` | Continuous pan left. |
| `MoveRight` | Continuous pan right. |
| `MoveUpLeft` | Continuous diagonal up-left. |
| `MoveUpRight` | Continuous diagonal up-right. |
| `MoveDownLeft` | Continuous diagonal down-left. |
| `MoveDownRight` | Continuous diagonal down-right. |
| `ZoomIn` | Continuous optical zoom in. |
| `ZoomOut` | Continuous optical zoom out. |
| `Stop` | Stop ALL movement immediately. Always call this on joystick release. |
| `Home` | Move to home position (0, 0, 0). |
| `AbsoluteMove` | Move to exact position. Set `pan`, `tilt`, `zoom` (-1.0 to 1.0). |
| `RelativeMove` | Move by delta from current position. Set `pan`, `tilt`, `zoom`. |
| `ContinuousMove` | Raw continuous move with velocity. Set `pan`, `tilt`, `zoom`. |
| `GoToPreset` | Jump to saved preset. Set `presetToken` (ONVIF token string). |
| `SavePreset` | Save current position. Set `presetName` (display name). |
| `DeletePreset` | Delete preset. Set `presetToken`. |
| `FocusNear` | Focus to near (camera-dependent). |
| `FocusFar` | Focus to far. |
| `FocusAuto` | Enable auto-focus. |
| `IrisOpen` | Open iris (camera-dependent). |
| `IrisClose` | Close iris. |
| `IrisAuto` | Enable auto iris. |

### Joystick / D-Pad Pattern

```typescript
// Move while button held, stop when released
const ptzDown = async (action: string) => {
  await conn.invoke('PtzCommand', {
    CameraId: cameraId,
    Action: action,
    Speed: 0.5
  });
};

const ptzUp = async () => {
  await conn.invoke('PtzCommand', { CameraId: cameraId, Action: 'Stop' });
};

// JSX usage:
// <button
//   onPointerDown={() => ptzDown('MoveUp')}
//   onPointerUp={ptzUp}
//   onPointerLeave={ptzUp}
// >▲</button>
```

### Preset Commands

```typescript
// Go to preset
await conn.invoke('PtzCommand', {
  CameraId: cameraId,
  Action: 'GoToPreset',
  PresetToken: '001'   // ONVIF token from PtzPresetDto.onvifToken
});

// Save current position as preset
await conn.invoke('PtzCommand', {
  CameraId: cameraId,
  Action: 'SavePreset',
  PresetName: 'Entrance View'
});

// Delete preset
await conn.invoke('PtzCommand', {
  CameraId: cameraId,
  Action: 'DeletePreset',
  PresetToken: '001'
});
```

Server sends `PtzFeedback` after every `PtzCommand`.

---

## GetPtzStatus

Request current PTZ position and preset list for a camera.

```typescript
await conn.invoke('GetPtzStatus', cameraId);
// Server sends 'PtzStatus' event in response
```

---

## RequestSnapshot

Request a single JPEG frame.

```typescript
await conn.invoke('RequestSnapshot', cameraId);
// Server sends 'Snapshot' event in response
```

---

## AcknowledgeAlert

```typescript
await conn.invoke('AcknowledgeAlert', alertId);
// Server broadcasts 'AlertAcknowledged' to all event subscribers
```

---

# Server → Client Events

Register handlers with `connection.on('EventName', handler)`. Register **before** calling `invoke`.

## Event Summary

| Event | Triggered When |
|---|---|
| `SystemState` | Immediately on connect — current camera list + server time |
| `CameraFrame` | Every ~66ms per subscribed camera — live JPEG |
| `StreamState` | After any StreamControl command or state change |
| `PlaybackPosition` | Every second during playback — current timestamp |
| `PlaybackReady` | Recording found at requested seek position |
| `SeekComplete` | After seek finishes |
| `ZoomChanged` | After ZoomIn/ZoomOut/ZoomReset |
| `CameraStatus` | Camera goes online/offline/starts-stops recording |
| `RecordingStatus` | Recording started/stopped/chunk written |
| `PtzFeedback` | After every PtzCommand — success or error + new position |
| `PtzStatus` | Response to GetPtzStatus — current position + presets |
| `PresetSaved` | After SavePreset completes |
| `Snapshot` | Response to RequestSnapshot |
| `Alert` | New motion/tamper/error event (requires SubscribeToEvents) |
| `StorageAlert` | Storage space warning or error |
| `AnalyticsUpdate` | Hourly metrics push (requires SubscribeToEvents) |
| `AlertAcknowledged` | When any alert is acknowledged |
| `AccessDenied` | Permission check failed on a camera operation |
| `UserNotification` | Server-to-current-user targeted notification |
| `Error` | General error message |
| `StreamError` | Stream-specific error (e.g. RTSP disconnected) |

---

## CameraFrame Payload

Most frequent event — one per frame per subscribed camera.

```typescript
connection.on('CameraFrame', (payload: {
  cameraId:    string;
  frame:       string;        // base64-encoded JPEG bytes
  timestampMs: number;        // Unix milliseconds
  width:       number;        // frame width px
  height:      number;        // frame height px
  fps:         number;        // current fps
  isKeyframe:  boolean;
  streamState: 'Live' | 'Playback';
}) => {
  const img = cameraRefs[payload.cameraId].current;
  if (img) img.src = `data:image/jpeg;base64,${payload.frame}`;
});
```

---

## StreamState Payload

Sent after every StreamControl command and on state changes.

```typescript
connection.on('StreamState', (state: {
  cameraId:          string;
  state:             'Live' | 'Playing' | 'Paused' | 'Stopped' | 'Buffering' | 'Error' | 'NoRecording';
  isLive:            boolean;
  speed:             number;          // 0.25 to 8.0
  zoomLevel:         number;          // 0.0 to 1.0
  playbackPosition?: string;          // ISO timestamp — null when live
  errorMessage?:     string;
  fps:               number;
  bitrateKbps:       number;
  bufferedMs:        number;
}) => {
  updateStreamUI(state);
});
```

---

## PlaybackPosition Payload

Sent every second during recording playback.

```typescript
connection.on('PlaybackPosition', (pos: {
  cameraId: string;
  position: string;   // ISO UTC timestamp of current playback position
  speed:    number;
}) => {
  setTimelineScrubber(new Date(pos.position).getTime());
});
```

---

## PlaybackReady Payload

Sent when recording is found at the requested seek timestamp.

```typescript
connection.on('PlaybackReady', (info: {
  cameraId:       string;
  startPosition:  string;    // ISO timestamp
  chunkCount:     number;
  firstChunkPath: string;
  speed:          number;
}) => {
  showPlaybackReady(info);
});
```

---

## SeekComplete Payload

```typescript
connection.on('SeekComplete', (result: {
  cameraId: string;
  position: string;   // ISO timestamp seeked to
}) => {
  updateScrubber(result.position);
});
```

---

## CameraStatus Payload

Pushed by background health monitor every 5 minutes and immediately when recording starts/stops.

```typescript
connection.on('CameraStatus', (status: {
  cameraId:      string;
  status:        'Online' | 'Offline' | 'Error';
  isOnline:      boolean;
  isRecording:   boolean;
  lastSeenAt?:   string;
  lastError?:    string;
  activeViewers: number;
}) => {
  updateCameraCard(status);
});
```

---

## RecordingStatus Payload

```typescript
connection.on('RecordingStatus', (rec: {
  cameraId:         string;
  recordingId?:     string;
  status:           'Started' | 'Stopped' | 'Chunk' | 'Error';
  chunkNumber?:     number;    // which 60s chunk just completed
  fileSizeBytes?:   number;    // size of completed chunk
  durationSeconds?: number;
  timestamp:        string;
}) => {
  if (rec.status === 'Started') showRecordingBadge(rec.cameraId);
  if (rec.status === 'Stopped') hideRecordingBadge(rec.cameraId);
});
```

---

## PtzFeedback Payload

Returned after every `PtzCommand` call.

```typescript
connection.on('PtzFeedback', (fb: {
  cameraId:   string;
  action:     string;
  success:    boolean;
  error?:     string;
  pan:        number;        // current pan position after command
  tilt:       number;        // current tilt position
  zoom:       number;        // current zoom position
  moveStatus: 'Idle' | 'Moving' | 'Unknown' | 'NotSupported';
}) => {
  if (!fb.success) toast.error(`PTZ failed: ${fb.error}`);
  setPtzPosition({ pan: fb.pan, tilt: fb.tilt, zoom: fb.zoom });
});
```

---

## PtzStatus Payload

Response to `GetPtzStatus` call.

```typescript
connection.on('PtzStatus', (status: {
  cameraId:      string;
  pan:           number;
  tilt:          number;
  zoom:          number;
  moveStatus:    'Idle' | 'Moving' | 'Unknown' | 'NotSupported';
  supportsFocus: boolean;
  supportsIris:  boolean;
  presets: Array<{
    id:           string;
    name:         string;
    onvifToken:   string;
    panPosition:  number | null;
    tiltPosition: number | null;
    zoomPosition: number | null;
  }>;
  lastUpdated: string;
}) => {
  setPresets(status.presets);
  setPtzPosition({ pan: status.pan, tilt: status.tilt, zoom: status.zoom });
});
```

---

## PresetSaved Payload

```typescript
connection.on('PresetSaved', (preset: {
  id:           string;
  name:         string;
  onvifToken:   string;
  panPosition:  number;
  tiltPosition: number;
  zoomPosition: number;
}) => {
  addPresetToList(preset);
});
```

---

## Snapshot Payload

Response to `RequestSnapshot` call.

```typescript
connection.on('Snapshot', (snap: {
  cameraId:  string;
  frame:     string;    // base64 JPEG
  timestamp: string;
}) => {
  downloadSnapshot(snap.frame);
});
```

---

## Alert Payload

System-wide events. Requires `SubscribeToEvents()`.

```typescript
connection.on('Alert', (alert: {
  alertId:         string;
  cameraId?:       string;
  cameraName?:     string;
  type:            'Motion' | 'Tamper' | 'CameraOffline' | 'StorageError' | 'System';
  message:         string;
  severity:        'Info' | 'Warning' | 'Critical';
  timestamp:       string;
  snapshotBase64?: string;   // JPEG snapshot at time of alert (if available)
}) => {
  addToAlertFeed(alert);
  if (alert.severity === 'Critical') showBannerAlert(alert);
});
```

---

## StorageAlert Payload

```typescript
connection.on('StorageAlert', (sa: {
  storageProfileId: string;
  usagePercent:     number;
  message:          string;
  severity:         'Warning' | 'Critical';
  timestamp:        string;
}) => {
  if (sa.usagePercent > 95) showCriticalStorageWarning(sa);
  else showStorageWarning(sa);
});
```

---

## AnalyticsUpdate Payload

Pushed every hour by the background service to all event subscribers.

```typescript
connection.on('AnalyticsUpdate', (update: {
  onlineCameras:        number;
  recordingCameras:     number;
  activeViewers:        number;
  storageUsagePercent:  number;
  unacknowledgedAlerts: number;
  timestamp:            string;
}) => {
  setDashboardStats(update);
});
```

---

## AlertAcknowledged Payload

Broadcast to all event subscribers when any alert is acknowledged.

```typescript
connection.on('AlertAcknowledged', (ack: {
  alertId:        string;
  acknowledgedBy: string;
}) => {
  markAlertAcknowledged(ack.alertId);
});
```

---

## AccessDenied Payload

Returned to caller when a camera operation fails the permission check.

```typescript
connection.on('AccessDenied', (denied: {
  cameraId:           string;
  requiredPermission: string;
  message:            string;
}) => {
  toast.error(`Access denied: ${denied.message}`);
});
```

---

## ZoomChanged Payload

Returned after ZoomIn/ZoomOut/ZoomReset commands. Apply client-side with CSS transform.

```typescript
connection.on('ZoomChanged', (zoom: {
  cameraId:  string;
  zoomDelta: number;   // positive = zoom in, negative = zoom out, 0 = reset
  reset:     boolean;  // true = reset to 1x
}) => {
  if (zoom.reset) {
    setCameraZoom(zoom.cameraId, 1.0);
  } else {
    setZoom(prev => Math.max(1.0, Math.min(8.0, prev + zoom.zoomDelta * 4)));
  }
});

// Apply in JSX:
// <img style={{ transform: `scale(${zoomLevel})`, transformOrigin: 'center' }} />
```

---

## Error / StreamError Payloads

```typescript
connection.on('Error', (err: { cameraId?: string; message: string }) => {
  console.error('Hub error:', err.message);
});

connection.on('StreamError', (err: {
  cameraId:  string;
  error:     string;
  timestamp: string;
}) => {
  showStreamError(err.cameraId, err.error);
  // Optionally auto-retry subscribe
});
```

---

## UserNotification Payload

Targeted to the current user's connection only (not broadcast).

```typescript
connection.on('UserNotification', (notif: {
  message:   string;
  severity:  'Info' | 'Warning' | 'Critical';
  timestamp: string;
}) => {
  showToast(notif.message, notif.severity);
});
```

---

# Complete Integration Pattern

```typescript
// hooks/useNvrHub.ts
import { useEffect, useRef, useCallback } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';

export function useNvrHub(accessToken: string | null) {
  const connRef = useRef<any>(null);

  useEffect(() => {
    if (!accessToken) return;

    const c = new HubConnectionBuilder()
      .withUrl(`/hubs/nvr?access_token=${accessToken}`)
      .withAutomaticReconnect()
      .build();

    // Register ALL handlers before starting
    c.on('SystemState',      state  => store.dispatch(setSystemState(state)));
    c.on('CameraFrame',      frame  => store.dispatch(updateFrame(frame)));
    c.on('StreamState',      s      => store.dispatch(setStreamState(s)));
    c.on('PlaybackPosition', pos    => store.dispatch(setPlaybackPos(pos)));
    c.on('CameraStatus',     status => store.dispatch(updateCameraStatus(status)));
    c.on('RecordingStatus',  rec    => store.dispatch(updateRecordingStatus(rec)));
    c.on('PtzFeedback',      fb     => store.dispatch(updatePtzFeedback(fb)));
    c.on('PtzStatus',        s      => store.dispatch(setPtzStatus(s)));
    c.on('Alert',            alert  => store.dispatch(addAlert(alert)));
    c.on('StorageAlert',     sa     => store.dispatch(addStorageAlert(sa)));
    c.on('AnalyticsUpdate',  upd    => store.dispatch(updateAnalytics(upd)));
    c.on('AccessDenied',     d      => toast.error(d.message));
    c.on('Error',            e      => console.error('Hub:', e.message));
    c.on('StreamError',      e      => store.dispatch(setStreamError(e)));

    c.start().then(() => {
      connRef.current = c;
      c.invoke('SubscribeToEvents'); // receive alerts + analytics
    });

    return () => { c.stop(); };
  }, [accessToken]);

  const subscribe = useCallback((cameraId: string) =>
    connRef.current?.invoke('SubscribeToCamera', cameraId), []);

  const unsubscribe = useCallback((cameraId: string) =>
    connRef.current?.invoke('UnsubscribeFromCamera', cameraId), []);

  const streamControl = useCallback((cmd: object) =>
    connRef.current?.invoke('StreamControl', cmd), []);

  const ptzCommand = useCallback((cmd: object) =>
    connRef.current?.invoke('PtzCommand', cmd), []);

  const getSnapshot = useCallback((cameraId: string) =>
    connRef.current?.invoke('RequestSnapshot', cameraId), []);

  const getPtzStatus = useCallback((cameraId: string) =>
    connRef.current?.invoke('GetPtzStatus', cameraId), []);

  return { subscribe, unsubscribe, streamControl, ptzCommand, getSnapshot, getPtzStatus };
}
```

---

# Complete API Quick Reference

All REST + SignalR methods in one table.

## REST Endpoints

| Method | Endpoint | Auth |
|---|---|---|
| POST | `/api/auth/login` | Public |
| POST | `/api/auth/refresh` | Public |
| POST | `/api/auth/register` | Admin |
| POST | `/api/auth/logout` | Any |
| GET | `/api/auth/me` | Any |
| GET | `/api/users` | Admin |
| PUT | `/api/users/{id}` | Admin |
| DELETE | `/api/users/{id}` | Admin |
| GET | `/api/cameras` | Any |
| GET | `/api/cameras/{id}` | Any |
| POST | `/api/cameras` | Admin/Op |
| PUT | `/api/cameras/{id}` | Admin/Op |
| DELETE | `/api/cameras/{id}` | Admin |
| GET | `/api/cameras/discover` | Admin/Op |
| POST | `/api/cameras/{id}/recording/start` | Admin/Op |
| POST | `/api/cameras/{id}/recording/stop` | Admin/Op |
| GET | `/api/cameras/{id}/snapshot` | Any |
| POST | `/api/cameras/{id}/ptz/move` | Admin/Op |
| POST | `/api/cameras/{id}/ptz/stop` | Admin/Op |
| GET | `/api/cameras/{id}/ptz/presets` | Any |
| POST | `/api/cameras/{id}/ptz/presets/{pid}/goto` | Admin/Op |
| POST | `/api/cameras/{id}/ptz/presets` | Admin/Op |
| DELETE | `/api/cameras/{id}/ptz/presets/{pid}` | Admin/Op |
| GET | `/api/cameras/{id}/access` | Admin |
| POST | `/api/cameras/{id}/access` | Admin |
| PUT | `/api/cameras/{id}/access/{aid}` | Admin |
| DELETE | `/api/cameras/{id}/access/{aid}` | Admin |
| GET | `/api/cameras/{id}/access/my-permission` | Any |
| GET | `/api/users/{uid}/camera-permissions` | Admin |
| PUT | `/api/users/{uid}/camera-permissions/bulk` | Admin |
| GET | `/api/my/cameras` | Any |
| GET | `/api/my/layout` | Any |
| GET | `/api/recordings` | Any |
| GET | `/api/recordings/{id}` | Any |
| POST | `/api/recordings/playback` | Any |
| GET | `/api/recordings/{id}/stream` | Any |
| DELETE | `/api/recordings/{id}` | Admin/Op |
| GET | `/api/storage` | Admin |
| GET | `/api/storage/types` | Admin |
| POST | `/api/storage` | Admin |
| POST | `/api/storage/{id}/test` | Admin |
| DELETE | `/api/storage/{id}` | Admin |
| GET | `/api/layout` | Any |
| POST | `/api/layout` | Any |
| GET | `/api/layout/names` | Any |
| GET | `/api/cameras/{id}/schedules` | Any |
| POST | `/api/cameras/{id}/schedules` | Admin/Op |
| DELETE | `/api/cameras/{id}/schedules/{sid}` | Admin/Op |
| GET | `/api/dashboard` | Any |
| GET | `/api/dashboard/events` | Any |
| GET | `/api/settings` | Admin |
| PUT | `/api/settings` | Admin |
| GET | `/api/analytics/summary` | Any |
| GET | `/api/analytics/cameras/{id}` | Any |
| GET | `/api/analytics/alerts/trend` | Any |
| GET | `/api/analytics/recordings/trend` | Any |
| GET | `/api/analytics/cameras/{id}/uptime` | Any |
| GET | `/api/analytics/cameras/{id}/heatmap` | Any |
| GET | `/api/analytics/viewers` | Any |
| GET | `/api/analytics/storage` | Admin/Op |
| GET | `/api/analytics/events/recent` | Any |
| POST | `/api/analytics/events/acknowledge` | Any |
| GET | `/api/analytics/recordings/by-camera` | Any |
| GET | `/api/analytics/health` | Admin/Op |
| GET | `/api/analytics/motion/summary` | Any |
| GET | `/api/analytics/bandwidth` | Any |

## SignalR Hub Methods (Client → Server)

| Method | Args | Min Permission |
|---|---|---|
| `SubscribeToCamera` | `cameraId: string` | View |
| `UnsubscribeFromCamera` | `cameraId: string` | View |
| `SubscribeToEvents` | — | Any |
| `StreamControl` | `StreamControlCommand` | View |
| `StartRecording` | `cameraId: string` | Record |
| `StopRecording` | `cameraId: string` | Record |
| `PtzCommand` | `PtzCommandDto` | Control |
| `GetPtzStatus` | `cameraId: string` | View |
| `RequestSnapshot` | `cameraId: string` | View |
| `AcknowledgeAlert` | `alertId: string` | View |

## SignalR Events (Server → Client)

| Event | When |
|---|---|
| `SystemState` | On connect |
| `CameraFrame` | Every frame (~66ms) |
| `StreamState` | After StreamControl / state change |
| `PlaybackPosition` | Every second during playback |
| `PlaybackReady` | Recording found at seek position |
| `SeekComplete` | Seek done |
| `ZoomChanged` | After zoom commands |
| `CameraStatus` | Camera online/offline/recording change |
| `RecordingStatus` | Recording started/stopped/chunk |
| `PtzFeedback` | After every PtzCommand |
| `PtzStatus` | Response to GetPtzStatus |
| `PresetSaved` | After SavePreset |
| `Snapshot` | Response to RequestSnapshot |
| `Alert` | New motion/tamper/error event |
| `StorageAlert` | Storage warning |
| `AnalyticsUpdate` | Hourly metric push |
| `AlertAcknowledged` | Alert marked acknowledged |
| `AccessDenied` | Permission check failed |
| `UserNotification` | Targeted user message |
| `Error` | General error |
| `StreamError` | Stream-specific error |
