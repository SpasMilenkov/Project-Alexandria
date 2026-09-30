export type MediaKind = "audio" | "video";

export interface EngineControls {
  play: () => void;
  pause: () => void;
  seek: (seconds: number) => void;
  setVolume: (volume: number) => void;
  selectVariant: (id: number | null) => void;
  setPlaybackRate: (rate: number) => void;
}

export type HistoryFlush = () => Promise<void>;

export const createEngineBridge = () => {
  const controls = new Map<MediaKind, EngineControls>();
  const flushers = new Set<HistoryFlush>();

  const active = (kind: MediaKind): EngineControls | null => controls.get(kind) ?? null;

  return {
    registerEngine: (kind: MediaKind, next: EngineControls): (() => void) => {
      controls.set(kind, next);
      return () => {
        if (controls.get(kind) === next) controls.delete(kind);
      };
    },
    registerHistoryFlush: (flush: HistoryFlush): (() => void) => {
      flushers.add(flush);
      return () => {
        flushers.delete(flush);
      };
    },
    flushHistory: async (): Promise<void> => {
      for (const flush of [...flushers]) await flush();
    },
    play: (kind: MediaKind) => active(kind)?.play(),
    pause: (kind: MediaKind) => active(kind)?.pause(),
    seek: (kind: MediaKind, seconds: number) => active(kind)?.seek(seconds),
    setVolume: (kind: MediaKind, volume: number) => active(kind)?.setVolume(volume),
    selectVariant: (kind: MediaKind, id: number | null) => active(kind)?.selectVariant(id),
    setPlaybackRate: (kind: MediaKind, rate: number) => active(kind)?.setPlaybackRate(rate),
  };
};
