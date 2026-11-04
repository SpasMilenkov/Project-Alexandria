import { type Locator, type Page, type Route, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { openWidgetTab, selectWidget } from "./helpers/home-grid-editor.js";

interface BrowserPlayer {
  engineLoadError: string | null;
  isPlaying: boolean;
  $patch: (state: Record<string, unknown>) => void;
  setIsPlaying: (playing: boolean) => void;
  setCurrentTime: (position: number) => void;
  registerEngine: (kind: string, controls: Record<string, (...args: number[]) => void>) => void;
}

const gallery = (playerStyle = 0) => {
  const value = seed();
  const sizes = [
    { x: 0, y: 0, width: 2, height: 2 },
    { x: 2, y: 0, width: 3, height: 6 },
    { x: 5, y: 0, width: 7, height: 2 },
    { x: 5, y: 2, width: 6, height: 6 },
  ];

  value.desktop.widgets = sizes.map((size, index) => ({
    ...value.desktop.widgets[0]!,
    ...size,
    instanceId: `player-${index}`,
    type: 6,
    options: { ...value.desktop.widgets[0]!.options, playerStyle },
  }));

  return value;
};

const playerState = () => {
  const app = (
    document.getElementById("app") as HTMLElement & {
      __vue_app__: { config: { globalProperties: { $pinia: { _s: Map<string, BrowserPlayer> } } } };
    }
  ).__vue_app__;

  const player = app.config.globalProperties.$pinia._s.get("player")!;

  return { engineLoadError: player.engineLoadError, isPlaying: player.isPlaying };
};

const loadMedia = async (page: Page) => {
  await page.evaluate(() => {
    const app = (
      document.getElementById("app") as HTMLElement & {
        __vue_app__: {
          config: { globalProperties: { $pinia: { _s: Map<string, BrowserPlayer> } } };
        };
      }
    ).__vue_app__;
    const player = app.config.globalProperties.$pinia._s.get("player")!;

    player.$patch({
      playbackOwnerId: "home-fixture",
      playerDismissed: true,
      nowPlaying: {
        instanceId: 1,
        origin: "queue",
        ended: false,
        restored: false,
        file: {
          fileId: "player-media",
          fileName: "midnight-city.mp3",
          title: "Midnight City",
          artist: "M83",
          album: "Hurry Up, We're Dreaming",
          mimeType: "audio/mpeg",
          isVideo: false,
          currentVersionId: "current-version",
          playbackVersionId: "playing-version",
          duration: 240,
          transpilationJobId: "job",
        },
      },
    });
  });

  await expect
    .poll(() => page.evaluate(playerState))
    .toMatchObject({
      engineLoadError: "Failed to resolve stream URL.",
    });

  await page.evaluate(() => {
    const app = (
      document.getElementById("app") as HTMLElement & {
        __vue_app__: {
          config: { globalProperties: { $pinia: { _s: Map<string, BrowserPlayer> } } };
        };
      }
    ).__vue_app__;
    const player = app.config.globalProperties.$pinia._s.get("player")!;

    player.registerEngine("audio", {
      play: () => player.setIsPlaying(true),
      pause: () => player.setIsPlaying(false),
      seek: (position) => player.setCurrentTime(position),
      setVolume: () => {},
      selectVariant: () => {},
      setPlaybackRate: () => {},
    });
    player.$patch({
      engineReady: true,
      engineLoadError: null,
      engineBuffering: false,
      duration: 240,
      currentTime: 60,
      isPlaying: false,
    });
  });
};

const playerFixture = async (page: Page, value = gallery(), thumbnails = true) => {
  const artwork = `<svg xmlns="http://www.w3.org/2000/svg" width="300" height="300"><defs><linearGradient id="a" x2="1" y2="1"><stop stop-color="#dd7c8f"/><stop offset="1" stop-color="#4256a3"/></linearGradient></defs><rect width="300" height="300" fill="url(#a)"/><circle cx="170" cy="120" r="70" fill="#e9b592"/><path d="M0 260L90 130L180 300H0" fill="#373661"/></svg>`;
  const apiHandler = async (route: Route) => {
    const path = new URL(route.request().url()).pathname;

    if (path.endsWith("/thumbnail")) {
      await route.fulfill({ contentType: "image/svg+xml", body: artwork });

      return true;
    }

    if (path.startsWith("/api/streaming/manifest/")) {
      await route.fulfill({ json: "" });

      return true;
    }

    return false;
  };
  const state = await fixture(
    page,
    value,
    { thumbnailsEnabled: thumbnails },
    undefined,
    apiHandler,
  );

  return state;
};

const fits = async (card: Locator) => {
  await expect
    .poll(() =>
      card.evaluate((element) => {
        const bounds = element.getBoundingClientRect();
        const content = element.querySelectorAll(".player-details, .player-empty, .player-artwork");

        return (
          [...content].every((target) => {
            if (!target.getClientRects().length) return true;

            const box = target.getBoundingClientRect();

            return (
              box.left >= bounds.left &&
              box.right <= bounds.right &&
              box.top >= bounds.top &&
              box.bottom <= bounds.bottom
            );
          }) &&
          element.scrollHeight <= element.clientHeight + 1 &&
          element.scrollWidth <= element.clientWidth + 1
        );
      }),
    )
    .toBe(true);
};

for (const dark of [false, true]) {
  for (const thumbnails of [false, true]) {
    for (const style of [0, 1]) {
      test(`Player style ${style} fits ${dark ? "dark" : "light"} mode with thumbnails ${thumbnails}`, async ({
        page,
      }) => {
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.emulateMedia({ colorScheme: dark ? "dark" : "light" });
        await playerFixture(page, gallery(style), thumbnails);
        await loadMedia(page);

        await expect(page.locator("[data-home-player]")).toHaveCount(4);
        await expect(page.locator("[data-home-player] .record-disc")).toHaveCount(
          style === 0 ? 4 : 0,
        );

        for (const card of await page.locator("[data-widget-id]").all()) await fits(card);

        const tall = page.locator('[data-widget-id="player-1"]');
        const wide = page.locator('[data-widget-id="player-2"]');
        await expect(tall.locator(".player-artwork")).toBeVisible();
        await expect(wide.locator(".player-face")).toHaveCSS("flex-direction", "row");

        if (thumbnails) {
          await expect(tall.locator("img")).toHaveAttribute("src", /playing-version/u);
          await expect(tall.locator("img")).toBeVisible();
          await expect(tall.locator(".player-tint")).toHaveCSS(
            "background-image",
            /playing-version/u,
          );
        } else {
          await expect(page.locator("[data-home-player] img")).toHaveCount(0);
        }

        await page.screenshot({
          path: `/tmp/alexandria-player-${style}-${dark ? "dark" : "light"}-${thumbnails}.png`,
          fullPage: true,
        });
      });
    }
  }
}

test("mobile compact cards share transport and keyboard seeking", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const value = gallery();
  value.useSeparateMobileLayout = true;
  value.mobile = {
    columns: 4,
    widgets: value.desktop.widgets.slice(0, 2).map((widget, index) => ({
      ...widget,
      x: index * 2,
      y: 0,
      width: 2,
      height: 2,
    })),
  };
  await playerFixture(page, value);
  await loadMedia(page);
  const cards = page.locator("[data-widget-id]");

  for (const card of await cards.all()) await fits(card);

  await cards.first().getByRole("button", { name: "Play", exact: true }).click();
  await expect(cards.last().getByRole("button", { name: "Pause", exact: true })).toBeVisible();
  await expect(cards.first().locator(".record-disc")).toHaveCSS("animation-play-state", "running");
  const seek = cards.first().getByRole("slider", { name: "Seek playback" });
  await seek.focus();
  await seek.press("ArrowRight");
  await expect(cards.last().getByRole("slider")).toHaveAttribute("aria-valuetext", "1:01 of 4:00");
  await cards.first().getByRole("button", { name: "Pause", exact: true }).click();
  await expect(cards.last().locator(".record-disc")).toHaveCSS("animation-play-state", "paused");
  await page.screenshot({ path: "/tmp/alexandria-player-mobile.png", fullPage: true });
});

test("vinyl fits with titles enabled and honors reduced motion during playback", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.emulateMedia({ reducedMotion: "reduce", colorScheme: "dark" });
  const value = gallery();
  value.showWidgetTitles = true;
  await playerFixture(page, value);
  await loadMedia(page);

  for (const card of await page.locator("[data-widget-id]").all()) await fits(card);

  const card = page.locator('[data-widget-id="player-0"]');
  await card.getByRole("button", { name: "Play", exact: true }).click();
  await expect(card.locator(".record-disc")).toHaveCSS("animation-name", "none");
  await expect(card.locator(".record-arm")).toHaveCSS("transition-duration", "0s");
  await page.screenshot({ path: "/tmp/alexandria-player-vinyl-titles.png", fullPage: true });
});

test("editing and removal preserve the shared engine and Done persists Player", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const state = await playerFixture(page);
  await loadMedia(page);
  const card = page.locator('[data-widget-id="player-0"]');
  await card.getByRole("button", { name: "Play", exact: true }).click();
  const engineCount = await page.locator("video, audio").count();

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, card);
  await expect(card.getByRole("button", { name: "Pause", exact: true })).toBeDisabled();
  await expect(page.getByRole("tab", { name: "Style", exact: true })).toBeVisible();
  await card.focus();
  await card.press("Escape");
  await page
    .getByRole("dialog", { name: "Leave customization?" })
    .getByRole("button", { name: "Keep editing", exact: true })
    .click();
  await expect(page.getByRole("dialog", { name: "Leave customization?" })).toHaveCount(0);
  await card.focus();
  await card.press("Delete");
  await expect(card).toHaveCount(0);
  await expect(page.locator("video, audio")).toHaveCount(engineCount);
  await expect.poll(() => page.evaluate(playerState)).toMatchObject({ isPlaying: true });
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets).toHaveLength(3);
  expect(state.saved.desktop.widgets.every((widget) => widget.type === 6)).toBe(true);

  await page.reload();
  await expect(page.locator("[data-widget-id]")).toHaveCount(3);
});

test("Player can be added from the catalogue and saved on Done", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const state = await playerFixture(page, seed());
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Add widget", exact: true }).click();
  await page.getByRole("button", { name: "Add Player", exact: true }).click();
  const player = page.getByRole("region", { name: "Player", exact: true });
  await expect(player.getByText("Nothing playing", { exact: true })).toBeVisible();
  await selectWidget(page, player);
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Traditional", exact: true }).click();
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets).toHaveLength(3);
  expect(state.saved.desktop.widgets.map((widget) => widget.type)).toEqual(
    expect.arrayContaining([0, 1, 6]),
  );
  expect(state.saved.desktop.widgets.find((widget) => widget.type === 6)!.options.playerStyle).toBe(
    1,
  );

  await page.reload();
  await expect(player.getByText("Nothing playing", { exact: true })).toBeVisible();
});

test("empty, loading, unknown-duration and error states stay usable", async ({ page }) => {
  await playerFixture(page);
  const card = page.locator('[data-widget-id="player-0"]');
  await expect(card.getByText("Nothing playing", { exact: true })).toBeVisible();
  await expect(card.getByRole("link", { name: "Music", exact: true })).toHaveAttribute(
    "href",
    "/streaming/music",
  );
  await fits(card);
  await loadMedia(page);

  await page.evaluate(() => {
    const app = (
      document.getElementById("app") as HTMLElement & {
        __vue_app__: {
          config: { globalProperties: { $pinia: { _s: Map<string, BrowserPlayer> } } };
        };
      }
    ).__vue_app__;
    app.config.globalProperties.$pinia._s
      .get("player")!
      .$patch({ duration: 0, engineReady: false });
  });
  await expect(card.getByRole("slider")).toBeDisabled();
  await expect(card.getByRole("button", { name: "Play", exact: true })).toBeDisabled();
  await fits(card);

  await page.evaluate(() => {
    const app = (
      document.getElementById("app") as HTMLElement & {
        __vue_app__: {
          config: { globalProperties: { $pinia: { _s: Map<string, BrowserPlayer> } } };
        };
      }
    ).__vue_app__;
    app.config.globalProperties.$pinia._s
      .get("player")!
      .$patch({ engineLoadError: "Playback unavailable" });
  });
  await expect(card.getByRole("alert")).toHaveText("Playback unavailable");
  await fits(card);
});
