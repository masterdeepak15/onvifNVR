import { api } from './api';
import type {
  AuthResponse, LoginRequest, RegisterRequest, UserDto,
  CameraDto, MyCameraDto, DiscoveredCamera,
  RecordingDto, PagedResult, PlaybackSession,
  StorageProfileDto, GridLayout, ScheduleDto,
  DashboardDto, DashboardEvent, AnalyticsSummary,
  CameraAccessDto, GrantCameraAccessRequest, UserCameraPermissionsDto,
  MyPermission, SystemSettings,
  CameraUptimeReportDto, StorageHeatmapDto, LiveViewerDto,
  CameraBreakdown, StorageBreakdownItem, AlertTrendItem, RecordingTrendItem,
} from '@/types/nvr';

// Auth
export const authService = {
  login: (data: LoginRequest) => api.post<AuthResponse>('/api/auth/login', data),
  refresh: (refreshToken: string) => api.post<AuthResponse>('/api/auth/refresh', { refreshToken }),
  register: (data: RegisterRequest) => api.post<UserDto>('/api/auth/register', data),
  logout: () => api.post<void>('/api/auth/logout'),
  me: () => api.get<UserDto>('/api/auth/me'),
};

// Users
export const userService = {
  getAll: () => api.get<UserDto[]>('/api/users'),
  update: (id: string, data: Partial<UserDto & { password?: string; isActive?: boolean }>) =>
    api.put<UserDto>(`/api/users/${id}`, data),
  delete: (id: string) => api.delete<void>(`/api/users/${id}`),
};

// Cameras
export const cameraService = {
  getAll: () => api.get<CameraDto[]>('/api/cameras'),
  getById: (id: string) => api.get<CameraDto>(`/api/cameras/${id}`),
  create: (data: any) => api.post<CameraDto>('/api/cameras', data),
  update: (id: string, data: any) => api.put<CameraDto>(`/api/cameras/${id}`, data),
  delete: (id: string) => api.delete<void>(`/api/cameras/${id}`),
  discover: () => api.get<DiscoveredCamera[]>('/api/cameras/discover'),
  startRecording: (id: string) => api.post<void>(`/api/cameras/${id}/recording/start`),
  stopRecording: (id: string) => api.post<void>(`/api/cameras/${id}/recording/stop`),
  getSnapshot: (id: string) => api.requestBlob(`/api/cameras/${id}/snapshot`),
  getMyCameras: () => api.get<MyCameraDto[]>('/api/my/cameras'),
  getMyLayout: (name?: string) => api.get<GridLayout>('/api/my/layout', { layoutName: name }),
};

// PTZ
export const ptzService = {
  move: (cameraId: string, data: any) => api.post<void>(`/api/cameras/${cameraId}/ptz/move`, data),
  stop: (cameraId: string) => api.post<void>(`/api/cameras/${cameraId}/ptz/stop`),
  getPresets: (cameraId: string) => api.get<any[]>(`/api/cameras/${cameraId}/ptz/presets`),
  gotoPreset: (cameraId: string, presetId: string) =>
    api.post<void>(`/api/cameras/${cameraId}/ptz/presets/${presetId}/goto`),
  savePreset: (cameraId: string, name: string) =>
    api.post<any>(`/api/cameras/${cameraId}/ptz/presets`, name),
  deletePreset: (cameraId: string, presetId: string) =>
    api.delete<void>(`/api/cameras/${cameraId}/ptz/presets/${presetId}`),
};

// Recordings
export const recordingService = {
  search: (params: {
    cameraId?: string; cameraIds?: string; startTime?: string;
    endTime?: string; triggerType?: string; page?: number; pageSize?: number;
  }) => api.get<PagedResult<RecordingDto>>('/api/recordings', params),
  getById: (id: string) => api.get<RecordingDto>(`/api/recordings/${id}`),
  startPlayback: (cameraIds: string[], timestamp: string, speed = 1.0) =>
    api.post<PlaybackSession>('/api/recordings/playback', { cameraIds, timestamp, speed }),
  delete: (id: string) => api.delete<void>(`/api/recordings/${id}`),
};

// Storage
export const storageService = {
  getAll: () => api.get<StorageProfileDto[]>('/api/storage'),
  getTypes: () => api.get<string[]>('/api/storage/types'),
  create: (data: any) => api.post<StorageProfileDto>('/api/storage', data),
  update: (id: string, data: any) => api.put<StorageProfileDto>(`/api/storage/${id}`, data),
  test: (id: string) => api.post<{ success: boolean; message: string }>(`/api/storage/${id}/test`),
  delete: (id: string) => api.delete<void>(`/api/storage/${id}`),
};

// Layout
export const layoutService = {
  get: (name = 'Default') => api.get<GridLayout>('/api/layout', { layoutName: name }),
  save: (layout: GridLayout) => api.post<void>('/api/layout', layout),
  getNames: () => api.get<string[]>('/api/layout/names'),
};

// Schedules
export const scheduleService = {
  getForCamera: (cameraId: string) => api.get<ScheduleDto[]>(`/api/cameras/${cameraId}/schedules`),
  create: (cameraId: string, data: any) => api.post<ScheduleDto>(`/api/cameras/${cameraId}/schedules`, data),
  delete: (cameraId: string, scheduleId: string) =>
    api.delete<void>(`/api/cameras/${cameraId}/schedules/${scheduleId}`),
};

// Dashboard
export const dashboardService = {
  get: () => api.get<DashboardDto>('/api/dashboard'),
  getEvents: (count = 50) => api.get<DashboardEvent[]>('/api/dashboard/events', { count }),
};

// Analytics
export const analyticsService = {
  getSummary: () => api.get<AnalyticsSummary>('/api/analytics/summary'),
  getCamera: (id: string) => api.get<CameraBreakdown>(`/api/analytics/cameras/${id}`),
  getAlertTrend: (from?: string, to?: string) =>
    api.get<AlertTrendItem[]>('/api/analytics/alerts/trend', { from, to }),
  getRecordingTrend: (from?: string, to?: string) =>
    api.get<RecordingTrendItem[]>('/api/analytics/recordings/trend', { from, to }),
  getCameraUptime: (id: string, from?: string, to?: string) =>
    api.get<CameraUptimeReportDto>(`/api/analytics/cameras/${id}/uptime`, { from, to }),
  getCameraHeatmap: (id: string, days = 30) =>
    api.get<StorageHeatmapDto>(`/api/analytics/cameras/${id}/heatmap`, { days }),
  getViewers: (cameraId?: string) => api.get<LiveViewerDto[]>('/api/analytics/viewers', { cameraId }),
  getStorage: () => api.get<StorageBreakdownItem[]>('/api/analytics/storage'),
  getRecentEvents: (params?: any) => api.get<any[]>('/api/analytics/events/recent', params),
  acknowledgeEvents: (eventIds: string[]) =>
    api.post<void>('/api/analytics/events/acknowledge', eventIds),
  getRecordingsByCamera: (from?: string, to?: string) =>
    api.get<any[]>('/api/analytics/recordings/by-camera', { from, to }),
  getHealth: () => api.get<any>('/api/analytics/health'),
  getMotionSummary: (hours = 24) => api.get<any>('/api/analytics/motion/summary', { hours }),
  getBandwidth: () => api.get<any[]>('/api/analytics/bandwidth'),
};

// Camera Access
export const cameraAccessService = {
  getAccess: (cameraId: string) => api.get<CameraAccessDto[]>(`/api/cameras/${cameraId}/access`),
  grantAccess: (cameraId: string, data: GrantCameraAccessRequest) =>
    api.post<CameraAccessDto>(`/api/cameras/${cameraId}/access`, data),
  updateAccess: (cameraId: string, accessId: string, permission: string) =>
    api.put<CameraAccessDto>(`/api/cameras/${cameraId}/access/${accessId}`, { permission }),
  revokeAccess: (cameraId: string, accessId: string) =>
    api.delete<void>(`/api/cameras/${cameraId}/access/${accessId}`),
  getMyPermission: (cameraId: string) =>
    api.get<MyPermission>(`/api/cameras/${cameraId}/access/my-permission`),
  getUserPermissions: (userId: string) =>
    api.get<UserCameraPermissionsDto>(`/api/users/${userId}/camera-permissions`),
  bulkUpdatePermissions: (userId: string, permissions: any[]) =>
    api.put<void>(`/api/users/${userId}/camera-permissions/bulk`, permissions),
};

// Settings
export const settingsService = {
  getAll: () => api.get<SystemSettings>('/api/settings'),
  update: (settings: SystemSettings) => api.put<void>('/api/settings', settings),
};
