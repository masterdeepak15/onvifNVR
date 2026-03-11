import { useEffect, useRef, useCallback, useState } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import type { StreamControlCommand, PtzCommandDto } from '@/types/nvr';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

export function useNvrHub(accessToken: string | null) {
  const connRef = useRef<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const handlersRef = useRef<Map<string, Set<Function>>>(new Map());

  const on = useCallback((event: string, handler: Function) => {
    if (!handlersRef.current.has(event)) {
      handlersRef.current.set(event, new Set());
    }
    handlersRef.current.get(event)!.add(handler);
    connRef.current?.on(event, handler as any);
    return () => {
      handlersRef.current.get(event)?.delete(handler);
      connRef.current?.off(event, handler as any);
    };
  }, []);

  useEffect(() => {
    if (!accessToken) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/nvr?access_token=${accessToken}`)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    // Register existing handlers
    handlersRef.current.forEach((handlers, event) => {
      handlers.forEach(handler => {
        connection.on(event, handler as any);
      });
    });

    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));
    connection.onclose(() => setIsConnected(false));

    connection.start().then(() => {
      connRef.current = connection;
      setIsConnected(true);
      connection.invoke('SubscribeToEvents').catch(console.error);
    }).catch(console.error);

    return () => {
      connection.stop();
      connRef.current = null;
      setIsConnected(false);
    };
  }, [accessToken]);

  const invoke = useCallback(async (method: string, ...args: any[]) => {
    if (!connRef.current) return;
    return connRef.current.invoke(method, ...args);
  }, []);

  // ── Live Stream ──
  const subscribe = useCallback((cameraId: string) => invoke('SubscribeToCamera', cameraId), [invoke]);
  const unsubscribe = useCallback((cameraId: string) => invoke('UnsubscribeFromCamera', cameraId), [invoke]);

  // ── Stream Control (Play/Pause/Resume/Stop/Seek/Speed/Zoom/GoLive) ──
  const streamControl = useCallback((cmd: StreamControlCommand) => invoke('StreamControl', cmd), [invoke]);

  // Convenience wrappers
  const play = useCallback((cameraId: string, seekTo?: string, speed?: number) =>
    streamControl({ cameraId, command: 'Play', seekTo, speed }), [streamControl]);
  const pause = useCallback((cameraId: string) =>
    streamControl({ cameraId, command: 'Pause' }), [streamControl]);
  const resume = useCallback((cameraId: string) =>
    streamControl({ cameraId, command: 'Resume' }), [streamControl]);
  const stop = useCallback((cameraId: string) =>
    streamControl({ cameraId, command: 'Stop' }), [streamControl]);
  const seek = useCallback((cameraId: string, seekTo: string) =>
    streamControl({ cameraId, command: 'Seek', seekTo }), [streamControl]);
  const setSpeed = useCallback((cameraId: string, speed: number) =>
    streamControl({ cameraId, command: 'SetSpeed', speed }), [streamControl]);
  const digitalZoomIn = useCallback((cameraId: string) =>
    streamControl({ cameraId, command: 'ZoomIn' }), [streamControl]);
  const digitalZoomOut = useCallback((cameraId: string) =>
    streamControl({ cameraId, command: 'ZoomOut' }), [streamControl]);
  const digitalZoomReset = useCallback((cameraId: string) =>
    streamControl({ cameraId, command: 'ZoomReset' }), [streamControl]);
  const goLive = useCallback((cameraId: string) =>
    streamControl({ cameraId, command: 'GoLive' }), [streamControl]);

  // ── PTZ Control ──
  const ptzCommand = useCallback((cmd: PtzCommandDto) => invoke('PtzCommand', cmd), [invoke]);

  // PTZ convenience: directional move (continuous)
  const ptzMove = useCallback((cameraId: string, action: string, speed = 0.5) =>
    ptzCommand({ cameraId, action, speed, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const ptzStop = useCallback((cameraId: string) =>
    ptzMove(cameraId, 'Stop', 0), [ptzMove]);
  const ptzHome = useCallback((cameraId: string) =>
    ptzMove(cameraId, 'Home', 0), [ptzMove]);
  const ptzAbsoluteMove = useCallback((cameraId: string, pan: number, tilt: number, zoom: number) =>
    ptzCommand({ cameraId, action: 'AbsoluteMove', speed: 0.5, pan, tilt, zoom }), [ptzCommand]);
  const ptzRelativeMove = useCallback((cameraId: string, pan: number, tilt: number, zoom: number) =>
    ptzCommand({ cameraId, action: 'RelativeMove', speed: 0.5, pan, tilt, zoom }), [ptzCommand]);

  // PTZ presets
  const ptzGoToPreset = useCallback((cameraId: string, presetToken: string) =>
    ptzCommand({ cameraId, action: 'GoToPreset', speed: 0.5, pan: 0, tilt: 0, zoom: 0, presetToken }), [ptzCommand]);
  const ptzSavePreset = useCallback((cameraId: string, presetName: string) =>
    ptzCommand({ cameraId, action: 'SavePreset', speed: 0, pan: 0, tilt: 0, zoom: 0, presetName }), [ptzCommand]);
  const ptzDeletePreset = useCallback((cameraId: string, presetToken: string) =>
    ptzCommand({ cameraId, action: 'DeletePreset', speed: 0, pan: 0, tilt: 0, zoom: 0, presetToken }), [ptzCommand]);

  // Focus
  const focusNear = useCallback((cameraId: string, speed = 0.5) =>
    ptzCommand({ cameraId, action: 'FocusNear', speed, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const focusFar = useCallback((cameraId: string, speed = 0.5) =>
    ptzCommand({ cameraId, action: 'FocusFar', speed, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const focusStop = useCallback((cameraId: string) =>
    ptzCommand({ cameraId, action: 'FocusStop', speed: 0, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const focusAuto = useCallback((cameraId: string) =>
    ptzCommand({ cameraId, action: 'FocusAuto', speed: 0, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const focusManual = useCallback((cameraId: string) =>
    ptzCommand({ cameraId, action: 'FocusManual', speed: 0, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const focusAbsolute = useCallback((cameraId: string, position: number, speed = 0.5) =>
    ptzCommand({ cameraId, action: 'FocusAbsolute', speed, pan: 0, tilt: 0, zoom: position }), [ptzCommand]);

  // Iris
  const irisOpen = useCallback((cameraId: string) =>
    ptzCommand({ cameraId, action: 'IrisOpen', speed: 0, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const irisClose = useCallback((cameraId: string) =>
    ptzCommand({ cameraId, action: 'IrisClose', speed: 0, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const irisSet = useCallback((cameraId: string, level: number) =>
    ptzCommand({ cameraId, action: 'IrisSet', speed: 0, pan: 0, tilt: 0, zoom: level }), [ptzCommand]);
  const irisAuto = useCallback((cameraId: string) =>
    ptzCommand({ cameraId, action: 'IrisAuto', speed: 0, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);
  const irisManual = useCallback((cameraId: string) =>
    ptzCommand({ cameraId, action: 'IrisManual', speed: 0, pan: 0, tilt: 0, zoom: 0 }), [ptzCommand]);

  // ── Recording ──
  const startRecording = useCallback((cameraId: string) => invoke('StartRecording', cameraId), [invoke]);
  const stopRecording = useCallback((cameraId: string) => invoke('StopRecording', cameraId), [invoke]);

  // ── Snapshot ──
  const requestSnapshot = useCallback((cameraId: string) => invoke('RequestSnapshot', cameraId), [invoke]);

  // ── Alerts ──
  const acknowledgeAlert = useCallback((alertId: string) => invoke('AcknowledgeAlert', alertId), [invoke]);

  // ── PTZ Status ──
  const getPtzStatus = useCallback((cameraId: string) => invoke('GetPtzStatus', cameraId), [invoke]);

  return {
    isConnected,
    on,
    invoke,
    // Live
    subscribe,
    unsubscribe,
    // Stream control
    streamControl,
    play,
    pause,
    resume,
    stop,
    seek,
    setSpeed,
    digitalZoomIn,
    digitalZoomOut,
    digitalZoomReset,
    goLive,
    // PTZ
    ptzCommand,
    ptzMove,
    ptzStop,
    ptzHome,
    ptzAbsoluteMove,
    ptzRelativeMove,
    ptzGoToPreset,
    ptzSavePreset,
    ptzDeletePreset,
    // Focus
    focusNear,
    focusFar,
    focusStop,
    focusAuto,
    focusManual,
    focusAbsolute,
    // Iris
    irisOpen,
    irisClose,
    irisSet,
    irisAuto,
    irisManual,
    // Recording
    startRecording,
    stopRecording,
    // Snapshot
    requestSnapshot,
    // Alerts
    acknowledgeAlert,
    // PTZ status
    getPtzStatus,
  };
}
