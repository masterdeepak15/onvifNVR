import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useQuery } from '@tanstack/react-query';
import { cameraService, layoutService } from '@/lib/services';
import { useAuth } from '@/contexts/AuthContext';
import { useNvrHub } from '@/hooks/useNvrHub';
import type { MyCameraDto, CameraFrame, GridLayout, GridPosition } from '@/types/nvr';
import {
  Grid2X2, Grid3X3, ChevronLeft, ChevronRight,
  ChevronsLeft, ChevronsRight, Camera, Minimize,
  LayoutGrid, Square, RectangleHorizontal
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { CameraCell } from '@/components/liveview/CameraCell';
import { CameraControls } from '@/components/liveview/CameraControls';
import { LayoutManager } from '@/components/liveview/LayoutManager';

type MatrixSize = 1 | 2 | 3 | 4 | 8;
type LayoutMode = 'grid' | 'spotlight';

const MATRIX_OPTIONS: { size: MatrixSize; label: string; icon: React.ReactNode }[] = [
  { size: 1, label: '1×1', icon: <Square className="w-4 h-4" /> },
  { size: 2, label: '2×2', icon: <Grid2X2 className="w-4 h-4" /> },
  { size: 3, label: '3×3', icon: <Grid3X3 className="w-4 h-4" /> },
  { size: 4, label: '4×4', icon: <LayoutGrid className="w-4 h-4" /> },
  { size: 8, label: '8×8', icon: <RectangleHorizontal className="w-4 h-4" /> },
];

export default function LiveView() {
  const { accessToken } = useAuth();
  const hub = useNvrHub(accessToken);
  const [matrix, setMatrix] = useState<MatrixSize>(4);
  const [currentPage, setCurrentPage] = useState(0);
  const [selectedCamera, setSelectedCamera] = useState<string | null>(null);
  const [fullscreenCamera, setFullscreenCamera] = useState<string | null>(null);
  const [layoutName, setLayoutName] = useState('Default');
  const [layoutMode, setLayoutMode] = useState<LayoutMode>('grid');
  const [spotlightCameraId, setSpotlightCameraId] = useState<string | null>(null);
  const frameRefs = useRef<Map<string, HTMLImageElement>>(new Map());
  const subscribedRef = useRef<Set<string>>(new Set());
  const containerRef = useRef<HTMLDivElement>(null);

  const { data: cameras = [] } = useQuery({
    queryKey: ['my-cameras'],
    queryFn: () => cameraService.getMyCameras(),
    refetchInterval: 30000,
  });

  // Load initial layout
  const { data: savedLayout } = useQuery({
    queryKey: ['my-layout', layoutName],
    queryFn: () => cameraService.getMyLayout(layoutName),
    enabled: !!layoutName,
  });

  const cellsPerPage = matrix * matrix;
  const totalPages = Math.max(1, Math.ceil(cameras.length / cellsPerPage));
  const visibleCameras = cameras.slice(currentPage * cellsPerPage, (currentPage + 1) * cellsPerPage);

  // Subscribe to visible cameras
  useEffect(() => {
    if (!hub.isConnected) return;
    const visibleIds = new Set(visibleCameras.map(c => c.id));
    subscribedRef.current.forEach(id => {
      if (!visibleIds.has(id)) {
        hub.unsubscribe(id);
        subscribedRef.current.delete(id);
      }
    });
    visibleCameras.forEach(cam => {
      if (!subscribedRef.current.has(cam.id)) {
        hub.subscribe(cam.id);
        subscribedRef.current.add(cam.id);
      }
    });
    return () => {
      subscribedRef.current.forEach(id => hub.unsubscribe(id));
      subscribedRef.current.clear();
    };
  }, [hub.isConnected, visibleCameras.map(c => c.id).join(',')]);

  // Handle camera frames
  useEffect(() => {
    const unsub = hub.on('CameraFrame', (frame: CameraFrame) => {
      const img = frameRefs.current.get(frame.cameraId);
      if (img) img.src = `data:image/jpeg;base64,${frame.frame}`;
    });
    return unsub;
  }, [hub.on]);

  const goFirst = () => setCurrentPage(0);
  const goPrev = () => setCurrentPage(p => Math.max(0, p - 1));
  const goNext = () => setCurrentPage(p => Math.min(totalPages - 1, p + 1));
  const goLast = () => setCurrentPage(totalPages - 1);

  const handleDoubleClick = useCallback((cameraId: string) => {
    if (fullscreenCamera === cameraId) {
      setFullscreenCamera(null);
    } else {
      setFullscreenCamera(cameraId);
    }
  }, [fullscreenCamera]);

  const handleSelect = useCallback((cameraId: string) => {
    setSelectedCamera(prev => prev === cameraId ? null : cameraId);
  }, []);

  const toggleSpotlight = (cameraId: string) => {
    if (layoutMode === 'spotlight' && spotlightCameraId === cameraId) {
      setLayoutMode('grid');
      setSpotlightCameraId(null);
    } else {
      setLayoutMode('spotlight');
      setSpotlightCameraId(cameraId);
    }
  };

  // Fullscreen via browser API on double-click
  const exitFullscreen = () => {
    setFullscreenCamera(null);
    if (document.fullscreenElement) document.exitFullscreen().catch(() => {});
  };

  useEffect(() => {
    if (fullscreenCamera && containerRef.current) {
      containerRef.current.requestFullscreen?.().catch(() => {});
    }
    const onFsChange = () => {
      if (!document.fullscreenElement) setFullscreenCamera(null);
    };
    document.addEventListener('fullscreenchange', onFsChange);
    return () => document.removeEventListener('fullscreenchange', onFsChange);
  }, [fullscreenCamera]);

  const handleLayoutLoad = (layout: GridLayout) => {
    if (layout.gridColumns) {
      const m = layout.gridColumns as MatrixSize;
      if ([1, 2, 3, 4, 8].includes(m)) setMatrix(m);
    }
    setCurrentPage(0);
  };

  const getCurrentPositions = (): GridPosition[] => {
    return visibleCameras.map((cam, idx) => ({
      cameraId: cam.id,
      gridPosition: idx,
    }));
  };

  const selectedCameraData = cameras.find(c => c.id === selectedCamera);
  const fullscreenCameraData = cameras.find(c => c.id === fullscreenCamera);
  const spotlightData = cameras.find(c => c.id === spotlightCameraId);
  const otherCameras = spotlightCameraId
    ? visibleCameras.filter(c => c.id !== spotlightCameraId)
    : [];

  return (
    <div className="h-full flex flex-col" ref={containerRef}>
      {/* Toolbar */}
      {!fullscreenCamera && (
        <div className="h-14 border-b flex items-center justify-between px-4 shrink-0 bg-card">
          <div className="flex items-center gap-3">
            <h2 className="font-semibold">Live View</h2>
            <div className="h-5 w-px bg-border" />

            {/* Matrix selector */}
            <div className="flex items-center gap-0.5">
              {MATRIX_OPTIONS.map(opt => (
                <button
                  key={opt.size}
                  onClick={() => { setMatrix(opt.size); setCurrentPage(0); setLayoutMode('grid'); }}
                  className={cn(
                    "p-1.5 rounded-md transition-colors text-xs",
                    matrix === opt.size && layoutMode === 'grid'
                      ? "bg-primary text-primary-foreground"
                      : "text-muted-foreground hover:bg-muted"
                  )}
                  title={opt.label}
                >
                  {opt.icon}
                </button>
              ))}
            </div>

            <div className="h-5 w-px bg-border" />

            {/* Custom layout button */}
            <button
              onClick={() => {
                if (layoutMode === 'spotlight') {
                  setLayoutMode('grid');
                  setSpotlightCameraId(null);
                } else if (selectedCamera) {
                  toggleSpotlight(selectedCamera);
                }
              }}
              className={cn(
                "p-1.5 rounded-md transition-colors",
                layoutMode === 'spotlight' ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-muted"
              )}
              title="Spotlight: 1 large + others small (select a camera first)"
            >
              <RectangleHorizontal className="w-4 h-4" />
            </button>

            <div className="h-5 w-px bg-border" />

            {/* Layout manager */}
            <LayoutManager
              layoutName={layoutName}
              onLayoutNameChange={setLayoutName}
              currentPositions={getCurrentPositions()}
              gridColumns={matrix}
              onLayoutLoad={handleLayoutLoad}
            />
          </div>

          {/* Navigation */}
          <div className="flex items-center gap-1">
            <button onClick={goFirst} disabled={currentPage === 0}
              className="p-2 rounded-md text-muted-foreground hover:bg-muted disabled:opacity-30" title="First">
              <ChevronsLeft className="w-4 h-4" />
            </button>
            <button onClick={goPrev} disabled={currentPage === 0}
              className="p-2 rounded-md text-muted-foreground hover:bg-muted disabled:opacity-30" title="Previous">
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="text-sm px-3 text-muted-foreground">
              {currentPage + 1} / {totalPages}
            </span>
            <button onClick={goNext} disabled={currentPage >= totalPages - 1}
              className="p-2 rounded-md text-muted-foreground hover:bg-muted disabled:opacity-30" title="Next">
              <ChevronRight className="w-4 h-4" />
            </button>
            <button onClick={goLast} disabled={currentPage >= totalPages - 1}
              className="p-2 rounded-md text-muted-foreground hover:bg-muted disabled:opacity-30" title="Last">
              <ChevronsRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}

      {/* Main view */}
      <div className="flex-1 p-2 overflow-hidden relative">
        {/* Fullscreen single camera */}
        {fullscreenCamera && fullscreenCameraData ? (
          <div className="h-full flex flex-col bg-background">
            <div className="flex items-center justify-between px-4 py-2 bg-card border-b shrink-0">
              <div className="flex items-center gap-3">
                <span className={cn("w-2 h-2 rounded-full", fullscreenCameraData.isOnline ? "bg-success" : "bg-destructive")} />
                <span className="font-medium">{fullscreenCameraData.name}</span>
                <span className="text-xs text-muted-foreground">
                  {fullscreenCameraData.resolution_Width}×{fullscreenCameraData.resolution_Height} @ {fullscreenCameraData.framerate}fps
                </span>
              </div>
              <button onClick={exitFullscreen} className="p-2 rounded-md hover:bg-muted">
                <Minimize className="w-4 h-4" />
              </button>
            </div>
            <div className="flex-1 relative">
              <img
                ref={el => { if (el) frameRefs.current.set(fullscreenCameraData.id, el); }}
                className="w-full h-full object-contain bg-background"
                alt={fullscreenCameraData.name}
              />
            </div>
          </div>
        ) : layoutMode === 'spotlight' && spotlightData ? (
          /* Spotlight layout: 1 large + small sidebar */
          <div className="h-full flex gap-1">
            <div className="flex-1">
              <CameraCell
                camera={spotlightData}
                frameRefs={frameRefs}
                isSelected={selectedCamera === spotlightData.id}
                onSelect={() => handleSelect(spotlightData.id)}
                onDoubleClick={() => handleDoubleClick(spotlightData.id)}
              />
            </div>
            {otherCameras.length > 0 && (
              <div className="w-48 flex flex-col gap-1 overflow-auto">
                {otherCameras.map(cam => (
                  <div key={cam.id} className="h-28 shrink-0">
                    <CameraCell
                      camera={cam}
                      frameRefs={frameRefs}
                      isSelected={selectedCamera === cam.id}
                      onSelect={() => handleSelect(cam.id)}
                      onDoubleClick={() => handleDoubleClick(cam.id)}
                    />
                  </div>
                ))}
              </div>
            )}
          </div>
        ) : (
          /* Standard grid */
          <div
            className="grid h-full gap-1"
            style={{
              gridTemplateColumns: `repeat(${matrix}, 1fr)`,
              gridTemplateRows: `repeat(${matrix}, 1fr)`,
            }}
          >
            {visibleCameras.map(cam => (
              <CameraCell
                key={cam.id}
                camera={cam}
                frameRefs={frameRefs}
                isSelected={selectedCamera === cam.id}
                onSelect={() => handleSelect(cam.id)}
                onDoubleClick={() => handleDoubleClick(cam.id)}
              />
            ))}
            {[...Array(Math.max(0, cellsPerPage - visibleCameras.length))].map((_, i) => (
              <div key={`empty-${i}`} className="grid-cell flex items-center justify-center">
                <Camera className="w-8 h-8 text-muted-foreground/20" />
              </div>
            ))}
          </div>
        )}

        {/* Camera controls panel - shows when camera is selected */}
        {selectedCamera && selectedCameraData && !fullscreenCamera && (
          <div className="absolute top-4 right-4 z-10">
            <CameraControls
              camera={selectedCameraData}
              hub={hub}
              onClose={() => setSelectedCamera(null)}
            />
          </div>
        )}
      </div>
    </div>
  );
}
