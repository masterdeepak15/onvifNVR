import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/contexts/AuthContext';
import { useTheme } from '@/contexts/ThemeContext';
import {
  userService, cameraService, storageService, settingsService,
  authService, cameraAccessService,
} from '@/lib/services';
import type { UserDto, CameraDto, StorageProfileDto, SystemSettings, CameraAccessDto, GrantCameraAccessRequest } from '@/types/nvr';
import {
  Users, Camera, HardDrive, Sliders, Shield, Sun, Moon, Plus,
  Trash2, Edit, Search, RefreshCw, CheckCircle, XCircle, Eye, Lock
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';

const tabs = [
  { id: 'general', label: 'General', icon: Sliders },
  { id: 'users', label: 'Users', icon: Users },
  { id: 'cameras', label: 'Cameras', icon: Camera },
  { id: 'storage', label: 'Storage', icon: HardDrive },
  { id: 'access', label: 'Access Control', icon: Shield },
];

export default function SettingsPage() {
  const { user } = useAuth();
  const [activeTab, setActiveTab] = useState('general');
  const isAdmin = user?.role === 'Admin';

  return (
    <div className="h-full flex flex-col">
      <div className="border-b bg-card px-6 py-4 shrink-0">
        <h2 className="text-xl font-bold">Settings</h2>
        <p className="text-sm text-muted-foreground mt-1">Manage system configuration</p>
      </div>

      <div className="flex-1 flex overflow-hidden">
        <div className="w-52 border-r bg-card p-3 shrink-0">
          {tabs.map(tab => {
            const restricted = (tab.id === 'users' || tab.id === 'storage' || tab.id === 'access') && !isAdmin;
            if (restricted) return null;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={cn(
                  "flex items-center gap-3 w-full px-3 py-2.5 rounded-lg text-sm transition-colors mb-1",
                  activeTab === tab.id
                    ? "bg-primary/10 text-primary font-medium"
                    : "text-muted-foreground hover:bg-muted"
                )}
              >
                <tab.icon className="w-4 h-4" />
                {tab.label}
              </button>
            );
          })}
        </div>

        <div className="flex-1 overflow-auto p-6">
          {activeTab === 'general' && <GeneralSettings />}
          {activeTab === 'users' && isAdmin && <UserManagement />}
          {activeTab === 'cameras' && <CameraManagement />}
          {activeTab === 'storage' && isAdmin && <StorageManagement />}
          {activeTab === 'access' && isAdmin && <AccessControl />}
        </div>
      </div>
    </div>
  );
}

function GeneralSettings() {
  const { theme, setTheme } = useTheme();
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  const { data: settings, isLoading } = useQuery({
    queryKey: ['system-settings'],
    queryFn: () => settingsService.getAll(),
    enabled: isAdmin,
  });

  const qc = useQueryClient();
  const updateMutation = useMutation({
    mutationFn: (data: SystemSettings) => settingsService.update(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['system-settings'] }); toast.success('Settings saved'); },
    onError: () => toast.error('Failed to save settings'),
  });

  const [editSettings, setEditSettings] = useState<SystemSettings>({});

  React.useEffect(() => {
    if (settings) setEditSettings(settings);
  }, [settings]);

  return (
    <div className="space-y-8 max-w-2xl">
      <section>
        <h3 className="text-lg font-semibold mb-4">Appearance</h3>
        <div className="flex gap-4">
          <button onClick={() => setTheme('light')}
            className={cn("flex-1 p-4 rounded-xl border-2 transition-colors",
              theme === 'light' ? "border-primary bg-primary/5" : "border-border hover:border-muted-foreground/30")}>
            <Sun className="w-8 h-8 mb-2 text-warning mx-auto" />
            <p className="text-sm font-medium text-center">Light Mode</p>
          </button>
          <button onClick={() => setTheme('dark')}
            className={cn("flex-1 p-4 rounded-xl border-2 transition-colors",
              theme === 'dark' ? "border-primary bg-primary/5" : "border-border hover:border-muted-foreground/30")}>
            <Moon className="w-8 h-8 mb-2 text-primary mx-auto" />
            <p className="text-sm font-medium text-center">Dark Mode</p>
          </button>
        </div>
      </section>

      {isAdmin && !isLoading && (
        <section>
          <h3 className="text-lg font-semibold mb-4">System Configuration</h3>
          <div className="space-y-3">
            {Object.entries(editSettings).map(([key, value]) => (
              <div key={key} className="flex items-center gap-4">
                <label className="text-sm text-muted-foreground w-64 shrink-0 truncate" title={key}>{key}</label>
                <input type="text" value={value}
                  onChange={e => setEditSettings(prev => ({ ...prev, [key]: e.target.value }))}
                  className="flex-1 h-9 px-3 bg-muted border-none rounded-lg text-sm focus:ring-1 focus:ring-primary" />
              </div>
            ))}
          </div>
          <button onClick={() => updateMutation.mutate(editSettings)} disabled={updateMutation.isPending}
            className="mt-4 h-9 px-4 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:bg-primary/90 disabled:opacity-50">
            {updateMutation.isPending ? 'Saving...' : 'Save Settings'}
          </button>
        </section>
      )}
    </div>
  );
}

function UserManagement() {
  const qc = useQueryClient();
  const { data: users = [] } = useQuery({ queryKey: ['users'], queryFn: () => userService.getAll() });
  const [showRegister, setShowRegister] = useState(false);
  const [newUser, setNewUser] = useState({ username: '', email: '', password: '', role: 'Viewer' });
  const [editingUser, setEditingUser] = useState<string | null>(null);

  const registerMutation = useMutation({
    mutationFn: () => authService.register(newUser),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['users'] }); setShowRegister(false); setNewUser({ username: '', email: '', password: '', role: 'Viewer' }); toast.success('User created'); },
    onError: (err: any) => toast.error(err.message),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) => userService.update(id, data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['users'] }); setEditingUser(null); toast.success('User updated'); },
    onError: (err: any) => toast.error(err.message),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => userService.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['users'] }); toast.success('User deleted'); },
    onError: (err: any) => toast.error(err.message),
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">User Management</h3>
        <button onClick={() => setShowRegister(!showRegister)}
          className="h-9 px-4 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:bg-primary/90 flex items-center gap-2">
          <Plus className="w-4 h-4" /> Add User
        </button>
      </div>

      {showRegister && (
        <div className="bg-muted/50 rounded-xl p-4 space-y-3 border">
          <div className="grid grid-cols-2 gap-3">
            <input placeholder="Username" value={newUser.username} onChange={e => setNewUser({ ...newUser, username: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <input placeholder="Email" value={newUser.email} onChange={e => setNewUser({ ...newUser, email: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <input placeholder="Password" type="password" value={newUser.password} onChange={e => setNewUser({ ...newUser, password: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <select value={newUser.role} onChange={e => setNewUser({ ...newUser, role: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm">
              <option value="Viewer">Viewer</option>
              <option value="Operator">Operator</option>
              <option value="Admin">Admin</option>
            </select>
          </div>
          <div className="flex gap-2">
            <button onClick={() => registerMutation.mutate()} className="h-9 px-4 bg-primary text-primary-foreground rounded-lg text-sm">Create</button>
            <button onClick={() => setShowRegister(false)} className="h-9 px-4 bg-muted rounded-lg text-sm">Cancel</button>
          </div>
        </div>
      )}

      <div className="border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/50">
            <tr className="text-left text-muted-foreground">
              <th className="px-4 py-3 font-medium">Username</th>
              <th className="px-4 py-3 font-medium">Email</th>
              <th className="px-4 py-3 font-medium">Role</th>
              <th className="px-4 py-3 font-medium">Status</th>
              <th className="px-4 py-3 font-medium">Last Login</th>
              <th className="px-4 py-3 font-medium w-28">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {users.map(u => (
              <tr key={u.id} className="hover:bg-muted/30">
                <td className="px-4 py-3 font-medium">{u.username}</td>
                <td className="px-4 py-3 text-muted-foreground">{u.email}</td>
                <td className="px-4 py-3">
                  {editingUser === u.id ? (
                    <select defaultValue={u.role}
                      onChange={e => updateMutation.mutate({ id: u.id, data: { role: e.target.value } })}
                      className="h-7 px-2 bg-muted rounded text-xs">
                      <option value="Viewer">Viewer</option>
                      <option value="Operator">Operator</option>
                      <option value="Admin">Admin</option>
                    </select>
                  ) : (
                    <span className={cn("px-2 py-0.5 rounded text-xs",
                      u.role === 'Admin' ? "bg-primary/10 text-primary" :
                      u.role === 'Operator' ? "bg-warning/10 text-warning" : "bg-muted text-muted-foreground"
                    )}>{u.role}</span>
                  )}
                </td>
                <td className="px-4 py-3">
                  <button onClick={() => updateMutation.mutate({ id: u.id, data: { isActive: !u.isActive } })}
                    className={cn("text-xs px-2 py-0.5 rounded cursor-pointer",
                      u.isActive !== false ? "bg-success/10 text-success" : "bg-destructive/10 text-destructive")}>
                    {u.isActive !== false ? 'Active' : 'Inactive'}
                  </button>
                </td>
                <td className="px-4 py-3 text-muted-foreground text-xs">
                  {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : 'Never'}
                </td>
                <td className="px-4 py-3 flex gap-1">
                  <button onClick={() => setEditingUser(editingUser === u.id ? null : u.id)}
                    className="p-1.5 rounded hover:bg-muted text-muted-foreground hover:text-foreground">
                    <Edit className="w-4 h-4" />
                  </button>
                  <button onClick={() => { if (confirm('Delete user?')) deleteMutation.mutate(u.id); }}
                    className="p-1.5 rounded hover:bg-destructive/10 text-muted-foreground hover:text-destructive">
                    <Trash2 className="w-4 h-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function CameraManagement() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const canManage = user?.role === 'Admin' || user?.role === 'Operator';

  const { data: cameras = [] } = useQuery({ queryKey: ['cameras-all'], queryFn: () => cameraService.getAll() });

  const [showAdd, setShowAdd] = useState(false);
  const [discovering, setDiscovering] = useState(false);
  const [discovered, setDiscovered] = useState<any[]>([]);
  const [newCam, setNewCam] = useState({ name: '', ipAddress: '', port: 80, username: '', password: '', rtspUrl: '', storageProfileId: '', autoDiscover: true });
  const { data: storageProfiles = [] } = useQuery({ queryKey: ['storage-profiles'], queryFn: () => storageService.getAll() });

  const addMutation = useMutation({
    mutationFn: () => cameraService.create(newCam),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['cameras-all'] }); setShowAdd(false); setNewCam({ name: '', ipAddress: '', port: 80, username: '', password: '', rtspUrl: '', storageProfileId: '', autoDiscover: true }); toast.success('Camera added'); },
    onError: (err: any) => toast.error(err.message),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => cameraService.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['cameras-all'] }); toast.success('Camera deleted'); },
  });

  const discover = async () => {
    setDiscovering(true);
    try { const result = await cameraService.discover(); setDiscovered(result); }
    catch { toast.error('Discovery failed'); }
    setDiscovering(false);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Camera Management</h3>
        {canManage && (
          <div className="flex gap-2">
            <button onClick={discover} disabled={discovering}
              className="h-9 px-4 border rounded-lg text-sm hover:bg-muted flex items-center gap-2 disabled:opacity-50">
              <Search className={cn("w-4 h-4", discovering && "animate-spin")} /> Discover
            </button>
            <button onClick={() => setShowAdd(!showAdd)}
              className="h-9 px-4 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:bg-primary/90 flex items-center gap-2">
              <Plus className="w-4 h-4" /> Add Camera
            </button>
          </div>
        )}
      </div>

      {discovered.length > 0 && (
        <div className="bg-primary/5 border border-primary/20 rounded-xl p-4">
          <h4 className="text-sm font-medium mb-3">Discovered Cameras</h4>
          <div className="space-y-2">
            {discovered.map((d, i) => (
              <div key={i} className="flex items-center justify-between p-3 bg-card rounded-lg">
                <div>
                  <span className="font-medium text-sm">{d.manufacturer} {d.model}</span>
                  <span className="text-xs text-muted-foreground ml-3">{d.ipAddress}:{d.port}</span>
                </div>
                {d.isAlreadyAdded
                  ? <span className="text-xs text-success">Already added</span>
                  : <button onClick={() => { setNewCam({ ...newCam, ipAddress: d.ipAddress, port: d.port, name: `${d.manufacturer} ${d.model}` }); setShowAdd(true); }}
                    className="text-xs text-primary hover:underline">Add</button>
                }
              </div>
            ))}
          </div>
        </div>
      )}

      {showAdd && canManage && (
        <div className="bg-muted/50 rounded-xl p-4 space-y-3 border">
          <div className="grid grid-cols-2 gap-3">
            <input placeholder="Camera Name *" value={newCam.name} onChange={e => setNewCam({ ...newCam, name: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <input placeholder="IP Address *" value={newCam.ipAddress} onChange={e => setNewCam({ ...newCam, ipAddress: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <input placeholder="Port" type="number" value={newCam.port} onChange={e => setNewCam({ ...newCam, port: Number(e.target.value) })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <input placeholder="Username *" value={newCam.username} onChange={e => setNewCam({ ...newCam, username: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <input placeholder="Password *" type="password" value={newCam.password} onChange={e => setNewCam({ ...newCam, password: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <input placeholder="RTSP URL (optional override)" value={newCam.rtspUrl} onChange={e => setNewCam({ ...newCam, rtspUrl: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <select value={newCam.storageProfileId} onChange={e => setNewCam({ ...newCam, storageProfileId: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm">
              <option value="">Default Storage</option>
              {storageProfiles.map(sp => <option key={sp.id} value={sp.id}>{sp.name} ({sp.type})</option>)}
            </select>
            <label className="flex items-center gap-2 h-9 px-3">
              <input type="checkbox" checked={newCam.autoDiscover}
                onChange={e => setNewCam({ ...newCam, autoDiscover: e.target.checked })}
                className="rounded border-border" />
              <span className="text-sm">Auto-discover via ONVIF</span>
            </label>
          </div>
          <div className="flex gap-2">
            <button onClick={() => addMutation.mutate()} disabled={!newCam.name || !newCam.ipAddress || !newCam.username || !newCam.password}
              className="h-9 px-4 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50">Add Camera</button>
            <button onClick={() => setShowAdd(false)} className="h-9 px-4 bg-muted rounded-lg text-sm">Cancel</button>
          </div>
        </div>
      )}

      <div className="border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/50">
            <tr className="text-left text-muted-foreground">
              <th className="px-4 py-3 font-medium">Name</th>
              <th className="px-4 py-3 font-medium">IP</th>
              <th className="px-4 py-3 font-medium">Status</th>
              <th className="px-4 py-3 font-medium">Model</th>
              <th className="px-4 py-3 font-medium">Resolution</th>
              <th className="px-4 py-3 font-medium">Codec</th>
              {canManage && <th className="px-4 py-3 font-medium w-20">Actions</th>}
            </tr>
          </thead>
          <tbody className="divide-y">
            {cameras.map(cam => (
              <tr key={cam.id} className="hover:bg-muted/30">
                <td className="px-4 py-3 font-medium">{cam.name}</td>
                <td className="px-4 py-3 text-muted-foreground">{cam.ipAddress}:{cam.port}</td>
                <td className="px-4 py-3">
                  <span className={cn("inline-flex items-center gap-1.5 text-xs px-2 py-0.5 rounded-full",
                    cam.isOnline ? "bg-success/10 text-success" : "bg-destructive/10 text-destructive")}>
                    <span className={cn("w-1.5 h-1.5 rounded-full", cam.isOnline ? "bg-success" : "bg-destructive")} />
                    {cam.status}
                  </span>
                </td>
                <td className="px-4 py-3 text-muted-foreground text-xs">{cam.manufacturer} {cam.model}</td>
                <td className="px-4 py-3 text-muted-foreground text-xs">{cam.resolution_Width}×{cam.resolution_Height}</td>
                <td className="px-4 py-3 text-muted-foreground text-xs">{cam.codec}</td>
                {canManage && (
                  <td className="px-4 py-3">
                    {user?.role === 'Admin' && (
                      <button onClick={() => { if (confirm('Delete camera?')) deleteMutation.mutate(cam.id); }}
                        className="p-1.5 rounded hover:bg-destructive/10 text-muted-foreground hover:text-destructive">
                        <Trash2 className="w-4 h-4" />
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function StorageManagement() {
  const qc = useQueryClient();
  const { data: profiles = [] } = useQuery({ queryKey: ['storage-profiles'], queryFn: () => storageService.getAll() });
  const { data: storageTypes = [] } = useQuery({ queryKey: ['storage-types'], queryFn: () => storageService.getTypes() });

  const [showAdd, setShowAdd] = useState(false);
  const [newProfile, setNewProfile] = useState<any>({
    name: '', type: 'Local', isDefault: false, basePath: '', maxStorageBytes: 536870912000, retentionDays: 30,
    autoDeleteEnabled: true, host: '', port: null, username: '', password: '', shareName: '',
    region: '', accessKey: '', secretKey: '', containerName: '', connectionString: '',
  });

  const addMutation = useMutation({
    mutationFn: () => storageService.create(newProfile),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['storage-profiles'] }); setShowAdd(false); toast.success('Storage profile created'); },
    onError: (err: any) => toast.error(err.message),
  });

  const testMutation = useMutation({
    mutationFn: (id: string) => storageService.test(id),
    onSuccess: (data) => toast.success(data.message),
    onError: () => toast.error('Connection test failed'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => storageService.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['storage-profiles'] }); toast.success('Profile deleted'); },
    onError: (err: any) => toast.error(err.message),
  });

  const formatBytes = (b: number) => {
    if (b === 0) return '0 B';
    const k = 1024;
    const s = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(b) / Math.log(k));
    return parseFloat((b / Math.pow(k, i)).toFixed(1)) + ' ' + s[i];
  };

  const selectedType = newProfile.type;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Storage Profiles</h3>
        <button onClick={() => setShowAdd(!showAdd)}
          className="h-9 px-4 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:bg-primary/90 flex items-center gap-2">
          <Plus className="w-4 h-4" /> Add Profile
        </button>
      </div>

      {showAdd && (
        <div className="bg-muted/50 rounded-xl p-4 space-y-4 border">
          <h4 className="text-sm font-medium">Create Storage Profile</h4>
          <div className="grid grid-cols-2 gap-3">
            {/* Common fields */}
            <input placeholder="Profile Name *" value={newProfile.name} onChange={e => setNewProfile({ ...newProfile, name: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <select value={newProfile.type} onChange={e => setNewProfile({ ...newProfile, type: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm">
              {storageTypes.length > 0
                ? storageTypes.map(t => <option key={t} value={t}>{t}</option>)
                : <>
                    <option value="Local">Local</option>
                    <option value="NAS_SMB">NAS/SMB</option>
                    <option value="NAS_NFS">NAS/NFS</option>
                    <option value="S3">AWS S3</option>
                    <option value="AzureBlob">Azure Blob</option>
                    <option value="FTP">FTP</option>
                    <option value="SFTP">SFTP</option>
                  </>
              }
            </select>
            <input placeholder="Base Path" value={newProfile.basePath} onChange={e => setNewProfile({ ...newProfile, basePath: e.target.value })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <input placeholder="Retention Days" type="number" value={newProfile.retentionDays}
              onChange={e => setNewProfile({ ...newProfile, retentionDays: Number(e.target.value) })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />

            {/* Network-based storage: NAS_SMB, NAS_NFS, FTP, SFTP */}
            {(['NAS_SMB', 'NAS_NFS', 'FTP', 'SFTP'].includes(selectedType)) && (
              <>
                <input placeholder="Host *" value={newProfile.host} onChange={e => setNewProfile({ ...newProfile, host: e.target.value })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
                <input placeholder="Port" type="number" value={newProfile.port || ''} onChange={e => setNewProfile({ ...newProfile, port: Number(e.target.value) || null })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
                <input placeholder="Username" value={newProfile.username} onChange={e => setNewProfile({ ...newProfile, username: e.target.value })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
                <input placeholder="Password" type="password" value={newProfile.password} onChange={e => setNewProfile({ ...newProfile, password: e.target.value })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
                {(['NAS_SMB', 'NAS_NFS'].includes(selectedType)) && (
                  <input placeholder="Share Name" value={newProfile.shareName} onChange={e => setNewProfile({ ...newProfile, shareName: e.target.value })}
                    className="h-9 px-3 bg-background border rounded-lg text-sm" />
                )}
              </>
            )}

            {/* S3 */}
            {selectedType === 'S3' && (
              <>
                <input placeholder="Region *" value={newProfile.region} onChange={e => setNewProfile({ ...newProfile, region: e.target.value })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
                <input placeholder="Bucket Name *" value={newProfile.containerName} onChange={e => setNewProfile({ ...newProfile, containerName: e.target.value })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
                <input placeholder="Access Key *" value={newProfile.accessKey} onChange={e => setNewProfile({ ...newProfile, accessKey: e.target.value })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
                <input placeholder="Secret Key *" type="password" value={newProfile.secretKey} onChange={e => setNewProfile({ ...newProfile, secretKey: e.target.value })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
              </>
            )}

            {/* Azure Blob */}
            {selectedType === 'AzureBlob' && (
              <>
                <input placeholder="Container Name *" value={newProfile.containerName} onChange={e => setNewProfile({ ...newProfile, containerName: e.target.value })}
                  className="h-9 px-3 bg-background border rounded-lg text-sm" />
                <input placeholder="Connection String *" value={newProfile.connectionString} onChange={e => setNewProfile({ ...newProfile, connectionString: e.target.value })}
                  className="col-span-2 h-9 px-3 bg-background border rounded-lg text-sm" />
              </>
            )}

            <input placeholder="Max Storage (bytes)" type="number" value={newProfile.maxStorageBytes}
              onChange={e => setNewProfile({ ...newProfile, maxStorageBytes: Number(e.target.value) })}
              className="h-9 px-3 bg-background border rounded-lg text-sm" />
            <div className="flex items-center gap-4 h-9 px-3">
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={newProfile.autoDeleteEnabled}
                  onChange={e => setNewProfile({ ...newProfile, autoDeleteEnabled: e.target.checked })}
                  className="rounded border-border" />
                <span className="text-sm">Auto-delete</span>
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={newProfile.isDefault}
                  onChange={e => setNewProfile({ ...newProfile, isDefault: e.target.checked })}
                  className="rounded border-border" />
                <span className="text-sm">Set as default</span>
              </label>
            </div>
          </div>
          <div className="flex gap-2">
            <button onClick={() => addMutation.mutate()} disabled={!newProfile.name || !newProfile.type}
              className="h-9 px-4 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50">Create</button>
            <button onClick={() => setShowAdd(false)} className="h-9 px-4 bg-muted rounded-lg text-sm">Cancel</button>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {profiles.map(p => {
          const pct = p.usagePercent;
          return (
            <div key={p.id} className="bg-card border rounded-xl p-5">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h4 className="font-medium">{p.name}</h4>
                  <span className="text-xs text-muted-foreground">{p.type} {p.isDefault && '• Default'}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className={cn("w-2 h-2 rounded-full", p.isHealthy ? "bg-success" : "bg-destructive")} />
                  <span className="text-xs">{p.isHealthy ? 'Healthy' : 'Unhealthy'}</span>
                </div>
              </div>

              <div className="w-full bg-muted rounded-full h-2 mb-2">
                <div className={cn("h-2 rounded-full", pct > 85 ? "bg-warning" : "bg-primary")}
                  style={{ width: `${Math.min(pct, 100)}%` }} />
              </div>
              <div className="flex justify-between text-xs text-muted-foreground mb-4">
                <span>{formatBytes(p.usedStorageBytes)} used</span>
                <span>{formatBytes(p.maxStorageBytes)} total</span>
              </div>

              <div className="flex items-center gap-2 text-xs text-muted-foreground mb-2">
                <span>Retention: {p.retentionDays}d</span>
                <span>•</span>
                <span>Auto-delete: {p.autoDeleteEnabled ? 'On' : 'Off'}</span>
              </div>
              {p.healthError && (
                <p className="text-xs text-destructive mb-2">{p.healthError}</p>
              )}
              {p.lastHealthCheck && (
                <p className="text-[10px] text-muted-foreground mb-3">Last check: {new Date(p.lastHealthCheck).toLocaleString()}</p>
              )}

              <div className="flex gap-2">
                <button onClick={() => testMutation.mutate(p.id)}
                  className="h-8 px-3 border rounded-lg text-xs hover:bg-muted flex items-center gap-1">
                  <RefreshCw className="w-3 h-3" /> Test
                </button>
                {!p.isDefault && (
                  <button onClick={() => { if (confirm('Delete?')) deleteMutation.mutate(p.id); }}
                    className="h-8 px-3 border rounded-lg text-xs hover:bg-destructive/10 text-destructive flex items-center gap-1">
                    <Trash2 className="w-3 h-3" /> Delete
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function AccessControl() {
  const qc = useQueryClient();
  const { data: users = [] } = useQuery({ queryKey: ['users'], queryFn: () => userService.getAll() });
  const { data: cameras = [] } = useQuery({ queryKey: ['cameras-all'], queryFn: () => cameraService.getAll() });
  const [selectedUser, setSelectedUser] = useState<string | null>(null);
  const [permissions, setPermissions] = useState<any>(null);
  const [selectedCamera, setSelectedCamera] = useState<string | null>(null);
  const [cameraAccess, setCameraAccess] = useState<CameraAccessDto[]>([]);
  const [showGrantForm, setShowGrantForm] = useState(false);
  const [grantData, setGrantData] = useState({ userId: '', permission: 'View' });

  const loadPermissions = async (userId: string) => {
    setSelectedUser(userId);
    setSelectedCamera(null);
    try {
      const perms = await cameraAccessService.getUserPermissions(userId);
      setPermissions(perms);
    } catch { toast.error('Failed to load permissions'); }
  };

  const loadCameraAccess = async (cameraId: string) => {
    setSelectedCamera(cameraId);
    try {
      const access = await cameraAccessService.getAccess(cameraId);
      setCameraAccess(access);
    } catch { toast.error('Failed to load camera access'); }
  };

  const updatePermission = async (cameraId: string, cameraName: string, permission: string) => {
    if (!selectedUser) return;
    try {
      await cameraAccessService.bulkUpdatePermissions(selectedUser, [
        { cameraId, cameraName, permission, isExplicit: true }
      ]);
      loadPermissions(selectedUser);
      toast.success('Permission updated');
    } catch { toast.error('Failed to update'); }
  };

  const grantAccess = async (cameraId: string) => {
    try {
      await cameraAccessService.grantAccess(cameraId, grantData as GrantCameraAccessRequest);
      loadCameraAccess(cameraId);
      setShowGrantForm(false);
      toast.success('Access granted');
    } catch { toast.error('Failed to grant access'); }
  };

  const revokeAccess = async (cameraId: string, accessId: string) => {
    try {
      await cameraAccessService.revokeAccess(cameraId, accessId);
      loadCameraAccess(cameraId);
      toast.success('Access revoked');
    } catch { toast.error('Failed to revoke access'); }
  };

  const [viewMode, setViewMode] = useState<'by-user' | 'by-camera'>('by-user');

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Camera Access Control</h3>
        <div className="flex gap-1 bg-muted rounded-lg p-0.5">
          <button onClick={() => setViewMode('by-user')}
            className={cn("px-3 py-1.5 rounded-md text-xs transition-colors",
              viewMode === 'by-user' ? "bg-card shadow text-foreground" : "text-muted-foreground")}>
            By User
          </button>
          <button onClick={() => setViewMode('by-camera')}
            className={cn("px-3 py-1.5 rounded-md text-xs transition-colors",
              viewMode === 'by-camera' ? "bg-card shadow text-foreground" : "text-muted-foreground")}>
            By Camera
          </button>
        </div>
      </div>

      {viewMode === 'by-user' ? (
        <div className="flex gap-6">
          <div className="w-64 shrink-0 space-y-1">
            <h4 className="text-sm font-medium mb-3 text-muted-foreground">Select User</h4>
            {users.map(u => (
              <button key={u.id} onClick={() => loadPermissions(u.id)}
                className={cn("w-full text-left px-3 py-2.5 rounded-lg text-sm transition-colors",
                  selectedUser === u.id ? "bg-primary/10 text-primary" : "hover:bg-muted")}>
                <div className="font-medium">{u.username}</div>
                <div className="text-xs text-muted-foreground">{u.role}</div>
              </button>
            ))}
          </div>

          <div className="flex-1">
            {permissions ? (
              <div>
                <div className="flex items-center gap-3 mb-4">
                  <h4 className="font-medium">{permissions.username}</h4>
                  <span className="text-xs bg-muted px-2 py-0.5 rounded">{permissions.globalRole}</span>
                </div>
                <div className="border rounded-xl overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-muted/50">
                      <tr className="text-left text-muted-foreground">
                        <th className="px-4 py-3 font-medium">Camera</th>
                        <th className="px-4 py-3 font-medium">Permission</th>
                        <th className="px-4 py-3 font-medium">Type</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {permissions.cameraPermissions?.map((p: any) => (
                        <tr key={p.cameraId}>
                          <td className="px-4 py-3">{p.cameraName}</td>
                          <td className="px-4 py-3">
                            <select value={p.permission}
                              onChange={e => updatePermission(p.cameraId, p.cameraName, e.target.value)}
                              className="h-8 px-2 bg-muted rounded text-xs">
                              <option value="View">View</option>
                              <option value="Control">Control</option>
                              <option value="Record">Record</option>
                              <option value="Admin">Admin</option>
                            </select>
                          </td>
                          <td className="px-4 py-3">
                            <span className={cn("text-xs", p.isExplicit ? "text-primary" : "text-muted-foreground")}>
                              {p.isExplicit ? 'Explicit' : 'Inherited'}
                            </span>
                          </td>
                        </tr>
                      ))}
                      {cameras.filter(c => !permissions.cameraPermissions?.find((p: any) => p.cameraId === c.id)).map(cam => (
                        <tr key={cam.id} className="opacity-50">
                          <td className="px-4 py-3">{cam.name}</td>
                          <td className="px-4 py-3">
                            <select value=""
                              onChange={e => { if (e.target.value) updatePermission(cam.id, cam.name, e.target.value); }}
                              className="h-8 px-2 bg-muted rounded text-xs">
                              <option value="">No access</option>
                              <option value="View">View</option>
                              <option value="Control">Control</option>
                              <option value="Record">Record</option>
                              <option value="Admin">Admin</option>
                            </select>
                          </td>
                          <td className="px-4 py-3 text-xs text-muted-foreground">—</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : (
              <div className="flex items-center justify-center h-64 text-muted-foreground">
                <p>Select a user to manage their camera permissions</p>
              </div>
            )}
          </div>
        </div>
      ) : (
        /* By Camera view - uses per-camera access APIs */
        <div className="flex gap-6">
          <div className="w-64 shrink-0 space-y-1">
            <h4 className="text-sm font-medium mb-3 text-muted-foreground">Select Camera</h4>
            {cameras.map(cam => (
              <button key={cam.id} onClick={() => loadCameraAccess(cam.id)}
                className={cn("w-full text-left px-3 py-2.5 rounded-lg text-sm transition-colors",
                  selectedCamera === cam.id ? "bg-primary/10 text-primary" : "hover:bg-muted")}>
                <div className="font-medium">{cam.name}</div>
                <div className="text-xs text-muted-foreground">{cam.ipAddress}</div>
              </button>
            ))}
          </div>

          <div className="flex-1">
            {selectedCamera ? (
              <div>
                <div className="flex items-center justify-between mb-4">
                  <h4 className="font-medium">{cameras.find(c => c.id === selectedCamera)?.name} - Access List</h4>
                  <button onClick={() => { setShowGrantForm(!showGrantForm); setGrantData({ userId: '', permission: 'View' }); }}
                    className="h-8 px-3 bg-primary text-primary-foreground rounded-lg text-xs flex items-center gap-1">
                    <Plus className="w-3 h-3" /> Grant Access
                  </button>
                </div>

                {showGrantForm && (
                  <div className="bg-muted/50 border rounded-lg p-3 mb-4 flex items-center gap-3">
                    <select value={grantData.userId} onChange={e => setGrantData({ ...grantData, userId: e.target.value })}
                      className="h-8 px-2 bg-background border rounded text-xs flex-1">
                      <option value="">Select user...</option>
                      {users.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
                    </select>
                    <select value={grantData.permission} onChange={e => setGrantData({ ...grantData, permission: e.target.value })}
                      className="h-8 px-2 bg-background border rounded text-xs">
                      <option value="View">View</option>
                      <option value="Control">Control</option>
                      <option value="Record">Record</option>
                      <option value="Admin">Admin</option>
                    </select>
                    <button onClick={() => grantAccess(selectedCamera)} disabled={!grantData.userId}
                      className="h-8 px-3 bg-primary text-primary-foreground rounded text-xs disabled:opacity-50">Grant</button>
                    <button onClick={() => setShowGrantForm(false)} className="h-8 px-3 bg-muted rounded text-xs">Cancel</button>
                  </div>
                )}

                <div className="border rounded-xl overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-muted/50">
                      <tr className="text-left text-muted-foreground">
                        <th className="px-4 py-3 font-medium">User</th>
                        <th className="px-4 py-3 font-medium">Permission</th>
                        <th className="px-4 py-3 font-medium">Granted By</th>
                        <th className="px-4 py-3 font-medium">Granted At</th>
                        <th className="px-4 py-3 font-medium">Expires</th>
                        <th className="px-4 py-3 font-medium w-20">Actions</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {cameraAccess.map(a => (
                        <tr key={a.id} className="hover:bg-muted/30">
                          <td className="px-4 py-3 font-medium">{a.username}</td>
                          <td className="px-4 py-3">
                            <select value={a.permission}
                              onChange={e => {
                                cameraAccessService.updateAccess(selectedCamera, a.id, e.target.value)
                                  .then(() => { loadCameraAccess(selectedCamera); toast.success('Updated'); })
                                  .catch(() => toast.error('Failed'));
                              }}
                              className="h-7 px-2 bg-muted rounded text-xs">
                              <option value="View">View</option>
                              <option value="Control">Control</option>
                              <option value="Record">Record</option>
                              <option value="Admin">Admin</option>
                            </select>
                          </td>
                          <td className="px-4 py-3 text-xs text-muted-foreground">{a.grantedBy}</td>
                          <td className="px-4 py-3 text-xs text-muted-foreground">{new Date(a.grantedAt).toLocaleDateString()}</td>
                          <td className="px-4 py-3 text-xs text-muted-foreground">{a.expiresAt ? new Date(a.expiresAt).toLocaleDateString() : 'Never'}</td>
                          <td className="px-4 py-3">
                            <button onClick={() => { if (confirm('Revoke access?')) revokeAccess(selectedCamera, a.id); }}
                              className="p-1.5 rounded hover:bg-destructive/10 text-muted-foreground hover:text-destructive">
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </td>
                        </tr>
                      ))}
                      {cameraAccess.length === 0 && (
                        <tr><td colSpan={6} className="px-4 py-8 text-center text-muted-foreground text-sm">No explicit access grants for this camera</td></tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : (
              <div className="flex items-center justify-center h-64 text-muted-foreground">
                <p>Select a camera to manage access</p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
