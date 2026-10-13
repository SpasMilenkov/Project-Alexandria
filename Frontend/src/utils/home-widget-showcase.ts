import type { PaginatedResponse } from "@/api/directory";
import type { MediaFileDto } from "@/api/streaming";
import type { HomeLayout, HomeWidgetOptions } from "@/types/home";
import type { HomeWidgetPreview } from "@/types/home-widget-preview";

import { AuditEventCode, EntityType, LogSource, OperationType } from "@/api/activity";
import { HomeWidgetSize, type HomeWidgetType } from "@/enums/home-widget";
import { JobStatus } from "@/enums/job-status";
import { ServiceType } from "@/enums/service-type";
import { WrappedCardType } from "@/enums/wrapped-card-type";
import { homeLocalDay } from "@/utils/home-general";
import { homeWidgetFootprint } from "@/utils/home-grid";
import { defaultHomeWidgetOptions } from "@/utils/home-widget-options";

const page = <T>(items: T[]): PaginatedResponse<T> => ({
  items,
  currentPage: 1,
  pageSize: items.length,
  totalCount: items.length,
  totalPages: 1,
  hasPrevious: false,
  hasNext: false,
});

export const homeWidgetShowcaseLayout = (type: HomeWidgetType): HomeLayout => ({
  columns: 12,
  widgets: [HomeWidgetSize.Large, HomeWidgetSize.Medium, HomeWidgetSize.Small].map(
    (size, index) => ({
      instanceId: `showcase-${type}-${size}`,
      type,
      size,
      x: index === 2 ? 6 : 0,
      y: index === 0 ? 0 : 6,
      ...homeWidgetFootprint(size, 12),
      showTitle: false,
      options: {
        ...defaultHomeWidgetOptions(),
        countdownLabel: "Next adventure",
        countdownTarget: homeLocalDay(new Date(Date.now() + 12 * 86_400_000)),
        greetingHeading: "Welcome home",
        greetingSubtitle: "A little space for everything you love.",
      } satisfies HomeWidgetOptions,
    }),
  ),
});

const showcaseMedia = (): MediaFileDto[] =>
  ["Golden Hour", "Blue Monday", "Moonlit Drive", "After the Rain", "Daydream", "Slow Motion"].map(
    (title, index) => ({
      fileId: `showcase-song-${index}`,
      fileName: `${title}.flac`,
      mimeType: "audio/flac",
      currentVersionId: `showcase-version-${index}`,
      duration: 244 + index * 12,
      artist: ["The Sundials", "Northbound", "Paper Planes"][index % 3]!,
      album: "Open Skies",
      title,
      genre: "Indie",
      year: "2026",
      transpilationJobId: `showcase-job-${index}`,
      isVideo: false,
      segmentPrefix: null,
    }),
  );

export const createHomeWidgetPreview = (ownerId: string): HomeWidgetPreview => {
  const now = Date.now();
  const date = (hoursAgo: number) => new Date(now - hoursAgo * 3_600_000).toISOString();
  const owner = { id: ownerId, name: "Alex", email: "alex@example.test" };
  const media = showcaseMedia();
  const services = [ServiceType.Api, ServiceType.MediaPreviews, ServiceType.DocumentPreviews];
  const gib = 1024 ** 3;

  return {
    artwork: `data:image/svg+xml,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 400"><rect width="400" height="400" fill="#143d59"/><circle cx="280" cy="125" r="64" fill="#f4b66a"/><path d="M0 280 140 150 290 310 400 210V400H0" fill="#327b80"/><path d="M0 330 170 250 400 350V400H0" fill="#83b8a6"/></svg>')}`,
    storage: {
      sizeByType: {
        "image/jpeg": 8 * gib,
        "video/mp4": 6 * gib,
        "audio/flac": 4 * gib,
        "application/pdf": gib,
      },
      trashSize: gib,
      oldFiles: [],
      filesSize: 19 * gib,
      previewsSize: gib,
      previewsCount: 1248,
      transcodedSize: 2 * gib,
      representationsCount: 84,
      quotaBytes: 50 * gib,
      usedBytes: 22 * gib,
    },
    files: page(
      [
        "Mountain morning.jpg",
        "Project notes.pdf",
        "Golden Hour.flac",
        "Weekend reel.mp4",
        "Reading list.txt",
        "Sketchbook.png",
      ].map((fileName, index) => ({
        fileId: `showcase-file-${index}`,
        fileName,
        mimeType: [
          "image/jpeg",
          "application/pdf",
          "audio/flac",
          "video/mp4",
          "text/plain",
          "image/png",
        ][index]!,
        directoryId: null,
        createdAt: date(index + 1),
        updatedAt: date(index / 2),
        deletedAt: null,
        currentVersion: {
          id: `showcase-file-version-${index}`,
          size: "5242880",
          mimeType: "",
          versionNumber: 1,
          isDeleted: false,
          isEncrypted: false,
        },
        tags: [],
        owner,
      })),
    ),
    folders: page(
      ["Photography", "Documents", "Music", "Projects", "Travel", "Archive"].map((name, index) => ({
        id: `showcase-folder-${index}`,
        name,
        isStarred: true,
        parentId: "",
        createdAt: date(24),
        updatedAt: date(1),
        ownerUserDto: owner,
      })),
    ),
    currentStatus: services.map((serviceType) => ({ serviceType, status: "Healthy" })),
    statusHistory: services.map((serviceType) => ({
      serviceType,
      days: Array.from({ length: 30 }, (_, index) => ({
        date: date((29 - index) * 24).slice(0, 10),
        status: index === 9 ? "Degraded" : "Healthy",
      })),
    })),
    activity: page(
      [
        "Mountain morning.jpg",
        "Project notes.pdf",
        "Photography",
        "Weekend reel.mp4",
        "Reading list.txt",
      ].map((name, index) => ({
        operationType: OperationType.Create,
        entityType: EntityType.File,
        userId: ownerId,
        entityId: `showcase-file-${index}`,
        eventCode: AuditEventCode.FileCreated,
        metadata: JSON.stringify({ name }),
        timestamp: date(index * 8 + 1),
        logSource: LogSource.API,
      })),
    ),
    topSongs: media.map((file, index) => ({
      file,
      qualifiedPlayCount: 48 - index * 6,
      totalListenedSeconds: 7200 - index * 600,
    })),
    jobs: [JobStatus.Processing, JobStatus.Queued, JobStatus.Failed].map((status, index) =>
      page([
        {
          id: `showcase-job-${index}`,
          versionId: `showcase-version-${index}`,
          status,
          isVideo: true,
          progressPercent: index === 0 ? 64 : 0,
          retryCount: 0,
          fileName: ["Weekend reel.mp4", "Summer trip.mov", "Studio session.mkv"][index],
          errorDetail: status === JobStatus.Failed ? "Source file could not be read." : undefined,
          audioRungs: [],
          videoRungs: [],
          createdAt: date(1),
          representations: [],
        },
      ]),
    ),
    collections: page(
      ["The Sundials", "Evening listening", "Open Skies", "Indie favorites"].map((name, index) => ({
        id: `showcase-collection-${index}`,
        name,
        hasCover: false,
        itemCount: 12 + index * 7,
        isAutoGenerated: true,
        autoGroupKind: null,
        createdAt: date(24),
      })),
    ),
    recap: {
      from: date(30 * 24),
      to: date(0),
      summary: {
        seconds: 86400,
        sessions: 42,
        qualifiedPlayCount: 310,
        tracks: 84,
        artists: 26,
        activeDays: 21,
        knownArtistShare: 0.8,
        hasPriorHistory: true,
      },
      deck: {
        cards: [
          {
            type: WrappedCardType.TopArtists,
            headline: "Your month had a soundtrack",
            subline: "The Sundials led the way.",
            entries: [{ rank: 1, title: "The Sundials", seconds: 18000, plays: 64 }],
            visual: {
              colorKey: "teal",
              saturation: "medium",
              contrast: "medium",
              density: 0.6,
              fillLevel: 0.7,
              shapeGrammar: "circles",
            },
          },
          {
            type: WrappedCardType.TopSongs,
            headline: "One more time",
            subline: "Golden Hour, on repeat.",
            entries: [{ rank: 1, title: "Golden Hour", artist: "The Sundials", plays: 48 }],
            visual: {
              colorKey: "amber",
              saturation: "medium",
              contrast: "medium",
              density: 0.4,
              fillLevel: 0.6,
              shapeGrammar: "waves",
            },
          },
        ],
      },
    },
    player: {
      playbackOwnerId: ownerId,
      activeFile: media[0]!,
      duration: 244,
      currentTime: 91,
      engineLoadError: null,
      error: null,
      orderBusy: false,
      engineReady: true,
      engineBuffering: false,
      isPlaying: false,
      hasPrevious: true,
      hasNext: true,
      togglePlay: () => undefined,
      previous: () => Promise.resolve(),
      next: () => Promise.resolve(),
      seek: () => undefined,
      playNow: () => Promise.resolve(),
    },
  };
};
