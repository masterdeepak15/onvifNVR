import React, { useCallback } from 'react';
import { Camera, Circle, Maximize } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { MyCameraDto } from '@/types/nvr';

interface CameraCellProps {
  camera: MyCameraDto;
  frameRefs: React.MutableRefObject<Map<string, HTMLImageElement>>;
  isSelected: boolean;
  onSelect: () => void;
  onDoubleClick: () => void;
}

export function CameraCell({ camera, frameRefs, isSelected, onSelect, onDoubleClick }: CameraCellProps) {
  const imgRef = useCallback((el: HTMLImageElement | null) => {
    if (el) frameRefs.current.set(camera.id, el);
    else frameRefs.current.delete(camera.id);
  }, [camera.id]);

  return (
    <div
      className={cn(
        "grid-cell group cursor-pointer",
        isSelected && "ring-2 ring-primary"
      )}
      onClick={onSelect}
      onDoubleClick={onDoubleClick}
    >
      <img
        ref={imgRef}
        className="absolute inset-0 w-full h-full object-contain bg-background"
        alt={camera.name}
      />

      {/* Overlay */}
      <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-background/80 to-transparent p-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className={cn(
              "w-2 h-2 rounded-full",
              camera.isOnline ? "bg-success" : "bg-destructive"
            )} />
            <span className="text-xs font-medium truncate">{camera.name}</span>
          </div>
          <div className="flex items-center gap-1">
            {camera.isRecording && (
              <span className="flex items-center gap-1 text-[10px] text-recording">
                <Circle className="w-2 h-2 fill-current recording-pulse" /> REC
              </span>
            )}
            <button
              onClick={(e) => { e.stopPropagation(); onDoubleClick(); }}
              className="p-1 rounded opacity-0 group-hover:opacity-100 hover:bg-muted/50 transition-all"
            >
              <Maximize className="w-3 h-3" />
            </button>
          </div>
        </div>
      </div>

      {/* No signal */}
      {!camera.isOnline && (
        <div className="absolute inset-0 flex flex-col items-center justify-center bg-background/90">
          <Camera className="w-8 h-8 text-muted-foreground/30 mb-2" />
          <span className="text-xs text-muted-foreground">No Signal</span>
        </div>
      )}
    </div>
  );
}
