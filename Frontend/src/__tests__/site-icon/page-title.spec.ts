import { describe, expect, it } from "vitest";
import { effectScope, nextTick, ref } from "vue";

import { pageTitleOverride, usePageTitle } from "@/composables/usePageTitle";

describe("usePageTitle", () => {
  it("lends the title while set and gives it back when the page goes away", async () => {
    const title = ref<string | null>("Trip plan");
    const scope = effectScope();

    scope.run(() => usePageTitle(title));
    expect(pageTitleOverride.value).toBe("Trip plan");

    title.value = "Trip plan v2";
    await nextTick();
    expect(pageTitleOverride.value).toBe("Trip plan v2");

    scope.stop();
    expect(pageTitleOverride.value).toBeNull();
  });

  it("falls back to the route title for an empty name", async () => {
    const title = ref<string | null>("Draft");
    const scope = effectScope();

    scope.run(() => usePageTitle(title));
    title.value = "   ";
    await nextTick();
    expect(pageTitleOverride.value).toBeNull();

    scope.stop();
  });

  it("keeps the newer page's title when the older one unmounts after it", () => {
    const older = effectScope();
    const newer = effectScope();

    older.run(() => usePageTitle("First note"));
    newer.run(() => usePageTitle("Second note"));
    older.stop();
    expect(pageTitleOverride.value).toBe("Second note");

    newer.stop();
    expect(pageTitleOverride.value).toBeNull();
  });
});
