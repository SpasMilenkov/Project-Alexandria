import type { Router } from "vue-router";

import { type Page, expect, test } from "@playwright/test";

const account = (onboardingStep: number) => ({
  success: true,
  user: { id: "landing-user", name: "Alex", email: "alex@example.test" },
  userRoles: ["User"],
  onboardingStep,
});

const emptyPage = { items: [], totalCount: 0, currentPage: 1, pageSize: 10, totalPages: 0 };

const prepareLanding = async (
  page: Page,
  options: { signedIn?: boolean; onboardingStep?: number } = {},
) => {
  let step = options.onboardingStep ?? 3;

  await page.addInitScript(
    ({ signedIn, user }) => {
      Reflect.deleteProperty(Object.getPrototypeOf(navigator), "serviceWorker");

      if (sessionStorage.getItem("landing-fixture")) return;

      sessionStorage.setItem("landing-fixture", "true");
      localStorage.clear();

      if (signedIn) localStorage.setItem("auth", JSON.stringify({ user }));
    },
    { signedIn: options.signedIn ?? false, user: account(step) },
  );

  await page.route("**/api/**", async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;

    // Vite serves source modules from /src/api/*; only mock the API itself.
    if (path !== "/api" && !path.startsWith("/api/")) {
      await route.continue();

      return;
    }

    let body: unknown = {};

    if (path === "/api/auth/session") {
      body = account(step);
    } else if (path === "/api/auth/login") {
      body = account(step);
    } else if (path === "/api/users/onboarding") {
      body = { onboardingStep: step };
    } else if (path === "/api/auth/change-initial-password") {
      step = 1;
    } else if (path === "/api/users/setup-profile") {
      step = 2;
    } else if (path === "/api/users/finish-tour") {
      step = 3;
    } else if (path === "/api/home/widgets") {
      body = {
        initialized: false,
        hasMobileLayout: false,
        preferences: { useSeparateMobileLayout: false, showWidgetTitles: false },
        widgets: [],
      };
    } else if (path === "/api/settings/home/time-zones") {
      body = ["local", "UTC", "Europe/Bucharest"];
    } else if (path === "/api/stats/top-songs" || path === "/api/stats/jump-back-in") {
      body = { items: [], inactiveDays: 30 };
    } else if (path === "/api/stats/wrapped") {
      body = {
        from: "2026-09-01T00:00:00Z",
        to: "2026-10-01T00:00:00Z",
        generatedAt: "2026-10-10T12:00:00Z",
        schemaVersion: 3,
        deck: { cards: [] },
        summary: { seconds: 0, sessions: 0, qualifiedPlayCount: 0, tracks: 0, artists: 0 },
      };
    } else if (path === "/api/directories/landing-folder") {
      body = { id: "landing-folder", name: "Bookmarked folder", parentId: null };
    } else if (
      path.endsWith("/search") ||
      ["/api/streaming/files", "/api/playlists", "/api/stream-history"].includes(path)
    ) {
      body = emptyPage;
    }

    await route.fulfill({ json: body });
  });
};

const signIn = async (page: Page) => {
  await page.getByLabel("Email", { exact: true }).fill("alex@example.test");
  await page.getByLabel("Password", { exact: true }).fill("Alexandria123!");
  await page.getByRole("button", { name: "Continue", exact: true }).click();
};

const finishOnboarding = async (page: Page) => {
  await expect(page).toHaveURL(/\/onboarding\/set-password/u);
  await page.getByLabel("Temporary password", { exact: true }).fill("Temporary123!");
  await page.getByLabel("New password", { exact: true }).fill("Alexandria123!");
  await page.getByLabel("Confirm new password", { exact: true }).fill("Alexandria123!");
  await page.getByRole("button", { name: "Set password & continue", exact: true }).click();

  await expect(page).toHaveURL(/\/onboarding\/setup-profile/u);
  await page.getByRole("button", { name: "Continue", exact: true }).click();

  await expect(page).toHaveURL(/\/onboarding\/tour/u);
  await page
    .getByRole("button", { name: /^Go to /u })
    .last()
    .click();
  await page.getByRole("button", { name: "Get started", exact: true }).click();
};

test("the public welcome action leads through sign-in to Home", async ({ page }) => {
  await prepareLanding(page);
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Welcome to Alexandria" })).toBeVisible();

  const enter = page.getByRole("link", { name: "Enter your library" });
  await expect(enter).toHaveAttribute("href", "/home");
  await enter.focus();
  await page.keyboard.press("Enter");

  await expect(page).toHaveURL(/\/auth\?redirect=\/home$/u);
  await signIn(page);

  await expect(page).toHaveURL(/\/home$/u);
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
});

test("plain sign-in defaults to Home", async ({ page }) => {
  await prepareLanding(page);
  await page.goto("/auth");
  await signIn(page);

  await expect(page).toHaveURL(/\/home$/u);
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
});

test("signed-in entry routes use Home and retain explicit destinations", async ({ page }) => {
  await prepareLanding(page, { signedIn: true });

  for (const entry of ["/", "/auth", "/dashboard/admin"]) {
    await page.goto(entry);
    await expect(page).toHaveURL(/\/home$/u);
  }

  await page.goto("/auth?redirect=%2Fmedia");
  await expect(page).toHaveURL(/\/media$/u);
  await expect(page.getByRole("heading", { name: "Media", exact: true })).toBeVisible();
});

for (const destination of [
  "/dashboard",
  "/dashboard/landing-folder?source=bookmark#files",
  "/media",
  "/stats/wrapped/2026?month=2026-09#top-songs",
]) {
  test(`sign-in preserves ${destination}`, async ({ page }) => {
    await prepareLanding(page);
    await page.goto(destination);
    await expect(page).toHaveURL(/\/auth\?redirect=/u);
    expect(new URL(page.url()).searchParams.get("redirect")).toBe(destination);

    await signIn(page);

    await expect
      .poll(() => {
        const url = new URL(page.url());

        return `${url.pathname}${url.search}${url.hash}`;
      })
      .toBe(destination);
  });
}

for (const destination of [undefined, "/media?source=onboarding#library"]) {
  test(`onboarding finishes at ${destination ?? "Home"}`, async ({ page }) => {
    await prepareLanding(page, { onboardingStep: 0 });
    const query = destination ? `?redirect=${encodeURIComponent(destination)}` : "";
    await page.goto(`/auth${query}`);
    await signIn(page);
    await finishOnboarding(page);

    await expect.poll(() => new URL(page.url()).pathname).toBe(destination ? "/media" : "/home");

    if (destination) {
      expect(new URL(page.url()).search).toBe("?source=onboarding");
      expect(new URL(page.url()).hash).toBe("#library");
    }

    await page.goto("/auth");
    await expect(page).toHaveURL(/\/home$/u);
  });
}

test("completed onboarding bookmarks honor cached completion and their return destination", async ({
  page,
}) => {
  let onboardingRequests = 0;

  page.on("request", (request) => {
    if (new URL(request.url()).pathname === "/api/users/onboarding") onboardingRequests++;
  });

  await prepareLanding(page, { signedIn: true });
  await page.goto("/onboarding/tour");
  await expect(page).toHaveURL(/\/home$/u);

  await page.evaluate(async () => {
    const root = document.querySelector("#app") as HTMLElement & {
      __vue_app__: { config: { globalProperties: { $router: Router } } };
    };

    await root.__vue_app__.config.globalProperties.$router.push(
      "/onboarding/setup-profile?redirect=%2Fmedia",
    );
  });

  await expect(page).toHaveURL(/\/media$/u);
  expect(onboardingRequests).toBe(1);
});

test("Home and Files remain separate keyboard destinations", async ({ page }) => {
  await prepareLanding(page, { signedIn: true });
  await page.goto("/home");

  const explorer = page.getByRole("link", { name: "Files", exact: true }).first();
  await explorer.focus();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/dashboard$/u);

  const home = page.getByRole("link", { name: "Home", exact: true }).first();
  await home.focus();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/home$/u);
  await expect(home).toHaveAttribute("aria-current", "page");
});

test.describe("phone landing", () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  for (const colorScheme of ["light", "dark"] as const) {
    test(`Home, Media and Files navigation in ${colorScheme} mode`, async ({ page }) => {
      await page.emulateMedia({ colorScheme });
      await prepareLanding(page, { signedIn: true });
      await page.goto("/");
      await expect(page).toHaveURL(/\/home$/u);
      await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

      await page.getByRole("button", { name: "Open sidebar", exact: true }).click();
      await page.getByRole("button", { name: "Overview", exact: true }).click();
      await expect(page).toHaveURL(/\/media$/u);
      await expect(page.getByRole("heading", { name: "Media", exact: true })).toBeVisible();

      await page.getByRole("button", { name: "Open sidebar", exact: true }).click();
      await page.getByRole("button", { name: "Files", exact: true }).click();
      await expect(page).toHaveURL(/\/dashboard$/u);

      await page.getByRole("button", { name: "Open sidebar", exact: true }).click();
      await page.getByRole("button", { name: "Home", exact: true }).click();
      await expect(page).toHaveURL(/\/home$/u);

      await page.screenshot({ path: `/tmp/home-landing-${colorScheme}-phone.png`, fullPage: true });
    });
  }
});
