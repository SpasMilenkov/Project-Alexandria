import { beforeEach, describe, expect, it } from "vitest";
import { createPinia, setActivePinia } from "pinia";

import { useActivityStore } from "@/stores/activity";

describe("useActivityStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("starts with default values", () => {
    const store = useActivityStore();
    expect(store.activity).toHaveLength(0);
    expect(store.page).toBe(1);
    expect(store.pageSize).toBe(10);
    expect(store.totalCount).toBe(0);
    expect(store.isLoading).toBe(false);
    expect(store.error).toBeNull();
  });

  it("getActivity returns the activity array", () => {
    const store = useActivityStore();
    expect(store.getActivity).toBe(store.activity);
  });

  it("getPage returns the current page", () => {
    const store = useActivityStore();
    store.page = 3;
    expect(store.getPage).toBe(3);
  });

  it("getPageSize returns the page size", () => {
    const store = useActivityStore();
    expect(store.getPageSize).toBe(10);
  });

  it("getTotalCount returns the total count", () => {
    const store = useActivityStore();
    store.totalCount = 42;
    expect(store.getTotalCount).toBe(42);
  });
});
