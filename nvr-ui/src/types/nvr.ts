// Auth
export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserDto;
}

export interface UserDto {
  id: string;
  username: string;
  email: string;
  role: 'Admin' | 'Operator' | 'Viewer';
  lastLoginAt: string;
  isActive?: boolean;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
  role?: string;
}

// Camera
export interface CameraDto {
  id: string;
  name: string;
  ipAddress: string;
  port: number;
  rtspUrl: string;
  onvifServiceUrl: string;
  manufacturer: string;
  model: string;
  status: 'Online' | 'Offline' | 'Error' | 'Unknown';
  isOnline: boolean;
  isRecording: boolean;
  ptzCapable: boolean;
  audioEnabled: boolean;
  resolution_Width: number;
  resolution_Height: number;
  framerate: number;
  codec: string;
  gridPosition: number;
  storageProfileId: string | null;
  createdAt: string;
  lastSeenAt: string | null;
  ptzPresets: PtzPresetDto[];
}

export interface MyCameraDto {
  id: string;
  name: string;
  ipAddress: string;
  status: 'Online' | 'Offline' | 'Error' | 'Unknown';
  isOnline: boolean;
  isRecording: boolean;
  ptzCapable: boolean;
  resolution_Width: number;
  resolution_Height: number;
  framerate: number;
  gridPosition: number;
  lastSeenAt: string | null;
  permission: string;
  canControl: boolean;
  canRecord: boolean;
  ptzPresets: PtzPresetDto[];
}

export interface PtzPresetDto {
  id: string;
  name: string;
  onvifToken: string;
  panPosition: number | null;
  tiltPosition: number | null;
  zoomPosition: number | null;
}

export interface DiscoveredCamera {
  xAddr: string;
  ipAddress: string;
  port: number;
  manufacturer: string;
  model: string;
  isAlreadyAdded: boolean;
}

// Recording
export interface RecordingDto {
  id: string;
  cameraId: string;
  cameraName: string;
  startTime: string;
  endTime: string | null;
  fileSizeBytes: number;
  durationSeconds: number;
  status: 'Recording' | 'Completed' | 'Deleted' | 'Error';
  triggerType: 'Manual' | 'Scheduled' | 'Motion' | 'Continuous';
  thumbnailPath: string;
  width: number;
  height: number;
  chunkCount: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface PlaybackSession {
  sessionId: string;
  startTimestamp: string;
  cameraStreams: PlaybackCameraStream[];
}

export interface PlaybackCameraStream {
  cameraId: string;
  cameraName: string;
  recordingId: string;
  hasRecording: boolean;
  streamUrl: string | null;
  timeline: TimelineSegment[];
}

export interface TimelineSegment {
  start: string;
  end: string;
  triggerType: string;
  hasMotion: boolean;
}

// Storage
export interface StorageProfileDto {
  id: string;
  name: string;
  type: string;
  isDefault: boolean;
  isEnabled: boolean;
  host: string | null;
  port: number | null;
  username: string | null;
  basePath: string | null;
  shareName: string | null;
  region: string | null;
  containerName: string | null;
  maxStorageBytes: number;
  usedStorageBytes: number;
  retentionDays: number;
  autoDeleteEnabled: boolean;
  isHealthy: boolean;
  lastHealthCheck: string | null;
  healthError: string | null;
  usagePercent: number;
}

// Layout
export interface GridLayout {
  layoutName: string;
  gridColumns: number;
  positions: GridPosition[];
}

export interface GridPosition {
  cameraId: string;
  gridPosition: number;
}

// Schedule
export interface ScheduleDto {
  id: string;
  cameraId: string;
  name: string;
  isEnabled: boolean;
  daysOfWeek: number;
  startTime: string;
  endTime: string;
  recordingMode: string;
  chunkDurationSeconds: number;
  bitrateKbps: number;
  quality: string;
}

// Dashboard
export interface DashboardDto {
  totalCameras: number;
  onlineCameras: number;
  recordingCameras: number;
  offlineCameras: number;
  totalStorageBytes: number;
  usedStorageBytes: number;
  activeAlerts: number;
  todayRecordingCount: number;
  storageSummaries: StorageSummary[];
}

export interface StorageSummary {
  id: string;
  name: string;
  type: string;
  maxStorageBytes: number;
  usedStorageBytes: number;
  isHealthy: boolean;
}

export interface DashboardEvent {
  id: string;
  cameraId: string;
  cameraName: string;
  eventType: 'Motion' | 'Tamper' | 'Online' | 'Offline' | 'Error';
  severity: 'Info' | 'Warning' | 'Critical';
  timestamp: string;
  details: string;
  isAcknowledged: boolean;
}

// Analytics
export interface AnalyticsSummary {
  totalCameras: number;
  onlineCameras: number;
  offlineCameras: number;
  recordingCameras: number;
  systemUptimePercent: number;
  totalStorageBytes: number;
  usedStorageBytes: number;
  storageUsagePercent: number;
  storageBytesWrittenToday: number;
  estimatedDaysRemaining: number;
  totalRecordingHoursToday: number;
  totalRecordingHoursWeek: number;
  totalRecordingsToday: number;
  activeRecordings: number;
  totalAlertsToday: number;
  unacknowledgedAlerts: number;
  motionEventsToday: number;
  cameraErrorsToday: number;
  activeLiveViewers: number;
  activePlaybackSessions: number;
  peakViewersToday: number;
  cameraBreakdown: CameraBreakdown[];
  storageBreakdown: StorageBreakdownItem[];
  alertTrend: AlertTrendItem[];
  recordingTrend: RecordingTrendItem[];
}

export interface CameraBreakdown {
  cameraId: string;
  cameraName: string;
  status: string;
  isRecording: boolean;
  uptimePercent: number;
  recordingSeconds: number;
  storageBytesUsed: number;
  motionEvents: number;
  activeViewers: number;
  lastMotionAt: string | null;
  lastSeenAt: string | null;
  avgBitrateKbps: number;
}

export interface StorageBreakdownItem {
  profileId: string;
  profileName: string;
  type: string;
  totalBytes: number;
  usedBytes: number;
  usagePercent: number;
  retentionDays: number;
  estimatedDaysRemaining: number;
  isHealthy: boolean;
  bytesWrittenToday: number;
}

export interface AlertTrendItem {
  hour: string;
  motionCount: number;
  tamperCount: number;
  errorCount: number;
  totalCount: number;
}

export interface RecordingTrendItem {
  hour: string;
  recordingCount: number;
  totalSeconds: number;
  bytesWritten: number;
}

// Camera Access
export interface CameraAccessDto {
  id: string;
  cameraId: string;
  cameraName: string;
  userId: string;
  username: string;
  permission: 'View' | 'Control' | 'Record' | 'Admin';
  grantedAt: string;
  grantedBy: string;
  expiresAt: string | null;
  isActive: boolean;
}

export interface GrantCameraAccessRequest {
  userId: string;
  permission: 'View' | 'Control' | 'Record' | 'Admin';
  expiresAt?: string;
}

export interface UserCameraPermissionsDto {
  userId: string;
  username: string;
  globalRole: string;
  isAdmin: boolean;
  cameraPermissions: CameraPermissionItem[];
}

export interface CameraPermissionItem {
  cameraId: string;
  cameraName: string;
  permission: string;
  isExplicit: boolean;
}

export interface MyPermission {
  cameraId: string;
  permission: string;
  canView: boolean;
  canControl: boolean;
  canRecord: boolean;
  canAdmin: boolean;
}

// ============= SignalR Hub DTOs =============

// --- Invoke Commands ---

export interface StreamControlCommand {
  cameraId: string;
  command: 'Play' | 'Pause' | 'Resume' | 'Stop' | 'SetSpeed' | 'Seek' | 'ZoomIn' | 'ZoomOut' | 'ZoomReset' | 'GoLive';
  speed?: number;
  seekTo?: string; // ISO DateTime
  zoomLevel?: number;
}

export interface PtzCommandDto {
  cameraId: string;
  action: string;
  speed: number;
  pan: number;
  tilt: number;
  zoom: number;
  presetToken?: string;
  presetName?: string;
}

// --- Received Events ---

export interface CameraFrame {
  cameraId: string;
  frame: string; // base64 JPEG
  timestampMs: number;
  width: number;
  height: number;
  fps: number;
  isKeyframe: boolean;
  streamState: 'Live' | 'Playback';
}

export interface StreamStateDto {
  cameraId: string;
  state: 'Live' | 'Playing' | 'Paused' | 'Stopped' | 'Buffering' | 'Error' | 'NoRecording';
  speed: number;
  zoomLevel: number;
  playbackPosition?: string;
  isLive: boolean;
  errorMessage?: string;
  fps: number;
  bitrateKbps: number;
  bufferedMs: number;
}

export interface PtzFeedbackPayload {
  cameraId: string;
  action: string;
  success: boolean;
  error?: string;
  pan: number;
  tilt: number;
  zoom: number;
  moveStatus: string;
  focusPosition?: number;
  focusMode?: string;
  focusMoveStatus?: string;
  irisLevel?: number;
  irisMode?: string;
}

export interface PtzStatusDto {
  cameraId: string;
  pan: number;
  tilt: number;
  zoom: number;
  moveStatus: string;
  supportsFocus: boolean;
  supportsIris: boolean;
  presets: PtzPresetDto[];
  lastUpdated: string;
}

export interface RecordingStatusPayload {
  cameraId: string;
  recordingId?: string;
  status: 'Started' | 'Stopped' | 'Chunk' | 'Error';
  chunkNumber?: number;
  fileSizeBytes?: number;
  durationSeconds?: number;
  timestamp: string;
}

export interface CameraStatusPayload {
  cameraId: string;
  status: string;
  isOnline: boolean;
  isRecording: boolean;
  lastSeenAt?: string;
  lastError?: string;
  activeViewers: number;
}

export interface AlertPayload {
  alertId: string;
  cameraId?: string;
  cameraName?: string;
  type: string;
  message: string;
  severity: 'Info' | 'Warning' | 'Critical';
  timestamp: string;
  snapshotBase64?: string;
}

export interface AnalyticsUpdatePayload {
  onlineCameras: number;
  recordingCameras: number;
  activeViewers: number;
  storageUsagePercent: number;
  unacknowledgedAlerts: number;
  timestamp: string;
}

export interface SnapshotPayload {
  cameraId: string;
  frame: string;
  timestamp: string;
}

export interface ZoomChangedPayload {
  cameraId: string;
  zoomDelta: number;
  reset: boolean;
}

export interface PlaybackPositionPayload {
  cameraId: string;
  position: string;
  speed: number;
}

export interface PlaybackReadyPayload {
  cameraId: string;
  startPosition: string;
  chunkCount: number;
  firstChunkPath: string;
  speed: number;
}

export interface SeekCompletePayload {
  cameraId: string;
  position: string;
}

export interface SystemStatePayload {
  cameras: Array<{
    id: string;
    name: string;
    status: string;
    isOnline: boolean;
    isRecording: boolean;
    lastSeenAt: string | null;
  }>;
  serverTime: string;
}

export interface AlertAcknowledgedPayload {
  alertId: string;
  acknowledgedBy: string;
}

export interface AccessDeniedPayload {
  cameraId: string;
  requiredPermission: string;
  message: string;
}

export interface StreamErrorPayload {
  cameraId: string;
  error: string;
  timestamp: string;
}

export interface HubErrorPayload {
  cameraId?: string;
  message: string;
}

// ============= Analytics =============

export interface CameraUptimeReportDto {
  cameraId: string;
  cameraName: string;
  from: string;
  to: string;
  slots: UptimeSlotDto[];
  overallUptimePercent: number;
  totalDowntimeMinutes: number;
  totalDowntimeEvents: number;
}

export interface UptimeSlotDto {
  start: string;
  end: string;
  status: 'Online' | 'Offline' | 'Error';
}

export interface StorageHeatmapDto {
  cameraId: string;
  cameraName: string;
  days: StorageHeatmapDayDto[];
}

export interface StorageHeatmapDayDto {
  date: string;
  bytesRecorded: number;
  recordingMinutes: number;
  motionEvents: number;
  hasRecording: boolean;
}

export interface LiveViewerDto {
  cameraId: string;
  viewerCount: number;
  viewers: ViewerInfoDto[];
}

export interface ViewerInfoDto {
  userId: string;
  username: string;
  connectedAt: string;
  clientIp: string;
}

export interface SystemSettings {
  [key: string]: string;
}
