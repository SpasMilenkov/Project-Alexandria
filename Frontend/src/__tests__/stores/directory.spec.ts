import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";

import { useDirectoryStore } from "@/stores/directory";

const mockSearchDirectory = vi.fn();
vi.mock("@/api/directory", () => ({
  directoryApi: {
    searchDirectory: (...args: unknown[]) => mockSearchDirectory(...args),
  },
}));

const makeDir = (id: string) => ({
  createdAt: new Date().toISOString(),
  id,
  name: `Dir ${id}`,
  ownerUserDto: { email: "owner@test.com", id: "owner", name: "Owner" },
  parentId: "root",
  updatedAt: new Date().toISOString(),
});

describe("useDirectoryStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    mockSearchDirectory.mockReset();
  });

  it("starts with default values", () => {
    const store = useDirectoryStore();
    expect(store.selectedDirectories).toHaveLength(0);
    expect(store.modificationOriginDirId).toBeNull();
    expect(store.isLoading).toBe(false);
    expect(store.isSearching).toBe(false);
    expect(store.error).toBeNull();
  });

  it("searchDirectory returns results and updates state", async () => {
    const items = [makeDir("d1"), makeDir("d2")];
    mockSearchDirectory.mockResolvedValueOnce({ items, hasNext: false });

    const store = useDirectoryStore();
    const result = await store.searchDirectory({ nameContains: "test" });

    expect(result.success).toBe(true);
    expect(result.data?.items).toHaveLength(2);
    expect(mockSearchDirectory).toHaveBeenCalledWith({ nameContains: "test" });
  });

  it("searchDirectory sets isSearching during the call", async () => {
    mockSearchDirectory.mockImplementationOnce(
      () => new Promise((resolve) => setTimeout(() => { resolve({ items: [] }); }, 50)),
    );

    const store = useDirectoryStore();
    const promise = store.searchDirectory({ nameContains: "test" });
    expect(store.isSearching).toBe(true);
    await promise;
    expect(store.isSearching).toBe(false);
  });

  it("searchDirectory handles errors and sets error state", async () => {
    mockSearchDirectory.mockRejectedValueOnce(new Error("Network error"));

    const store = useDirectoryStore();
    const result = await store.searchDirectory({ nameContains: "test" });

    expect(result.success).toBe(false);
    expect(result.error).toBe("Network error");
    expect(store.error).toBe("Network error");
  });

  it("searchDirectory extracts Axios response error message", async () => {
    const axiosError = {
      isAxiosError: true,
      response: { data: { message: "Directory not found" } },
    };
    mockSearchDirectory.mockRejectedValueOnce(axiosError);

    const store = useDirectoryStore();
    const result = await store.searchDirectory({ nameContains: "lost" });

    expect(result.error).toBe("Directory not found");
  });

  it("clearError resets the error state", () => {
    const store = useDirectoryStore();
    store.error = "Something broke";
    store.clearError();
    expect(store.error).toBeNull();
  });
});
