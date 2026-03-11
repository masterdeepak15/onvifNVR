import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Move, ZoomIn, ZoomOut, Crosshair, Circle, Camera as CameraIcon,
  Download, Image as ImageIcon, X
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { ptzService, cameraService } from '@/lib/services';
import type { MyCameraDto } from '@/types/nvr';
import { toast } from 'sonner';

interface CameraControlsProps {
  camera: MyCameraDto;
  hub: any;
  onClose: () => void;
}

export function CameraControls({ camera, hub, onClose }: CameraControlsProps) {
  const [activeTab, setActiveTab] = useState<'ptz' | 'actions'>('actions');

  const { data: presets = [] } = useQuery({
    queryKey: ['ptz-presets', camera.id],
    queryFn: () => ptzService.getPresets(camera.id),
    enabled: camera.ptzCapable,
  });

  const ptzDown = (action: string) => {
    hub.ptzMove(camera.id, action, 0.5);
  };
  const ptzUp = () => {
    hub.ptzStop(camera.id);
  };

  const captureSnapshot = async () => {
    try {
      const blob = await cameraService.getSnapshot(camera.id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${camera.name}_snapshot_${new Date().toISOString().replace(/[:.]/g, '-')}.jpg`;
      a.click();
      URL.revokeObjectURL(url);
      toast.success('Snapshot saved');
    } catch {
      toast.error('Failed to capture snapshot');
    }
  };

  const toggleRecording = () => {
    if (camera.isRecording) {
      hub.stopRecording(camera.id);
    } else {
      hub.startRecording(camera.id);
    }
  };

  return (
    <div className="bg-card border rounded-xl shadow-xl w-72 overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-3 py-2 border-b bg-muted/30">
        <div className="flex items-center gap-2">
          <span className={cn("w-2 h-2 rounded-full", camera.isOnline ? "bg-success" : "bg-destructive")} />
          <span className="text-sm font-medium truncate">{camera.name}</span>
        </div>
        <button onClick={onClose} className="p-1 rounded hover:bg-muted">
          <X className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b">
        <button onClick={() => setActiveTab('actions')}
          className={cn("flex-1 text-xs py-2 transition-colors", activeTab === 'actions' ? "border-b-2 border-primary text-primary" : "text-muted-foreground")}>
          Actions
        </button>
        {camera.ptzCapable && camera.canControl && (
          <button onClick={() => setActiveTab('ptz')}
            className={cn("flex-1 text-xs py-2 transition-colors", activeTab === 'ptz' ? "border-b-2 border-primary text-primary" : "text-muted-foreground")}>
            PTZ
          </button>
        )}
      </div>

      <div className="p-3">
        {activeTab === 'actions' && (
          <div className="space-y-2">
            {/* Snapshot */}
            <button onClick={captureSnapshot} disabled={!camera.isOnline}
              className="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm hover:bg-muted disabled:opacity-40 transition-colors">
              <ImageIcon className="w-4 h-4 text-primary" /> Capture Snapshot
            </button>

            {/* Recording */}
            {camera.canRecord && (
              <button onClick={toggleRecording} disabled={!camera.isOnline}
                className={cn(
                  "w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm transition-colors disabled:opacity-40",
                  camera.isRecording ? "hover:bg-destructive/10 text-destructive" : "hover:bg-muted"
                )}>
                <Circle className={cn("w-4 h-4", camera.isRecording && "fill-current recording-pulse text-destructive")} />
                {camera.isRecording ? 'Stop Recording' : 'Start Recording'}
              </button>
            )}

            {/* Info */}
            <div className="pt-2 border-t space-y-1 text-xs text-muted-foreground">
              <div className="flex justify-between"><span>Resolution</span><span>{camera.resolution_Width}×{camera.resolution_Height}</span></div>
              <div className="flex justify-between"><span>FPS</span><span>{camera.framerate}</span></div>
              <div className="flex justify-between"><span>PTZ</span><span>{camera.ptzCapable ? 'Yes' : 'No'}</span></div>
              <div className="flex justify-between"><span>Permission</span><span>{camera.permission}</span></div>
            </div>
          </div>
        )}

        {activeTab === 'ptz' && camera.ptzCapable && (
          <div className="space-y-3">
            {/* D-Pad */}
            <div className="grid grid-cols-3 gap-1 w-28 mx-auto">
              <div />
              <button onPointerDown={() => ptzDown('MoveUp')} onPointerUp={ptzUp} onPointerLeave={ptzUp}
                className="p-2 rounded-md hover:bg-muted flex justify-center"><Move className="w-4 h-4 -rotate-90" /></button>
              <div />
              <button onPointerDown={() => ptzDown('MoveLeft')} onPointerUp={ptzUp} onPointerLeave={ptzUp}
                className="p-2 rounded-md hover:bg-muted flex justify-center"><Move className="w-4 h-4 rotate-180" /></button>
              <button onClick={() => ptzDown('Home')}
                className="p-2 rounded-md hover:bg-muted flex justify-center"><Crosshair className="w-4 h-4" /></button>
              <button onPointerDown={() => ptzDown('MoveRight')} onPointerUp={ptzUp} onPointerLeave={ptzUp}
                className="p-2 rounded-md hover:bg-muted flex justify-center"><Move className="w-4 h-4" /></button>
              <div />
              <button onPointerDown={() => ptzDown('MoveDown')} onPointerUp={ptzUp} onPointerLeave={ptzUp}
                className="p-2 rounded-md hover:bg-muted flex justify-center"><Move className="w-4 h-4 rotate-90" /></button>
              <div />
            </div>

            {/* Zoom */}
            <div className="flex gap-1 justify-center">
              <button onPointerDown={() => ptzDown('ZoomIn')} onPointerUp={ptzUp} onPointerLeave={ptzUp}
                className="p-2 rounded-md hover:bg-muted"><ZoomIn className="w-4 h-4" /></button>
              <button onPointerDown={() => ptzDown('ZoomOut')} onPointerUp={ptzUp} onPointerLeave={ptzUp}
                className="p-2 rounded-md hover:bg-muted"><ZoomOut className="w-4 h-4" /></button>
            </div>

            {/* Presets */}
            {presets.length > 0 && (
              <div className="pt-2 border-t">
                <p className="text-[10px] text-muted-foreground text-center mb-1">Presets</p>
                <div className="flex flex-wrap gap-1 justify-center">
                  {presets.map((p: any) => (
                    <button key={p.id || p.onvifToken}
                      onClick={() => hub.ptzGoToPreset(camera.id, p.id || p.onvifToken)}
                      className="text-[10px] px-2 py-1 rounded bg-muted hover:bg-primary hover:text-primary-foreground transition-colors">
                      {p.name}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
