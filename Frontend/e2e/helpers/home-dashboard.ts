import { type Page, type Route, expect } from "@playwright/test";

interface Widget {
  instanceId: string;
  type: number;
  size: number;
  x: number;
  y: number;
  width: number;
  height: number;
  options: { timeZone: string; shortcutGroup: number };
}

interface HomeDocument {
  schemaVersion: number;
  useSeparateMobileLayout: boolean;
  desktop: { columns: 4 | 12; widgets: Widget[] };
  mobile: { columns: 4 | 12; widgets: Widget[] } | null;
}

export const seed = (): HomeDocument => ({
  schemaVersion: 1,
  useSeparateMobileLayout: false,
  desktop: {
    columns: 12,
    widgets: [
      {
        instanceId: "default-clock",
        type: 0,
        size: 1,
        x: 0,
        y: 0,
        width: 6,
        height: 4,
        options: { timeZone: "local", shortcutGroup: 0 },
      },
      {
        instanceId: "default-library",
        type: 1,
        size: 2,
        x: 0,
        y: 4,
        width: 12,
        height: 6,
        options: { timeZone: "local", shortcutGroup: 0 },
      },
    ],
  },
  mobile: null,
});

export const fixture = async (
  page: Page,
  initial: HomeDocument = seed(),
  appearance: Record<string, unknown> = {},
  timeZones?: (route: Route) => Promise<void>,
) => {
  const state = { saved: initial, writes: [] as HomeDocument[], failNextSave: false };

  await page.addInitScript(() => {
    Reflect.deleteProperty(Object.getPrototypeOf(navigator), "serviceWorker");
    localStorage.setItem(
      "auth",
      JSON.stringify({
        user: {
          success: true,
          user: { id: "home-fixture", name: "Alex", email: "alex@example.test" },
          userRoles: ["User"],
          onboardingStep: 3,
        },
      }),
    );
  });

  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    let body: unknown = {};

    if (path === "/api/settings/home") {
      if (route.request().method() === "PUT") {
        state.writes.push(route.request().postDataJSON() as HomeDocument);

        if (state.failNextSave) {
          state.failNextSave = false;

          await route.fulfill({ status: 503, json: { message: "Temporarily unavailable" } });

          return;
        }

        state.saved = route.request().postDataJSON() as HomeDocument;
      }

      body = state.saved;
    } else if (path === "/api/settings/home/time-zones") {
      if (timeZones) {
        await timeZones(route);

        return;
      }

      body = [
        "local",
        "UTC",
        "Europe/Bucharest",
        "Asia/Tokyo",
        "Asia/Kathmandu",
        "Pacific/Chatham",
      ];
    } else if (path === "/api/users/onboarding") {
      body = { onboardingStep: 3 };
    } else if (path === "/api/settings/appearance") {
      body = {
        accentColor: "orange",
        backgroundColor: "#f5f5f4",
        backgroundImageKey: null,
        backgroundImageUpdatedAt: null,
        backgroundImageOpacity: 1,
        gridIconSize: 64,
        listIconSize: 24,
        backgroundBlurEnabled: true,
        backgroundBlurAmount: 8,
        disableBlurOnMobile: false,
        transparencyEnabled: true,
        surfaceOpacity: 80,
        thumbnailsEnabled: true,
        fontFamily: "system",
        cornerRadius: 0.5,
        ...appearance,
      };
    } else if (path === "/api/settings/behavior") {
      body = {
        skipDeleteConfirmation: false,
        toastLevel: 0,
        allowAutoTagRegression: false,
        autoPlaylistMinTracks: 10,
      };
    }

    await route.fulfill({ json: body });
  });

  await page.goto("/home");

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  return state;
};
