import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { FileResult } from "@/api/file";

import FileItem from "@/components/dashboard/file-system/FileItem.vue";

const ensure = vi.fn((options: unknown) => options);
const refresh = vi.fn();
const getQueryData = vi.fn();

vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQueryCache: () => ({ ensure, getQueryData, refresh }),
}));

const PPTX = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

const makeFile = (
  options: { isEncrypted?: boolean; isPasswordProtected?: boolean } = {},
): FileResult => ({
  createdAt: "2026-01-01T00:00:00.000Z",
  currentVersion: {
    id: "v1",
    isDeleted: false,
    isEncrypted: options.isEncrypted ?? false,
    isPasswordProtected: options.isPasswordProtected ?? false,
    mimeType: PPTX,
    size: "1024",
    versionNumber: 1,
  },
  deletedAt: null,
  directoryId: null,
  fileId: "f1",
  fileName: "deck.pptx",
  mimeType: PPTX,
  owner: { email: "owner@test.com", id: "owner", name: "Owner" },
  tags: [],
  updatedAt: null,
});

const mountItem = (data: FileResult, viewMode: "grid" | "list") =>
  mount(FileItem, {
    global: { plugins: [createPinia()] },
    props: { data, isSelected: false, viewMode },
  });

describe("FileItem password-protected indicator", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.useFakeTimers();
    ensure.mockClear();
    refresh.mockClear();
    getQueryData.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const warmedKeys = async (data: FileResult): Promise<unknown[]> => {
    const wrapper = mountItem(data, "list");

    await wrapper.find("button").trigger("mouseenter");
    vi.advanceTimersByTime(150);

    return ensure.mock.calls.map(([options]) => (options as { key: unknown[] }).key);
  };

  it("shows the locked-file chip on a protected grid tile", () => {
    const indicator = mountItem(makeFile({ isPasswordProtected: true }), "grid").find(
      "[data-password-protected-indicator]",
    );

    expect(indicator.exists()).toBe(true);
    expect(indicator.text()).toContain("Password protected");
  });

  it("shows the inline locked-file icon on a protected list row", () => {
    const indicator = mountItem(makeFile({ isPasswordProtected: true }), "list").find(
      "[data-password-protected-indicator]",
    );

    expect(indicator.exists()).toBe(true);
    expect(indicator.text()).toContain("Password protected");
  });

  it("omits the chip on a plain document", () => {
    const wrapper = mountItem(makeFile(), "grid");

    expect(wrapper.find("[data-password-protected-indicator]").exists()).toBe(false);
  });

  it("lets the encryption chip win when both apply", () => {
    const wrapper = mountItem(makeFile({ isEncrypted: true, isPasswordProtected: true }), "grid");

    expect(wrapper.find("[data-encrypted-indicator]").exists()).toBe(true);
    expect(wrapper.find("[data-password-protected-indicator]").exists()).toBe(false);
  });

  it("skips the thumbnail request for a protected tile", () => {
    const locked = mountItem(makeFile({ isPasswordProtected: true }), "grid");
    const plain = mountItem(makeFile(), "grid");

    expect(locked.find("img").exists()).toBe(false);
    expect(plain.find("img").exists()).toBe(true);
  });

  it("does not warm the preview query when hovering a protected file", async () => {
    const previewKey = ["files", "preview-by-version", "f1", "v1"];

    expect(await warmedKeys(makeFile())).toContainEqual(previewKey);

    ensure.mockClear();

    expect(await warmedKeys(makeFile({ isPasswordProtected: true }))).not.toContainEqual(
      previewKey,
    );
  });
});
