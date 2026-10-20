import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { FileResult } from "@/api/file";

import FileItem from "@/components/dashboard/file-system/FileItem.vue";

vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQueryCache: () => ({ ensure: vi.fn(), refresh: vi.fn() }),
}));

const makeFile = (overrides?: Partial<FileResult> & { isEncrypted?: boolean }): FileResult => ({
  createdAt: "2026-01-01T00:00:00.000Z",
  currentVersion: {
    id: "v1",
    isDeleted: false,
    isEncrypted: overrides?.isEncrypted ?? false,
    mimeType: overrides?.mimeType ?? "text/plain",
    size: "1024",
    versionNumber: 1,
  },
  deletedAt: null,
  directoryId: null,
  fileId: "f1",
  fileName: "f1.txt",
  mimeType: overrides?.mimeType ?? "text/plain",
  owner: { email: "owner@test.com", id: "owner", name: "Owner" },
  tags: [],
  updatedAt: null,
});

describe("FileItem encryption indicator", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  const mountItem = (data: FileResult, viewMode: "grid" | "list") =>
    mount(FileItem, {
      global: { plugins: [createPinia()] },
      props: { data, isSelected: false, viewMode },
    });

  it("shows the lock chip on an encrypted grid tile", () => {
    const wrapper = mountItem(makeFile({ isEncrypted: true }), "grid");
    const indicator = wrapper.find("[data-encrypted-indicator]");
    expect(indicator.exists()).toBe(true);
    expect(indicator.text()).toContain("Encrypted");
  });

  it("omits the lock chip on a plain grid tile", () => {
    const wrapper = mountItem(makeFile(), "grid");
    expect(wrapper.find("[data-encrypted-indicator]").exists()).toBe(false);
  });

  it("shows the inline lock on an encrypted list row", () => {
    const wrapper = mountItem(makeFile({ isEncrypted: true }), "list");
    const indicator = wrapper.find("[data-encrypted-indicator]");
    expect(indicator.exists()).toBe(true);
    expect(indicator.text()).toContain("Encrypted");
  });

  it("omits the inline lock on a plain list row", () => {
    const wrapper = mountItem(makeFile(), "list");
    expect(wrapper.find("[data-encrypted-indicator]").exists()).toBe(false);
  });

  it("skips the thumbnail request for encrypted image tiles", () => {
    const encrypted = mountItem(makeFile({ isEncrypted: true, mimeType: "image/png" }), "grid");
    expect(encrypted.find("img").exists()).toBe(false);

    const plain = mountItem(makeFile({ mimeType: "image/png" }), "grid");
    expect(plain.find("img").exists()).toBe(true);
  });
});
