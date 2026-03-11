import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useQuery } from '@tanstack/react-query';
import { cameraService, recordingService } from '@/lib/services';
import { useAuth } from '@/contexts/AuthContext';
import { useNvrHub } from '@/hooks/useNvrHub';
import type { MyCameraDto, CameraFrame, RecordingDto, PlaybackSession } from '@/types/nvr';
import {
  Play, Pause, SkipBack, SkipForward, Square, ChevronDown,
  Calendar, Clock, Camera, Tv, Image as ImageIcon, Download,
  Check
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';

export default function Playback() {
  const { accessToken } = useAuth();
  const hub = useNvrHub(accessToken);
  const [selectedCameras, setSelectedCameras] = useState<string[]>([]);
  const [seekTimestamp, setSeekTimestamp] = useState('');
  const [seekDate, setSeekDate] = useState('');
  const [speed, setSpeed] = useState(1.0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackPosition, setPlaybackPosition] = useState<string | null>(null);
  const [session, setSession] = useState<PlaybackSession | null>(null);
  const frameRefs = useRef<Map<string, HTMLImageElement>>(new Map());
  const [showCameraPicker, setShowCameraPicker] = useState(false);

  const { data: cameras = [] } = useQuery({
    queryKey: ['my-cameras'],
    queryFn: () => cameraService.getMyCameras(),
  });

  const { data: recordings } = useQuery({
    queryKey: ['recordings', selectedCameras, seekDate],
    queryFn: () => recordingService.search({
      cameraIds: selectedCameras.join(','),
      startTime: seekDate ? `${seekDate}T00:00:00Z` : undefined,
      endTime: seekDate ? `${seekDate}T23:59:59Z` : undefined,
      pageSize: 100,
    }),
    enabled: selectedCameras.length > 0,
  });

  // Listen for playback frames
  useEffect(() => {
    const unsubs = [
      hub.on('CameraFrame', (frame: CameraFrame) => {
        const img = frameRefs.current.get(frame.cameraId);
        if (img) img.src = `data:image/jpeg;base64,${frame.frame}`;
      }),
      hub.on('PlaybackPosition', (pos: any) => {
        setPlaybackPosition(pos.position);
      }),
      hub.on('StreamState', (state: any) => {
        if (state.state === 'Playing') setIsPlaying(true);
        else if (state.state === 'Paused' || state.state === 'Stopped') setIsPlaying(false);
      }),
    ];
    return () => unsubs.forEach(u => u());
  }, [hub.on]);

  const startPlayback = async () => {
    if (selectedCameras.length === 0 || !seekDate || !seekTimestamp) return;
    const ts = `${seekDate}T${seekTimestamp}:00Z`;
    try {
      const sess = await recordingService.startPlayback(selectedCameras, ts, speed);
      setSession(sess);
      for (const cam of selectedCameras) {
        await hub.subscribe(cam);
        await hub.streamControl({ cameraId: cam, command: 'Play', seekTo: ts, speed });
      }
      setIsPlaying(true);
    } catch (err) {
      console.error('Playback failed:', err);
      toast.error('Playback failed');
    }
  };

  const togglePause = () => {
    selectedCameras.forEach(id => {
      hub.streamControl({ cameraId: id, command: isPlaying ? 'Pause' : 'Resume' });
    });
    setIsPlaying(!isPlaying);
  };

  const stopPlayback = () => {
    selectedCameras.forEach(id => {
      hub.streamControl({ cameraId: id, command: 'Stop' });
    });
    setIsPlaying(false);
    setSession(null);
  };

  const setPlaybackSpeed = (newSpeed: number) => {
    setSpeed(newSpeed);
    selectedCameras.forEach(id => {
      hub.streamControl({ cameraId: id, command: 'SetSpeed', speed: newSpeed });
    });
  };

  const goLive = () => {
    selectedCameras.forEach(id => {
      hub.streamControl({ cameraId: id, command: 'GoLive' });
    });
  };

  const toggleCamera = (id: string) => {
    setSelectedCameras(prev =>
      prev.includes(id) ? prev.filter(c => c !== id) : [...prev, id]
    );
  };

  const selectAllCameras = () => {
    if (selectedCameras.length === cameras.length) {
      setSelectedCameras([]);
    } else {
      setSelectedCameras(cameras.map(c => c.id));
    }
  };

  const captureSnapshot = async (cameraId: string) => {
    try {
      const blob = await cameraService.getSnapshot(cameraId);
      const url = URL.createObjectURL(blob);
      const cam = cameras.find(c => c.id === cameraId);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${cam?.name || 'camera'}_playback_${new Date().toISOString().replace(/[:.]/g, '-')}.jpg`;
      a.click();
      URL.revokeObjectURL(url);
      toast.success('Snapshot saved');
    } catch {
      toast.error('Failed to capture snapshot');
    }
  };

  const downloadRecording = async (rec: RecordingDto) => {
    try {
      const blob = await cameraService.getSnapshot(rec.cameraId);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${rec.cameraName}_${new Date(rec.startTime).toISOString().replace(/[:.]/g, '-')}.mp4`;
      a.click();
      URL.revokeObjectURL(url);
      toast.success('Download started');
    } catch {
      toast.error('Download failed');
    }
  };

  const gridCols = selectedCameras.length <= 1 ? 1 : selectedCameras.length <= 4 ? 2 : selectedCameras.length <= 9 ? 3 : 4;

  return (
    <div className="h-full flex flex-col">
      {/* Toolbar */}
      <div className="border-b bg-card px-4 py-3 shrink-0">
        <div className="flex items-center justify-between flex-wrap gap-3">
          <div className="flex items-center gap-3">
            <h2 className="font-semibold">Playback</h2>
            <div className="h-5 w-px bg-border" />

            {/* Camera selector */}
            <div className="relative">
              <button
                onClick={() => setShowCameraPicker(!showCameraPicker)}
                className="flex items-center gap-2 h-9 px-3 bg-muted rounded-lg text-sm hover:bg-muted/80"
              >
                <Camera className="w-4 h-4" />
                {selectedCameras.length === 0 ? 'Select Cameras' : `${selectedCameras.length} camera${selectedCameras.length > 1 ? 's' : ''}`}
                <ChevronDown className="w-3 h-3" />
              </button>
              {showCameraPicker && (
                <div className="absolute top-full mt-1 left-0 bg-card border rounded-lg shadow-xl z-50 w-72 p-2 max-h-72 overflow-auto">
                  {/* Select all */}
                  <label className="flex items-center gap-3 px-3 py-2 rounded-md hover:bg-muted cursor-pointer border-b mb-1 pb-2">
                    <input
                      type="checkbox"
                      checked={selectedCameras.length === cameras.length && cameras.length > 0}
                      onChange={selectAllCameras}
                      className="rounded border-border"
                    />
                    <span className="text-sm font-medium">Select All ({cameras.length})</span>
                  </label>
                  {cameras.map(cam => (
                    <label key={cam.id} className="flex items-center gap-3 px-3 py-2 rounded-md hover:bg-muted cursor-pointer">
                      <input
                        type="checkbox"
                        checked={selectedCameras.includes(cam.id)}
                        onChange={() => toggleCamera(cam.id)}
                        className="rounded border-border"
                      />
                      <span className="text-sm">{cam.name}</span>
                      <span className={cn("ml-auto w-2 h-2 rounded-full", cam.isOnline ? "bg-success" : "bg-destructive")} />
                    </label>
                  ))}
                </div>
              )}
            </div>

            <div className="h-5 w-px bg-border" />

            {/* Date/Time picker */}
            <div className="flex items-center gap-2">
              <div className="flex items-center gap-1.5">
                <Calendar className="w-4 h-4 text-muted-foreground" />
                <input
                  type="date"
                  value={seekDate}
                  onChange={e => setSeekDate(e.target.value)}
                  className="h-9 px-3 bg-muted border-none rounded-lg text-sm focus:ring-1 focus:ring-primary"
                />
              </div>
              <div className="flex items-center gap-1.5">
                <Clock className="w-4 h-4 text-muted-foreground" />
                <input
                  type="time"
                  value={seekTimestamp}
                  onChange={e => setSeekTimestamp(e.target.value)}
                  className="h-9 px-3 bg-muted border-none rounded-lg text-sm focus:ring-1 focus:ring-primary"
                />
              </div>
            </div>
          </div>

          {/* Playback controls */}
          <div className="flex items-center gap-2">
            <button onClick={startPlayback} disabled={selectedCameras.length === 0 || !seekDate || !seekTimestamp}
              className="h-9 px-4 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:bg-primary/90 disabled:opacity-50 flex items-center gap-2">
              <Play className="w-4 h-4" /> Play
            </button>
            <button onClick={togglePause} disabled={!session}
              className="p-2 rounded-lg hover:bg-muted disabled:opacity-30">
              {isPlaying ? <Pause className="w-4 h-4" /> : <Play className="w-4 h-4" />}
            </button>
            <button onClick={stopPlayback} disabled={!session}
              className="p-2 rounded-lg hover:bg-muted disabled:opacity-30">
              <Square className="w-4 h-4" />
            </button>
            <div className="h-5 w-px bg-border" />

            {/* Speed */}
            <select
              value={speed}
              onChange={e => setPlaybackSpeed(Number(e.target.value))}
              className="h-9 px-2 bg-muted rounded-lg text-sm"
            >
              {[0.25, 0.5, 1.0, 2.0, 4.0, 8.0].map(s => (
                <option key={s} value={s}>{s}×</option>
              ))}
            </select>

            <button onClick={goLive} disabled={!session}
              className="h-9 px-3 border rounded-lg text-sm hover:bg-muted disabled:opacity-30 flex items-center gap-2">
              <Tv className="w-4 h-4" /> Go Live
            </button>
          </div>
        </div>

        {/* Playback position */}
        {playbackPosition && (
          <div className="mt-2 text-xs text-muted-foreground">
            Playback: {new Date(playbackPosition).toLocaleString()}
          </div>
        )}
      </div>

      {/* Playback grid */}
      <div className="flex-1 p-2 overflow-hidden">
        {selectedCameras.length === 0 ? (
          <div className="h-full flex flex-col items-center justify-center text-muted-foreground">
            <Camera className="w-16 h-16 mb-4 opacity-20" />
            <p className="text-lg font-medium mb-2">Select cameras to start playback</p>
            <p className="text-sm">Choose one or more cameras and set a date/time to begin</p>
          </div>
        ) : (
          <div
            className="grid h-full gap-1"
            style={{
              gridTemplateColumns: `repeat(${gridCols}, 1fr)`,
              gridTemplateRows: `repeat(${Math.ceil(selectedCameras.length / gridCols)}, 1fr)`,
            }}
          >
            {selectedCameras.map(camId => {
              const cam = cameras.find(c => c.id === camId);
              const stream = session?.cameraStreams?.find(s => s.cameraId === camId);
              return (
                <div key={camId} className="grid-cell group">
                  <img
                    ref={el => { if (el) frameRefs.current.set(camId, el); }}
                    className="absolute inset-0 w-full h-full object-contain bg-background"
                    alt={cam?.name || camId}
                  />
                  <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-background/80 to-transparent p-2">
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-medium">{cam?.name || 'Unknown'}</span>
                      {stream && !stream.hasRecording && (
                        <span className="text-[10px] text-warning">No recording</span>
                      )}
                      {/* Per-camera actions */}
                      <div className="ml-auto flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                        <button onClick={() => captureSnapshot(camId)} title="Capture Snapshot"
                          className="p-1 rounded hover:bg-muted/50">
                          <ImageIcon className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                  </div>

                  {/* Timeline bar */}
                  {stream?.timeline && stream.timeline.length > 0 && (
                    <div className="absolute inset-x-0 top-0 h-1">
                      {stream.timeline.map((seg, i) => (
                        <div key={i} className={cn(
                          "absolute h-full",
                          seg.hasMotion ? "bg-warning" : "bg-primary/60"
                        )} style={{
                          left: '0%', width: '100%',
                        }} />
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Recording list */}
      {recordings && recordings.items.length > 0 && (
        <div className="border-t bg-card px-4 py-3 shrink-0 max-h-48 overflow-auto">
          <h4 className="text-sm font-medium mb-2">Recordings ({recordings.totalCount})</h4>
          <div className="space-y-1">
            {recordings.items.slice(0, 10).map(rec => (
              <div key={rec.id}
                className="flex items-center gap-3 text-xs p-2 rounded hover:bg-muted cursor-pointer"
                onClick={() => {
                  setSeekTimestamp(new Date(rec.startTime).toTimeString().slice(0, 5));
                }}
              >
                <span className="font-medium w-32 truncate">{rec.cameraName}</span>
                <span className="text-muted-foreground">{new Date(rec.startTime).toLocaleTimeString()}</span>
                <span className="text-muted-foreground">→</span>
                <span className="text-muted-foreground">{rec.endTime ? new Date(rec.endTime).toLocaleTimeString() : 'Recording...'}</span>
                <span className={cn(
                  "px-1.5 py-0.5 rounded",
                  rec.triggerType === 'Motion' ? "bg-warning/10 text-warning" :
                  rec.triggerType === 'Manual' ? "bg-primary/10 text-primary" :
                  "bg-muted text-muted-foreground"
                )}>{rec.triggerType}</span>
                <span className="text-muted-foreground">{Math.round(rec.durationSeconds / 60)}min</span>
                <button onClick={e => { e.stopPropagation(); downloadRecording(rec); }} title="Download"
                  className="ml-auto p-1 rounded hover:bg-primary/10 text-muted-foreground hover:text-primary">
                  <Download className="w-3.5 h-3.5" />
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
