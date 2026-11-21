import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { FileResult } from "@/api/file";

import FileItem from "@/components/dashboard/file-system/FileItem.vue";

vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQueryCache: () => ({ ensure: vi.fn(), refresh: vi.fn() }),
}));

const file: FileResult = {
  createdAt: "2026-01-01T00:00:00.000Z",
  currentVersion: {
    id: "v1",
    isDeleted: false,
    isEncrypted: false,
    mimeType: "application/pdf",
    size: "1024",
    versionNumber: 1,
  },
  deletedAt: null,
  directoryId: null,
  fileId: "f1",
  fileName: "scan-0042.pdf",
  matchedInContent: true,
  mimeType: "application/pdf",
  owner: { email: "owner@test.com", id: "owner", name: "Owner" },
  tags: [],
  updatedAt: null,
};

describe("FileItem list row subtitle", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  const mountRow = (slots?: { subtitle: string }) =>
    mount(FileItem, {
      global: { plugins: [createPinia()] },
      props: { data: file, isSelected: false, viewMode: "list" },
      slots,
    });

  it("shows the subtitle under the name in a taller row", () => {
    const row = mountRow({ subtitle: "<span data-subtitle>…renamed to ML-KEM</span>" });

    expect(row.find("[data-subtitle]").text()).toBe("…renamed to ML-KEM");
    expect(row.find("button").classes()).toContain("h-14");
  });

  it("keeps the usual row without one, whatever the search flag says", () => {
    const row = mountRow();

    expect(row.find("button").classes()).toContain("h-11");
    expect(row.text()).not.toContain("In text");
  });
});
