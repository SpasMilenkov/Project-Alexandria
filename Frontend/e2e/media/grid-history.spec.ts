import { fixture, seed } from "@e2e/helpers/home-dashboard.js";
import { media } from "@e2e/helpers/media-overview.js";
import { type Page, type Route, expect, test } from "@playwright/test";

const paginated = (items: unknown[]) => ({
  items,
  totalCount: items.length,
  currentPage: 1,
  pageSize: items.length,
  totalPages: 1,
});

const historyEntry = (
  fileId: string,
  title: string,
  positionSeconds: number,
  hasFinished: boolean,
  isVideo = false,
) => ({
  id: `history-${fileId}`,
  fileId,
  title,
  artist: "The Alexandria Sessions",
  album: null,
  duration: 240,
  isVideo,
  positionSeconds,
  maxPositionReachedSeconds: positionSeconds,
  totalListenedSeconds: positionSeconds,
  qualifiedPlayCount: 1,
  lastPlayedAt: "2026-10-01T10:00:00Z",
  hasFinished,
  lastAccessedAt: "2026-10-01T10:00:00Z",
  createdAt: "2026-10-01T10:00:00Z",
  updatedAt: null,
});

// Shared history holds songs next to videos: one finished song, one stopped halfway.
const HISTORY = [
  historyEntry("audio-0", "Song 1", 240, true),
  historyEntry("audio-1", "Song 2", 120, false),
  historyEntry("video-0", "Film 1", 5400, true, true),
];

const streamingApi = async (route: Route) => {
  const url = new URL(route.request().url());

  if (url.pathname === "/api/streaming/files") {
    const isVideo = url.searchParams.get("isVideo") === "true";

    await route.fulfill({
      json: paginated(Array.from({ length: 3 }, (_, index) => media(isVideo, index))),
    });

    return true;
  }

  if (url.pathname === "/api/stream-history") {
    await route.fulfill({ json: paginated(HISTORY) });

    return true;
  }

  return false;
};

// In-app navigation keeps the query cache, which a full page load would reset.
const navigate = (page: Page, path: string) =>
  page.evaluate(
    (target) =>
      (
        document.getElementById("app") as HTMLElement & {
          __vue_app__: {
            config: { globalProperties: { $router: { push: (to: string) => Promise<unknown> } } };
          };
        }
      ).__vue_app__.config.globalProperties.$router.push(target),
    path,
  );

test("music cards ignore song history that the video pages cached", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await fixture(page, seed(), {}, undefined, streamingApi);

  await navigate(page, "/streaming/videos");
  const film = page.getByRole("button", { name: "Play Film 1", exact: true });

  await expect(film.first()).toContainText("Watched");
  await expect(film.filter({ hasNotText: "Watched" })).toHaveCount(0);

  await navigate(page, "/streaming/music");

  const finished = page.getByRole("button", { name: "Play Song 1", exact: true });
  const halfway = page.getByRole("button", { name: "Play Song 2", exact: true });

  // Music opens as a list; the resume bar only exists on grid tiles.
  for (const view of ["List view", "Grid view"]) {
    await page.getByRole("button", { name: view, exact: true }).click();

    await expect(finished).toContainText("The Alexandria Sessions");
    await expect(finished).not.toContainText("Watched");
    await expect(halfway).toContainText("The Alexandria Sessions");
    await expect(halfway.locator("span.bg-primary")).toHaveCount(0);
  }
});
