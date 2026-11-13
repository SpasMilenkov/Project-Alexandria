import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent } from "vue";

import { useSettingsSearch } from "@/composables/useSettingsSearch";

// The composable registers lifecycle hooks, so it runs inside a component
const searchFor = (query: string) => {
  const captured: ReturnType<typeof useSettingsSearch>[] = [];

  mount(
    defineComponent({
      setup() {
        captured.push(useSettingsSearch());
        return () => null;
      },
    }),
  );

  const [search] = captured;
  search.searchQuery.value = query;

  return search.searchResults.value.map((entry) => entry.section);
};

describe("useSettingsSearch", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it("finds auto-tagging settings while auto-tagging is on", () => {
    vi.stubEnv("VITE_AUTOTAGGING_ENABLED", "true");

    expect(searchFor("confidence")).toContain("auto-tagging");
  });

  it("leaves auto-tagging settings out while auto-tagging is off", () => {
    vi.stubEnv("VITE_AUTOTAGGING_ENABLED", "false");

    expect(searchFor("confidence")).not.toContain("auto-tagging");
    expect(searchFor("auto")).not.toContain("auto-tagging");
  });
});
