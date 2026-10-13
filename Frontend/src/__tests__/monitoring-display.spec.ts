import { describe, expect, it } from "vitest";

import { ServiceType } from "@/enums";
import { MONITORED_SERVICES } from "@/utils/monitoring-display.utils";

describe("MONITORED_SERVICES", () => {
  it("covers every known service so none vanish from incident history", () => {
    const members = Object.values(ServiceType).filter(
      (entry): entry is ServiceType => typeof entry === "number",
    );

    expect([...MONITORED_SERVICES].sort()).toEqual([...members].sort());
  });

  it("includes the playlist worker", () => {
    expect(MONITORED_SERVICES).toContain(ServiceType.Playlist);
  });
});
