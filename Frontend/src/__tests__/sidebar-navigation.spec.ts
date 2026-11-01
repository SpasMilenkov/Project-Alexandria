import { describe, expect, it } from "vitest";

import appRouter from "@/router";
import { buildSidebarSections, isSidebarItemActive } from "@/utils/sidebar-navigation";

const allItems = () =>
  buildSidebarSections({ isAdmin: true, streamingEnabled: true }).flatMap(
    (section) => section.items,
  );

const activeLabels = (routeName: string) =>
  buildSidebarSections({ isAdmin: true, streamingEnabled: true }).flatMap((section) =>
    section.items
      .filter((item) => isSidebarItemActive(item, routeName))
      .map((item) => `${section.id}:${item.label}`),
  );

describe("sidebar navigation", () => {
  it("shows only the library to a member without streaming", () => {
    const sections = buildSidebarSections({ isAdmin: false, streamingEnabled: false });

    expect(sections.map((section) => section.id)).toEqual(["library"]);
  });

  it("orders library, media and admin for an admin with streaming", () => {
    const sections = buildSidebarSections({ isAdmin: true, streamingEnabled: true });

    expect(sections.map((section) => section.id)).toEqual(["library", "media", "admin"]);
    expect(sections.map((section) => section.collapsible)).toEqual([false, true, true]);
  });

  it("points every item at a registered route and names only registered routes", () => {
    const routeNames = new Set(appRouter.getRoutes().map((route) => route.name));

    for (const item of allItems()) {
      expect(appRouter.resolve(item.to).matched.length, item.to).toBeGreaterThan(0);

      for (const name of item.activeRoutes) expect(routeNames.has(name), name).toBe(true);
    }
  });

  it("highlights exactly one item for every route it covers", () => {
    const covered = allItems().flatMap((item) => item.activeRoutes);

    for (const name of covered) expect(activeLabels(name), name).toHaveLength(1);
  });

  it("keeps Files and Integrations active on their nested pages", () => {
    expect(activeLabels("dashboard")).toEqual(["library:Files"]);
    expect(activeLabels("lyrics-dashboard")).toEqual(["admin:Integrations"]);
    expect(activeLabels("wrapped")).toEqual(["media:Listening stats"]);
    expect(activeLabels("playlist-details")).toEqual(["media:Playlists"]);
  });

  it("highlights nothing for pages outside the sidebar", () => {
    expect(activeLabels("settings")).toEqual([]);
    expect(activeLabels("account")).toEqual([]);
    expect(isSidebarItemActive(allItems()[0], undefined)).toBe(false);
  });
});
