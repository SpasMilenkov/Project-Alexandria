import { expect, test } from "@playwright/test";

import { expectBoundedRequests, media, openMedia, section } from "./helpers/media-overview.js";

test("fills the dashboard with bounded lists, On repeat and direct destination links", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1920, height: 1100 });
  const { requests } = await openMedia(page);
  const music = section(page, "Fresh in your library");
  const videos = section(page, "Videos");
  const collections = section(page, "Made from your library");

  await expect(music.getByRole("button", { name: /^Play Song/u })).toHaveCount(12);
  await expect(videos.getByRole("button", { name: /^Play Film/u })).toHaveCount(8);
  await expect(collections.getByRole("link", { name: /^Artist collection/u })).toHaveCount(6);
  await expect(
    section(page, "Your playlists").getByRole("link", { name: /^My playlist/u }),
  ).toHaveCount(4);
  await expect(section(page, "Recently played").getByRole("listitem")).toHaveCount(5);
  await expect(music).toContainText("123 tracks");
  await expect(
    section(page, "On repeat").getByRole("button", { name: /^Play Favorite/u }),
  ).toHaveCount(6);
  await expect(section(page, "On repeat")).toContainText("12 plays");
  await expect(
    section(page, "Jump back in").getByRole("button", { name: /^Play Old favorite/u }),
  ).toHaveCount(6);
  expectBoundedRequests(requests);
  const widths = await page.getByRole("main").evaluate((element) => ({
    width: element.getBoundingClientRect().width,
    parentWidth: element.parentElement!.clientWidth,
    maxWidth: getComputedStyle(element).maxWidth,
  }));
  expect(widths).toMatchObject({ maxWidth: "none", width: widths.parentWidth });
  await expect(music.locator("img").first()).toHaveAttribute(
    "src",
    /\/versions\/playback-version\/thumbnail/u,
  );
  await expect(collections.getByRole("link", { name: /View all/u })).toHaveAttribute(
    "href",
    "/streaming/playlists?origin=auto",
  );
  await expect(
    section(page, "Your playlists").getByRole("link", { name: /View all/u }),
  ).toHaveAttribute("href", "/streaming/playlists?origin=manual");
  await expect(page.getByRole("link", { name: "Overview", exact: true }).first()).toHaveAttribute(
    "aria-current",
    "page",
  );
  await page.screenshot({ path: "/tmp/media-overview-desktop.png", fullPage: true });
  await section(page, "Jump back in").screenshot({ path: "/tmp/jump-back-in-desktop.png" });
});

test("the hero and this week summarize the library", async ({ page }) => {
  await page.setViewportSize({ width: 1920, height: 1100 });
  await openMedia(page);
  await expect(page.getByRole("searchbox", { name: "Search music" })).toBeVisible();

  await expect(page.getByRole("region", { name: "Now playing" })).toContainText(
    "Played recently 1",
  );
  await expect(section(page, "This week")).toContainText("1h");
  await expect(section(page, "This week")).toContainText("9 plays · active 1 of 7 days");
  await expect(
    section(page, "This week").getByRole("link", { name: "Your listening" }),
  ).toHaveAttribute("href", "/stats");
});

for (const viewport of [
  { width: 1920, height: 1100 },
  { width: 390, height: 900 },
]) {
  test(`long hero titles stay on one line at ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await openMedia(page, { longTitle: true });
    const heading = page.getByRole("region", { name: "Now playing" }).getByRole("heading");

    await expect(heading).toContainText("An extraordinarily long song title");
    await expect(heading.locator("[title]")).toHaveAttribute("title", /Extended Remix/u);

    const box = await heading.boundingBox();
    expect(box!.height).toBeLessThan(80);
    await page
      .getByRole("region", { name: "Now playing" })
      .screenshot({ path: `/tmp/media-hero-long-${viewport.width}.png` });
  });
}

for (const colorScheme of ["light", "dark"] as const) {
  test(`phone touch layout in ${colorScheme} mode`, async ({ browser }) => {
    const context = await browser.newContext({
      viewport: { width: 390, height: 1000 },
      isMobile: true,
      hasTouch: true,
      colorScheme,
      baseURL: test.info().project.use.baseURL,
    });

    try {
      const page = await context.newPage();
      await openMedia(page);
      await expect(section(page, "Recently played").getByRole("listitem")).toHaveCount(5);
      await expect(
        section(page, "Fresh in your library").getByRole("button", {
          name: "Play Song 1",
          exact: true,
        }),
      ).toBeEnabled();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
        true,
      );
      await page.screenshot({
        path: `/tmp/media-overview-phone-${colorScheme}.png`,
        fullPage: true,
      });
      await section(page, "Jump back in").screenshot({
        path: `/tmp/jump-back-in-phone-${colorScheme}.png`,
      });
      await section(page, "Recently played")
        .getByRole("link", { name: "History", exact: true })
        .tap();
      await expect(page).toHaveURL(/\/streaming\/history$/u);
    } finally {
      await context.close();
    }
  });
}

test("loads and retries one failed section independently", async ({ page }) => {
  const state = await openMedia(page, { holdVideo: true, failedVideo: true });
  const videos = section(page, "Videos");
  await expect(videos.getByRole("status")).toBeVisible();
  await expect(
    section(page, "Fresh in your library").getByRole("button", { name: /^Play Song/u }),
  ).toHaveCount(12);
  await expect(section(page, "Recently played").getByRole("listitem")).toHaveCount(5);
  state.release();
  await expect(videos.getByRole("alert")).toBeVisible({ timeout: 20_000 });
  state.recover();
  await videos.getByRole("button", { name: /Try again/u }).click();
  await expect(videos.getByRole("button", { name: /^Play Film/u })).toHaveCount(8);
  expect(
    state.requests.filter(
      (url) =>
        url.pathname === "/api/streaming/files" && url.searchParams.get("isVideo") === "false",
    ),
  ).toHaveLength(1);
});

test("desktop Overview groups all media pages and remembers expansion", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await openMedia(page);
  const navigation = page.getByRole("navigation", { name: "Media navigation", exact: true });

  await expect(navigation.getByRole("link", { name: "Overview", exact: true })).toBeVisible();
  await expect(navigation.getByRole("link", { name: "Music", exact: true })).toHaveCount(0);
  await navigation.locator('[data-slot="linkTrailing"]').click();

  for (const name of ["Music", "Videos", "Playlists", "History", "Jobs", "Your listening"]) {
    await expect(navigation.getByRole("link", { name, exact: true })).toBeVisible();
  }

  await page.reload();
  await expect(navigation.getByRole("link", { name: "History", exact: true })).toBeVisible();
  await navigation.getByRole("link", { name: "History", exact: true }).click();
  await expect(page).toHaveURL(/\/streaming\/history$/u);
  await navigation.getByRole("link", { name: "Overview", exact: true }).click();
  await expect(page).toHaveURL(/\/media$/u);
  await navigation.locator('[data-slot="linkTrailing"]').click();
  await expect(navigation.getByRole("link", { name: "Music", exact: true })).toHaveCount(0);
  await page.reload();
  await expect(navigation.getByRole("link", { name: "Music", exact: true })).toHaveCount(0);
});

test("keeps playing media while moving between Overview and History", async ({ page }) => {
  await openMedia(page);
  await page.evaluate(
    (file) => {
      const app = (
        document.getElementById("app") as HTMLElement & {
          __vue_app__: {
            config: {
              globalProperties: {
                $pinia: {
                  _s: Map<
                    string,
                    {
                      $patch: (state: Record<string, unknown>) => void;
                    }
                  >;
                };
              };
            };
          };
        }
      ).__vue_app__;
      app.config.globalProperties.$pinia._s.get("player")!.$patch({
        playerDismissed: true,
        nowPlaying: { file, instanceId: 75, origin: "queue", ended: false, restored: false },
      });
    },
    media(false, 0),
  );

  await expect
    .poll(() =>
      page.evaluate(() => {
        const app = (
          document.getElementById("app") as HTMLElement & {
            __vue_app__: {
              config: {
                globalProperties: {
                  $pinia: { _s: Map<string, { engineLoadError: string | null }> };
                };
              };
            };
          }
        ).__vue_app__;
        return app.config.globalProperties.$pinia._s.get("player")!.engineLoadError;
      }),
    )
    .not.toBeNull();

  const readPlayer = () => {
    const app = (
      document.getElementById("app") as HTMLElement & {
        __vue_app__: {
          config: {
            globalProperties: {
              $pinia: {
                _s: Map<
                  string,
                  {
                    nowPlaying: { instanceId: number; file: { fileId: string } };
                    isPlaying: boolean;
                  }
                >;
              };
            };
          };
        };
      }
    ).__vue_app__;
    const player = app.config.globalProperties.$pinia._s.get("player")!;

    return {
      instance: player.nowPlaying.instanceId,
      file: player.nowPlaying.file.fileId,
      playing: player.isPlaying,
    };
  };

  await page.evaluate(() => {
    const app = (
      document.getElementById("app") as HTMLElement & {
        __vue_app__: {
          config: {
            globalProperties: {
              $pinia: {
                _s: Map<
                  string,
                  {
                    $patch: (state: Record<string, unknown>) => void;
                  }
                >;
              };
            };
          };
        };
      }
    ).__vue_app__;
    app.config.globalProperties.$pinia._s
      .get("player")!
      .$patch({ engineLoadError: null, isPlaying: true });
  });

  await section(page, "Recently played")
    .getByRole("link", { name: "History", exact: true })
    .click();
  await expect(page).toHaveURL(/\/streaming\/history$/u);
  expect(await page.evaluate(readPlayer)).toEqual({ instance: 75, file: "audio-0", playing: true });
  await page.goBack();
  await expect(page).toHaveURL(/\/media$/u);
  expect(await page.evaluate(readPlayer)).toEqual({ instance: 75, file: "audio-0", playing: true });
});

test("an empty library welcomes with policy guidance and quiet sections", async ({ page }) => {
  await openMedia(page, { empty: true });

  for (const text of [
    "Your library starts here",
    "What you will find here",
    "Nothing on repeat yet",
    "No plays yet",
    "No playlists yet",
  ]) {
    await expect(page.getByText(text, { exact: true })).toBeVisible();
  }

  await expect(page.getByText(/Drop audio or video files in a directory with a/u)).toBeVisible();
  await expect(section(page, "This week")).toContainText("0m");

  await page.getByRole("button", { name: "What is a transpilation policy?" }).hover();
  await expect(page.getByText("What is a transpilation policy?", { exact: true })).toBeVisible();
  await expect(page.getByText(/add a Transcode rule under/u)).toBeVisible();
  await page.screenshot({ path: "/tmp/media-overview-empty.png", fullPage: true });
});

test("Jump back in retries independently and plays the returned file directly", async ({
  page,
}) => {
  const state = await openMedia(page, { failedJumpBackIn: true });
  const jumpBackIn = section(page, "Jump back in");
  await expect(jumpBackIn.getByRole("alert")).toBeVisible({ timeout: 20_000 });
  await expect(
    section(page, "On repeat").getByRole("button", { name: /^Play Favorite/u }),
  ).toHaveCount(6);
  state.recover();
  await jumpBackIn.getByRole("button", { name: /Try again/u }).click();
  await expect(jumpBackIn.getByRole("button", { name: /^Play Old favorite/u })).toHaveCount(6);
  await expect(jumpBackIn).toContainText("69 days ago");
  expect(state.requests.filter((url) => url.pathname === "/api/stats/top-songs")).toHaveLength(1);
  await jumpBackIn.getByRole("button", { name: "Play Old favorite 1", exact: true }).click();
  await expect
    .poll(() =>
      page.evaluate(() => {
        const app = (
          document.getElementById("app") as HTMLElement & {
            __vue_app__: {
              config: {
                globalProperties: {
                  $pinia: { _s: Map<string, { activeFile: { fileId: string } | null }> };
                };
              };
            };
          }
        ).__vue_app__;

        return app.config.globalProperties.$pinia._s.get("player")!.activeFile?.fileId;
      }),
    )
    .toBe("audio-20");
});
