import { describe, expect, it, vi } from "vitest";
import { createMemoryHistory, createRouter } from "vue-router";

import { OnboardingStep } from "@/enums/OnboardingStep";
import appRouter from "@/router";
import { getHomeLandingDestination, getSignInDestination } from "@/utils/home-landing";

const router = createRouter({
  history: createMemoryHistory(),
  routes: appRouter.options.routes,
});

describe("Home landing", () => {
  it.each([
    "/home",
    "/dashboard",
    "/dashboard/folder-id?sortBy=name#files",
    "/media",
    "/streaming/playlists/playlist-id",
    "/stats/wrapped/2026?month=2026-09#top-songs",
    "/stats/timeline?source=bookmark#calendar",
    "/share/share-token",
    "/status",
  ])("preserves the explicit destination %s", (destination) => {
    expect(getHomeLandingDestination(router, destination)).toBe(destination);
    expect(getSignInDestination(router, OnboardingStep.Done, destination)).toBe(destination);
  });

  it.each([
    undefined,
    null,
    "",
    ["/media", "/dashboard"],
    "https://example.test/media",
    "//example.test/media",
    "/\\example.test/media",
    "media",
    "/",
    "/auth?redirect=/auth",
    "/onboarding/tour",
    "/missing-page",
  ])("uses Home for an unusable return URL: %j", (destination) => {
    const warning = vi.spyOn(console, "warn").mockImplementation(() => {});

    try {
      expect(getHomeLandingDestination(router, destination)).toBe("/home");
    } finally {
      warning.mockRestore();
    }
  });

  it.each([
    [OnboardingStep.SetPassword, "/onboarding/set-password"],
    [OnboardingStep.CompleteProfile, "/onboarding/setup-profile"],
    [OnboardingStep.Tour, "/onboarding/tour"],
  ])("retains the bookmark while completing onboarding step %s", (step, path) => {
    const destination = "/dashboard/folder-id?sortBy=name#files";

    expect(getSignInDestination(router, step as OnboardingStep, destination)).toEqual({
      path,
      query: { redirect: destination },
    });
  });

  it("defaults missing return URLs to Home after onboarding", () => {
    expect(getSignInDestination(router, OnboardingStep.Tour, undefined)).toEqual({
      path: "/onboarding/tour",
      query: { redirect: "/home" },
    });
    expect(getSignInDestination(router, OnboardingStep.Done, undefined)).toBe("/home");
  });
});
