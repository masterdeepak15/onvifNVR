import { useQuery } from '@tanstack/react-query';
import { dashboardService, analyticsService } from '@/lib/services';
import { useAuth } from '@/contexts/AuthContext';
import {
  Camera, CameraOff, Circle, HardDrive, AlertTriangle,
  Activity, Users, Clock, TrendingUp, Bell
} from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, BarChart, Bar
} from 'recharts';

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
}

function formatDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function StatCard({ icon: Icon, label, value, subValue, variant = 'default' }: {
  icon: any; label: string; value: string | number; subValue?: string;
  variant?: 'default' | 'success' | 'warning' | 'destructive' | 'primary';
}) {
  const variantStyles = {
    default: 'bg-card',
    success: 'bg-card border-success/20',
    warning: 'bg-card border-warning/20',
    destructive: 'bg-card border-destructive/20',
    primary: 'bg-card border-primary/20',
  };
  const iconStyles = {
    default: 'bg-muted text-muted-foreground',
    success: 'bg-success/10 text-success',
    warning: 'bg-warning/10 text-warning',
    destructive: 'bg-destructive/10 text-destructive',
    primary: 'bg-primary/10 text-primary',
  };

  return (
    <div className={cn("border rounded-xl p-5 transition-colors", variantStyles[variant])}>
      <div className="flex items-start justify-between">
        <div className="space-y-2">
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="text-2xl font-bold">{value}</p>
          {subValue && <p className="text-xs text-muted-foreground">{subValue}</p>}
        </div>
        <div className={cn("w-10 h-10 rounded-lg flex items-center justify-center", iconStyles[variant])}>
          <Icon className="w-5 h-5" />
        </div>
      </div>
    </div>
  );
}

export default function Dashboard() {
  const { user } = useAuth();

  const { data: dashboard, isLoading: dashLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => dashboardService.get(),
    refetchInterval: 30000,
  });

  const { data: summary } = useQuery({
    queryKey: ['analytics-summary'],
    queryFn: () => analyticsService.getSummary(),
    refetchInterval: 60000,
  });

  const { data: events } = useQuery({
    queryKey: ['dashboard-events'],
    queryFn: () => dashboardService.getEvents(20),
    refetchInterval: 15000,
  });

  const { data: health } = useQuery({
    queryKey: ['analytics-health'],
    queryFn: () => analyticsService.getHealth(),
    refetchInterval: 60000,
    enabled: user?.role === 'Admin' || user?.role === 'Operator',
  });

  const { data: motionSummary } = useQuery({
    queryKey: ['motion-summary'],
    queryFn: () => analyticsService.getMotionSummary(24),
    refetchInterval: 60000,
  });

  if (dashLoading) {
    return (
      <div className="p-6">
        <div className="animate-pulse space-y-6">
          <div className="h-8 bg-muted rounded w-48" />
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {[...Array(8)].map((_, i) => <div key={i} className="h-28 bg-muted rounded-xl" />)}
          </div>
        </div>
      </div>
    );
  }

  const alertTrend = summary?.alertTrend?.map((t: any) => ({
    time: new Date(t.hour).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    motion: t.motionCount,
    error: t.errorCount,
    total: t.totalCount,
  })) || [];

  const recordingTrend = summary?.recordingTrend?.map((t: any) => ({
    time: new Date(t.hour).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    recordings: t.recordingCount,
    data: Math.round(t.bytesWritten / (1024 * 1024)),
  })) || [];

  const storagePercent = dashboard
    ? Math.round((dashboard.usedStorageBytes / dashboard.totalStorageBytes) * 100)
    : 0;

  return (
    <div className="p-6 space-y-6 max-w-[1600px] mx-auto">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Dashboard</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Welcome back, {user?.username}. System overview at a glance.
          </p>
        </div>
        {health && (
          <div className={cn(
            "px-4 py-2 rounded-full text-sm font-medium border",
            health.overallStatus === 'Healthy'
              ? "bg-success/10 text-success border-success/20"
              : "bg-warning/10 text-warning border-warning/20"
          )}>
            {health.overallStatus === 'Healthy' ? '● System Healthy' : '● System Degraded'}
          </div>
        )}
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard icon={Camera} label="Online Cameras" value={`${dashboard?.onlineCameras ?? 0}/${dashboard?.totalCameras ?? 0}`}
          subValue={dashboard?.offlineCameras ? `${dashboard.offlineCameras} offline` : 'All online'} variant="success" />
        <StatCard icon={Circle} label="Recording" value={dashboard?.recordingCameras ?? 0}
          subValue={`${summary?.totalRecordingsToday ?? 0} recordings today`} variant="destructive" />
        <StatCard icon={HardDrive} label="Storage Used" value={`${storagePercent}%`}
          subValue={dashboard ? `${formatBytes(dashboard.usedStorageBytes)} / ${formatBytes(dashboard.totalStorageBytes)}` : ''}
          variant={storagePercent > 85 ? 'warning' : 'primary'} />
        <StatCard icon={AlertTriangle} label="Active Alerts" value={dashboard?.activeAlerts ?? 0}
          subValue={`${summary?.unacknowledgedAlerts ?? 0} unacknowledged`}
          variant={(dashboard?.activeAlerts ?? 0) > 0 ? 'warning' : 'default'} />
      </div>

      {summary && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard icon={Clock} label="Recording Hours Today" value={`${summary.totalRecordingHoursToday}h`}
            subValue={`${summary.totalRecordingHoursWeek}h this week`} variant="primary" />
          <StatCard icon={Activity} label="Motion Events" value={summary.motionEventsToday}
            subValue={motionSummary?.mostActiveCamera?.cameraName ? `Most active: ${motionSummary.mostActiveCamera.cameraName}` : ''}
            variant="warning" />
          <StatCard icon={Users} label="Live Viewers" value={summary.activeLiveViewers}
            subValue={`Peak today: ${summary.peakViewersToday}`} variant="primary" />
          <StatCard icon={TrendingUp} label="System Uptime" value={`${summary.systemUptimePercent}%`}
            subValue={`Est. ${summary.estimatedDaysRemaining} days storage left`} variant="success" />
        </div>
      )}

      {/* Charts */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Alert Trend */}
        <div className="bg-card border rounded-xl p-5">
          <h3 className="font-semibold mb-4 flex items-center gap-2">
            <Bell className="w-4 h-4 text-warning" /> Alert Trend
          </h3>
          <div className="h-64">
            {alertTrend.length > 0 ? (
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={alertTrend}>
                  <CartesianGrid strokeDasharray="3 3" className="opacity-20" />
                  <XAxis dataKey="time" tick={{ fontSize: 11 }} className="text-muted-foreground" />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip contentStyle={{ background: 'hsl(var(--card))', border: '1px solid hsl(var(--border))', borderRadius: '8px' }} />
                  <Area type="monotone" dataKey="motion" stackId="1" stroke="hsl(var(--warning))" fill="hsl(var(--warning))" fillOpacity={0.3} />
                  <Area type="monotone" dataKey="error" stackId="1" stroke="hsl(var(--destructive))" fill="hsl(var(--destructive))" fillOpacity={0.3} />
                </AreaChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-full flex items-center justify-center text-muted-foreground text-sm">No alert data available</div>
            )}
          </div>
        </div>

        {/* Recording Trend */}
        <div className="bg-card border rounded-xl p-5">
          <h3 className="font-semibold mb-4 flex items-center gap-2">
            <Activity className="w-4 h-4 text-primary" /> Recording Activity
          </h3>
          <div className="h-64">
            {recordingTrend.length > 0 ? (
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={recordingTrend}>
                  <CartesianGrid strokeDasharray="3 3" className="opacity-20" />
                  <XAxis dataKey="time" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip contentStyle={{ background: 'hsl(var(--card))', border: '1px solid hsl(var(--border))', borderRadius: '8px' }} />
                  <Bar dataKey="recordings" fill="hsl(var(--primary))" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-full flex items-center justify-center text-muted-foreground text-sm">No recording data available</div>
            )}
          </div>
        </div>
      </div>

      {/* Bottom row: Camera breakdown + Events */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Camera Breakdown */}
        <div className="lg:col-span-2 bg-card border rounded-xl p-5">
          <h3 className="font-semibold mb-4 flex items-center gap-2">
            <Camera className="w-4 h-4 text-primary" /> Camera Status
          </h3>
          <div className="overflow-auto max-h-80">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-muted-foreground text-left">
                  <th className="pb-3 font-medium">Camera</th>
                  <th className="pb-3 font-medium">Status</th>
                  <th className="pb-3 font-medium">Uptime</th>
                  <th className="pb-3 font-medium">Motion</th>
                  <th className="pb-3 font-medium">Storage</th>
                  <th className="pb-3 font-medium">Viewers</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {summary?.cameraBreakdown?.map((cam: any) => (
                  <tr key={cam.cameraId} className="hover:bg-muted/50">
                    <td className="py-3 font-medium">{cam.cameraName}</td>
                    <td className="py-3">
                      <span className={cn(
                        "inline-flex items-center gap-1.5 text-xs px-2 py-0.5 rounded-full",
                        cam.status === 'Online' ? "bg-success/10 text-success" : "bg-destructive/10 text-destructive"
                      )}>
                        <span className={cn("w-1.5 h-1.5 rounded-full", cam.status === 'Online' ? "bg-success" : "bg-destructive")} />
                        {cam.status}
                      </span>
                      {cam.isRecording && (
                        <span className="ml-2 inline-flex items-center gap-1 text-xs text-recording">
                          <span className="w-1.5 h-1.5 rounded-full bg-recording recording-pulse" /> REC
                        </span>
                      )}
                    </td>
                    <td className="py-3 text-muted-foreground">{cam.uptimePercent}%</td>
                    <td className="py-3 text-muted-foreground">{cam.motionEvents}</td>
                    <td className="py-3 text-muted-foreground">{formatBytes(cam.storageBytesUsed)}</td>
                    <td className="py-3 text-muted-foreground">{cam.activeViewers}</td>
                  </tr>
                ))}
                {(!summary?.cameraBreakdown || summary.cameraBreakdown.length === 0) && (
                  <tr><td colSpan={6} className="py-8 text-center text-muted-foreground">No cameras configured</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* Recent Events */}
        <div className="bg-card border rounded-xl p-5">
          <h3 className="font-semibold mb-4 flex items-center gap-2">
            <Bell className="w-4 h-4 text-warning" /> Recent Events
          </h3>
          <div className="space-y-3 max-h-80 overflow-auto">
            {events?.map((evt: any) => (
              <div key={evt.id} className="flex items-start gap-3 p-3 bg-muted/50 rounded-lg">
                <div className={cn(
                  "w-2 h-2 rounded-full mt-1.5 shrink-0",
                  evt.severity === 'Critical' ? "bg-destructive" :
                  evt.severity === 'Warning' ? "bg-warning" : "bg-primary"
                )} />
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-medium">{evt.cameraName}</span>
                    <span className={cn(
                      "text-[10px] px-1.5 py-0.5 rounded",
                      evt.eventType === 'Motion' ? "bg-warning/10 text-warning" :
                      evt.eventType === 'Error' ? "bg-destructive/10 text-destructive" :
                      evt.eventType === 'Offline' ? "bg-muted text-muted-foreground" :
                      "bg-primary/10 text-primary"
                    )}>{evt.eventType}</span>
                  </div>
                  <p className="text-xs text-muted-foreground mt-1 truncate">{evt.details}</p>
                  <p className="text-[10px] text-muted-foreground/60 mt-1">
                    {new Date(evt.timestamp).toLocaleTimeString()}
                  </p>
                </div>
              </div>
            ))}
            {(!events || events.length === 0) && (
              <div className="text-center text-muted-foreground text-sm py-8">No recent events</div>
            )}
          </div>
        </div>
      </div>

      {/* Storage Breakdown */}
      {dashboard?.storageSummaries && dashboard.storageSummaries.length > 0 && (
        <div className="bg-card border rounded-xl p-5">
          <h3 className="font-semibold mb-4 flex items-center gap-2">
            <HardDrive className="w-4 h-4 text-primary" /> Storage Profiles
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {dashboard.storageSummaries.map((s: any) => {
              const pct = Math.round((s.usedStorageBytes / s.maxStorageBytes) * 100);
              return (
                <div key={s.id} className="bg-muted/50 rounded-lg p-4">
                  <div className="flex items-center justify-between mb-3">
                    <span className="font-medium text-sm">{s.name}</span>
                    <span className={cn(
                      "text-xs px-2 py-0.5 rounded",
                      s.isHealthy ? "bg-success/10 text-success" : "bg-destructive/10 text-destructive"
                    )}>{s.isHealthy ? 'Healthy' : 'Unhealthy'}</span>
                  </div>
                  <div className="w-full bg-muted rounded-full h-2 mb-2">
                    <div
                      className={cn("h-2 rounded-full transition-all", pct > 85 ? "bg-warning" : "bg-primary")}
                      style={{ width: `${Math.min(pct, 100)}%` }}
                    />
                  </div>
                  <div className="flex justify-between text-xs text-muted-foreground">
                    <span>{formatBytes(s.usedStorageBytes)}</span>
                    <span>{formatBytes(s.maxStorageBytes)}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
