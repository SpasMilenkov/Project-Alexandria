import type { MediaFileDto } from "@/api/streaming";
import type { SourceAnchor, SourceDescriptor } from "@/utils/player-source";

export type Origin = "context" | "queue" | "interrupt";
export type MediaKind = "audio" | "video";
export type AfterContextEnds = "stop" | "library" | "resume-previous";
export type ShufflePreference = Record<MediaKind, boolean>;
export type RepeatMode = "off" | "all" | "one";
export type AdvancementReason = "explicit" | "natural";

export interface NowPlaying {
  instanceId: number;
  file: MediaFileDto;
  origin: Origin;
  ended: boolean;
  restored: boolean;
}

export interface RangeResult {
  offset: number;
  scannedCount: number;
  total: number;
  items: { position: number; file: MediaFileDto }[];
}

export type RangeLoader = (start: number) => Promise<RangeResult>;

export interface Order {
  readonly total: number;
  at: (position: number) => MediaFileDto | null;
  isScanned: (position: number) => boolean;
  ensure: (position: number) => Promise<MediaFileDto | null>;
  locate: (match: (file: MediaFileDto) => boolean) => MediaFileDto | null;
  seed: (result: RangeResult) => void;
  prefetch: (position: number) => void;
  evictAround: (position: number) => void;
  dispose: () => Promise<void>;
}

export interface RangeBufferHooks {
  onEntries?: (entries: Map<number, MediaFileDto>) => void;
  onTotal?: (total: number) => void;
  onSession?: (sessionId: string, expiresAt: string, total: number) => void;
}

export interface ContextState {
  ref: SourceDescriptor;
  label: string;
  shuffled: boolean;
  anchor: SourceAnchor | null;
  cursor: number;
  sessionId: string | null;
  expiresAt: string | null;
  total: number;
}

export interface ContextSnapshot {
  ref: SourceDescriptor;
  label: string;
  shuffled: boolean;
  file: MediaFileDto;
}

export interface QueueEntry {
  id: string;
  file: MediaFileDto;
  group: string | null;
}

export interface UpNextItem {
  kind: "history" | "queue" | "context";
  file: MediaFileDto;
  queueIndex: number;
  position: number;
  group: string | null;
}

export interface PlayerError {
  message: string;
  retry: () => Promise<void>;
}

export interface PlayerNotice {
  id: number;
  message: string;
  actionLabel: string | null;
  onAction: (() => void) | null;
}
