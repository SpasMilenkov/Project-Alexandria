import { describe, expect, it } from "vitest";

import { ServiceType } from "@/enums";
import {
  FALLBACK_ROUTE,
  parseDeepLinkQuery,
  SERVICE_DASHBOARD_ROUTES,
} from "@/utils/serviceDashboardRouting";

describe("SERVICE_DASHBOARD_ROUTES", () => {
  it("deep-links the playlist worker to its dashboard", () => {
    expect(SERVICE_DASHBOARD_ROUTES[ServiceType.Playlist]).toBe(
      "/dashboard/admin/integrations/playlist",
    );
  });

  it("keeps every mapped route off the fallback", () => {
    for (const route of Object.values(SERVICE_DASHBOARD_ROUTES)) {
      expect(route).not.toBe(FALLBACK_ROUTE);
    }
  });
});

describe("parseDeepLinkQuery service parsing", () => {
  it("accepts the playlist service ordinal", () => {
    expect(parseDeepLinkQuery({ service: "6" }).service).toBe(ServiceType.Playlist);
  });

  it("accepts every known service ordinal", () => {
    for (const value of Object.values(ServiceType).filter(
      (entry): entry is ServiceType => typeof entry === "number",
    )) {
      expect(parseDeepLinkQuery({ service: String(value) }).service).toBe(value);
    }
  });

  it("rejects out-of-range and garbage ordinals", () => {
    expect(parseDeepLinkQuery({ service: "7" }).service).toBeUndefined();
    expect(parseDeepLinkQuery({ service: "-1" }).service).toBeUndefined();
    expect(parseDeepLinkQuery({ service: "playlist" }).service).toBeUndefined();
    expect(parseDeepLinkQuery({}).service).toBeUndefined();
  });
});
