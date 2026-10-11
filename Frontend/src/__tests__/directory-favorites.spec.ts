import { PiniaColada, useQueryCache } from "@pinia/colada";
import { createPinia } from "pinia";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent } from "vue";

import { directoryApi } from "@/api/directory";
import { deleteDirectory, moveDirectories, setDirectoryFavorite } from "@/mutations/directories";
import { DIRECTORY_QUERY_KEYS, favoriteDirectories } from "@/queries/directories";

const mountMutations = () => {
  const app = createApp(defineComponent({ render: () => null }));
  app.use(createPinia());
  app.use(PiniaColada);

  const mutations = app.runWithContext(() => ({
    favorite: setDirectoryFavorite(),
    move: moveDirectories(),
    remove: deleteDirectory(),
  }));
  const cache = app.runWithContext(() => useQueryCache());
  const invalidate = vi.spyOn(cache, "invalidateQueries").mockImplementation(() => undefined);

  return { app, cache, invalidate, ...mutations };
};

afterEach(() => {
  vi.restoreAllMocks();
});

describe("directory favorites", () => {
  it("sends explicit state and refreshes directory details, listings, and favorites", async () => {
    const save = vi.spyOn(directoryApi, "setFavorite").mockResolvedValue({
      directoryId: "folder-1",
      isStarred: true,
    });
    const { app, favorite, invalidate } = mountMutations();

    await favorite.mutateAsync({ directoryId: "folder-1", isStarred: true });

    expect(save).toHaveBeenCalledWith("folder-1", true);
    expect(invalidate).toHaveBeenCalledWith({ key: DIRECTORY_QUERY_KEYS.root });
    app.unmount();
  });

  it("propagates failures and invalidates potentially stale directory state", async () => {
    const error = new Error("Folder was removed");
    vi.spyOn(directoryApi, "setFavorite").mockRejectedValue(error);
    const { app, favorite, invalidate } = mountMutations();

    await expect(favorite.mutateAsync({ directoryId: "folder-1", isStarred: false })).rejects.toBe(
      error,
    );

    expect(invalidate).toHaveBeenCalledWith({ key: DIRECTORY_QUERY_KEYS.root });
    app.unmount();
  });

  it("keeps favorite pages separate by owner and requests live favorites", async () => {
    const search = vi.spyOn(directoryApi, "searchDirectory").mockResolvedValue({
      currentPage: 0,
      hasNext: false,
      hasPrevious: false,
      items: [],
      pageSize: 12,
      totalCount: 0,
      totalPages: 0,
    });
    const { app, cache } = mountMutations();
    const first = favoriteDirectories({ pageSize: 12, userId: "owner-1" });
    const second = favoriteDirectories({ pageSize: 12, userId: "owner-2" });

    expect(first.key).not.toEqual(second.key);
    expect(favoriteDirectories({ userId: "" }).enabled).toBe(false);
    await cache.fetch(cache.ensure(first));

    expect(search).toHaveBeenCalledWith({
      isDeleted: false,
      isStarred: true,
      ownerId: "owner-1",
      page: 0,
      pageSize: 12,
    });
    app.unmount();
  });

  it("refreshes favorite collections after moving and deleting folders", async () => {
    vi.spyOn(directoryApi, "moveDirectories").mockResolvedValue(undefined);
    vi.spyOn(directoryApi, "deleteDirectory").mockResolvedValue(undefined);
    const { app, invalidate, move, remove } = mountMutations();

    await move.mutateAsync({
      directoryIds: ["folder-1"],
      destinationId: "destination",
      originId: null,
    });
    expect(invalidate).toHaveBeenCalledWith({ key: [...DIRECTORY_QUERY_KEYS.root, "favorites"] });

    invalidate.mockClear();
    await remove.mutateAsync({ id: "folder-1", options: { force: false }, originId: null });
    expect(invalidate).toHaveBeenCalledWith({ key: [...DIRECTORY_QUERY_KEYS.root, "favorites"] });

    app.unmount();
  });
});
