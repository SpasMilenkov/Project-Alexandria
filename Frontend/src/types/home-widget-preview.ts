import type { AuditLogResult } from "@/api/activity";
import type { DirectorySummaryDto, PaginatedResponse } from "@/api/directory";
import type { FileResult } from "@/api/file";
import type { TopSong } from "@/api/listening-rankings";
import type { PlaylistResponse } from "@/api/playlist";
import type { DailyServiceStatus, PublicServiceCurrent } from "@/api/publicStatus";
import type { WrappedDeckResponse } from "@/api/stats";
import type { StorageBreakdown } from "@/api/status";
import type { TranspilationJobResponse } from "@/api/streaming";
import type { usePlayerStore } from "@/stores/stream-player";

export interface HomeWidgetPreview {
  storage: StorageBreakdown;
  files: PaginatedResponse<FileResult>;
  folders: PaginatedResponse<DirectorySummaryDto>;
  currentStatus: PublicServiceCurrent[];
  statusHistory: DailyServiceStatus[];
  activity: PaginatedResponse<AuditLogResult>;
  topSongs: TopSong[];
  jobs: PaginatedResponse<TranspilationJobResponse>[];
  recap: WrappedDeckResponse;
  collections: PaginatedResponse<PlaylistResponse>;
  artwork: string;
  player: Pick<
    ReturnType<typeof usePlayerStore>,
    | "playbackOwnerId"
    | "activeFile"
    | "duration"
    | "currentTime"
    | "engineLoadError"
    | "error"
    | "orderBusy"
    | "engineReady"
    | "engineBuffering"
    | "isPlaying"
    | "hasPrevious"
    | "hasNext"
    | "togglePlay"
    | "previous"
    | "next"
    | "seek"
    | "playNow"
  >;
}
