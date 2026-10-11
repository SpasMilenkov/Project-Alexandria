import { type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";

const directoryId = "10101010-1010-4010-8010-101010101010";

const favoritesFixture = async (page: Page, isStarred = false) => {
  const directory = {
    createdAt: "2026-10-01T12:00:00Z",
    id: directoryId,
    isStarred,
    name: "Projects",
    ownerUserDto: { email: "alex@example.test", id: "home-fixture", name: "Alex" },
    parentId: null,
    updatedAt: "2026-10-01T12:00:00Z",
  };
  const state = { failNext: false, writes: [] as boolean[] };
  const paginated = (items: unknown[]) => ({
    currentPage: 1,
    hasNext: false,
    hasPrevious: false,
    items,
    pageSize: 20,
    totalCount: items.length,
    totalPages: items.length ? 1 : 0,
  });

  await fixture(page, seed(), {}, undefined, async (route) => {
    const path = new URL(route.request().url()).pathname;

    if (path === `/api/directories/${directoryId}/favorite`) {
      const data = route.request().postDataJSON() as { isStarred: boolean };
      state.writes.push(data.isStarred);

      if (state.failNext) {
        state.failNext = false;
        await route.fulfill({ status: 503, json: { message: "Please retry" } });

        return true;
      }

      directory.isStarred = data.isStarred;
      await route.fulfill({ json: { directoryId, isStarred: directory.isStarred } });

      return true;
    }

    if (path === "/api/directories/root") {
      await route.fulfill({ json: paginated([directory]) });

      return true;
    }

    if (path === "/api/files/root") {
      await route.fulfill({ json: paginated([]) });

      return true;
    }

    return false;
  });

  await page.goto("/dashboard");
  await expect(page.locator(`[data-dir-id="${directoryId}"]`)).toBeVisible();

  return state;
};

for (const dark of [false, true]) {
  test(`context menu and drawer stay synchronized in ${dark ? "dark" : "light"} mode`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme: dark ? "dark" : "light" });
    const state = await favoritesFixture(page);
    const row = page.locator(`[data-dir-id="${directoryId}"]`);

    await row.click({ button: "right" });
    await page.getByRole("menuitem", { name: "Add to favorites", exact: true }).click();
    await expect(row.getByRole("img", { name: "Favorite folder" })).toBeVisible();

    await row.click({ button: "right" });
    await page.getByRole("menuitem", { name: "View details" }).click();
    const drawer = page.getByRole("dialog");

    await expect(drawer.getByText("Favorite folder", { exact: true })).toBeVisible();
    await drawer.getByRole("button", { name: "Remove from favorites", exact: true }).click();

    await expect(drawer).toBeVisible();
    await expect(
      drawer.getByRole("button", { name: "Add to favorites", exact: true }),
    ).toBeVisible();
    await expect(drawer.getByText("Favorite folder", { exact: true })).toHaveCount(0);
    expect(state.writes).toEqual([true, false]);

    await page.reload();
    await expect(row).toBeVisible();
    await expect(row.getByRole("img", { name: "Favorite folder" })).toHaveCount(0);
  });
}

test("a failed favorite change keeps the mobile drawer state and can be retried", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const state = await favoritesFixture(page, true);
  const row = page.locator(`[data-dir-id="${directoryId}"]`);

  await row.click({ button: "right" });
  await page.getByRole("menuitem", { name: "View details" }).click();
  const drawer = page.getByRole("dialog");
  state.failNext = true;

  await drawer.getByRole("button", { name: "Remove from favorites", exact: true }).click();
  await expect(page.getByText("Could not update folder favorites", { exact: true })).toBeVisible();
  await expect(drawer.getByText("Favorite folder", { exact: true })).toBeVisible();

  await drawer.getByRole("button", { name: "Remove from favorites", exact: true }).click();
  await expect(drawer.getByRole("button", { name: "Add to favorites", exact: true })).toBeVisible();
  expect(state.writes).toEqual([false, false]);
});
