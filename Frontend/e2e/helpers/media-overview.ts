import { type Page, type Route, expect } from "@playwright/test";

import { fixture, seed } from "./home-dashboard.js";

export const media = (isVideo: boolean, index: number) => ({
  fileId: `${isVideo ? "video" : "audio"}-${index}`,
  fileName: isVideo ? "film.mp4" : "track.mp3",
  title: isVideo ? `Film ${index + 1}` : `Song ${index + 1}`,
  artist: isVideo ? null : "The Alexandria Sessions",
  album: null,
  genre: null,
  year: null,
  duration: isVideo ? 5400 : 240,
  mimeType: isVideo ? "video/mp4" : "audio/mpeg",
  currentVersionId: "current-version",
  playbackVersionId: "playback-version",
  transpilationJobId: "job",
  isVideo,
  segmentPrefix: null,
});

const paginated = (items: unknown[], totalCount = items.length) => ({
  items,
  totalCount,
  currentPage: 1,
  pageSize: items.length,
  totalPages: 1,
});

const timeline = (url: URL, empty: boolean) => ({
  from: url.searchParams.get("from"),
  to: url.searchParams.get("to"),
  days: empty ? [] : [{ date: "2026-10-08", seconds: 3600, plays: 9, sessionCount: 10, top: [] }],
  months: [],
  totalSeconds: empty ? 0 : 3600,
  sessionCount: empty ? 0 : 10,
  qualifiedPlayCount: empty ? 0 : 9,
  activeDays: empty ? 0 : 1,
});

const fulfillRanking = async (route: Route, empty: boolean, failedJumpBackIn: boolean) => {
  const url = new URL(route.request().url());

  if (url.pathname.endsWith("/timeline")) {
    await route.fulfill({ json: timeline(url, empty) });

    return;
  }

  if (url.pathname.endsWith("/jump-back-in")) {
    if (failedJumpBackIn) {
      await route.fulfill({ status: 503, json: { message: "Unavailable" } });

      return;
    }

    await route.fulfill({
      json: {
        inactiveDays: 30,
        inactiveBefore: "2026-09-09T12:00:00Z",
        items: empty
          ? []
          : Array.from({ length: 6 }, (_, index) => ({
              file: { ...media(false, index + 20), title: `Old favorite ${index + 1}` },
              qualifiedPlayCount: 20 - index,
              totalListenedSeconds: 4000 - index * 100,
              lastPlayedAt: "2026-08-01T12:00:00Z",
            })),
      },
    });

    return;
  }

  await route.fulfill({
    json: {
      from: url.searchParams.get("from"),
      to: url.searchParams.get("to"),
      items: empty
        ? []
        : Array.from({ length: 6 }, (_, index) => ({
            file: { ...media(false, index), title: `Favorite ${index + 1}` },
            qualifiedPlayCount: 12 - index,
            totalListenedSeconds: (12 - index) * 240,
          })),
    },
  });
};

export const openMedia = async (
  page: Page,
  options: {
    empty?: boolean;
    failedVideo?: boolean;
    holdVideo?: boolean;
    thumbnails?: boolean;
    failedJumpBackIn?: boolean;
    longTitle?: boolean;
  } = {},
) => {
  const requests: URL[] = [];
  let failedVideo = options.failedVideo;
  let failedJumpBackIn = options.failedJumpBackIn;
  let release = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });

  await page.clock.setFixedTime(new Date("2026-10-09T12:00:00Z"));
  await fixture(
    page,
    seed(),
    { thumbnailsEnabled: options.thumbnails ?? true },
    undefined,
    async (route) => {
      const url = new URL(route.request().url());
      const path = url.pathname;

      if (path.endsWith("/thumbnail")) {
        const index = Number(path.match(/(?:audio|video)-(?<index>\d+)/u)?.groups?.index ?? 0);
        const colors = ["#8980a8", "#6d9691", "#ba8d73", "#6d819c", "#b6a26b", "#a2748f"];
        await route.fulfill({
          contentType: "image/svg+xml",
          body: `<svg xmlns="http://www.w3.org/2000/svg" width="400" height="400"><rect width="400" height="400" fill="${colors[index % colors.length]}"/><circle cx="200" cy="200" r="130" fill="#171c25"/><circle cx="200" cy="200" r="42" fill="${colors[index % colors.length]}"/></svg>`,
        });

        return true;
      }

      if (
        ![
          "/api/streaming/files",
          "/api/playlists",
          "/api/stream-history",
          "/api/stats/top-songs",
          "/api/stats/jump-back-in",
          "/api/stats/timeline",
        ].includes(path)
      )
        return false;

      requests.push(url);

      if (path.startsWith("/api/stats/")) {
        await fulfillRanking(route, Boolean(options.empty), Boolean(failedJumpBackIn));
      } else if (path === "/api/streaming/files") {
        const isVideo = url.searchParams.get("isVideo") === "true";

        if (isVideo && options.holdVideo) await held;
        if (isVideo && failedVideo) {
          await route.fulfill({ status: 503, json: { message: "Unavailable" } });

          return true;
        }

        const size = Number(url.searchParams.get("pageSize"));
        const items = options.empty
          ? []
          : Array.from({ length: size }, (_, index) => media(isVideo, index));
        await route.fulfill({ json: paginated(items, options.empty ? 0 : 123) });
      } else if (path === "/api/playlists") {
        const automatic = url.searchParams.get("isAutoGenerated") === "true";
        const items = options.empty
          ? []
          : Array.from({ length: automatic ? 6 : 4 }, (_, index) => ({
              id: `${automatic ? "collection" : "playlist"}-${index}`,
              name: `${automatic ? "Artist collection" : "My playlist"} ${index + 1}`,
              hasCover: false,
              itemCount: 18,
              isAutoGenerated: automatic,
              autoGroupKind: automatic ? 0 : null,
              createdAt: "2026-10-08T12:00:00Z",
            }));
        await route.fulfill({ json: paginated(items) });
      } else {
        const items = options.empty
          ? []
          : Array.from({ length: 5 }, (_, index) => ({
              id: `history-${index}`,
              fileId: `audio-${index}`,
              title:
                options.longTitle && index === 0
                  ? "An extraordinarily long song title that keeps going well past the width of the hero card (Extended Remix)"
                  : `Played recently ${index + 1}`,
              positionSeconds: 20,
              maxPositionReachedSeconds: 200,
              totalListenedSeconds: 500,
              qualifiedPlayCount: 2,
              hasFinished: false,
              lastAccessedAt: "2026-10-08T12:00:00Z",
              createdAt: "2026-10-08T12:00:00Z",
            }));
        await route.fulfill({ json: paginated(items) });
      }

      return true;
    },
  );

  await page.goto("/media");
  await expect(page.getByRole("heading", { name: "Media", exact: true })).toBeVisible();

  return {
    requests,
    release,
    recover: () => {
      failedVideo = false;
      failedJumpBackIn = false;
    },
  };
};

export const section = (page: Page, name: string) =>
  page.getByRole("region", { name, exact: true });

export const expectBoundedRequests = (requests: URL[]) => {
  expect(requests).toHaveLength(8);
  expect(
    Object.fromEntries(
      requests.find((url) => url.pathname === "/api/stats/jump-back-in")!.searchParams,
    ),
  ).toEqual({ limit: "10" });
  expect(
    Object.fromEntries(
      requests.find((url) => url.pathname === "/api/stats/top-songs")!.searchParams,
    ),
  ).toEqual({
    from: "2026-10-03T00:00:00.000Z",
    to: "2026-10-09T12:00:00.000Z",
    limit: "6",
  });
  expect(
    Object.fromEntries(
      requests.find((url) => url.pathname === "/api/stats/timeline")!.searchParams,
    ),
  ).toEqual({ from: "2026-10-03T00:00:00.000Z", to: "2026-10-09T12:00:00.000Z" });
  expect(
    requests
      .filter((url) => url.pathname === "/api/streaming/files")
      .map((url) => Object.fromEntries(url.searchParams)),
  ).toEqual([
    { page: "1", pageSize: "12", isVideo: "false" },
    { page: "1", pageSize: "8", isVideo: "true" },
  ]);
  // Both shelves fetch in parallel, so the request order follows layout, not behavior.
  expect(
    requests
      .filter((url) => url.pathname === "/api/playlists")
      .map((url) => Object.fromEntries(url.searchParams))
      .sort((a, b) => a.isAutoGenerated!.localeCompare(b.isAutoGenerated!)),
  ).toEqual([
    { page: "1", pageSize: "4", isAutoGenerated: "false", sort: "0" },
    { page: "1", pageSize: "10", isAutoGenerated: "true", sort: "0" },
  ]);
};
