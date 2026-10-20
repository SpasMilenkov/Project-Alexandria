<template>
  <div>
    <section
      class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70"
      aria-labelledby="playlists-heading"
    >
      <header class="frosted-glass glass-surface px-4 pb-6 pt-6 sm:px-6 sm:pt-8">
        <div class="flex items-center justify-between gap-4 sm:items-start">
          <div class="min-w-0">
            <h1 id="playlists-heading" class="text-2xl font-bold text-gray-900 dark:text-gray-100">
              Playlists
            </h1>
            <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
              Your collections and automatic mixes.
            </p>
          </div>
          <UButton
            icon="i-heroicons-plus"
            label="New playlist"
            color="primary"
            variant="solid"
            :ui="{ label: 'max-sm:sr-only' }"
            class="h-10 shrink-0 rounded-xl max-sm:w-10 max-sm:justify-center max-sm:px-0"
            @click="showCreateModal = true"
          />
        </div>

        <!-- One grouped panel below md so search, sort and filters read as a
             single unit. From md up the panel chrome drops away and the
             controls sit directly on the page surface. -->
        <div
          class="mt-6 flex flex-col gap-4 max-md:rounded-xl max-md:border max-md:border-gray-200/70 max-md:bg-black/5 max-md:p-4 max-md:dark:border-gray-700/70 max-md:dark:bg-white/5"
        >
          <div class="flex flex-col gap-4 md:flex-row md:items-center">
            <div class="relative flex w-full flex-col gap-2 md:max-w-xl md:flex-1">
              <UInput
                v-model="searchTerm"
                icon="i-heroicons-magnifying-glass"
                placeholder="Search playlists"
                aria-label="Search playlists"
                :maxlength="PLAYLIST_SEARCH_MAX_LENGTH"
                :ui="searchUi"
                class="w-full"
              />
              <div
                class="flex items-center gap-1 max-md:justify-between md:absolute md:inset-y-1 md:right-1"
              >
                <UButton
                  :icon="searchItems ? 'mdi-check' : undefined"
                  label="Inside tracks"
                  size="sm"
                  :color="searchItems ? 'primary' : 'neutral'"
                  :variant="searchItems ? 'soft' : 'outline'"
                  :aria-pressed="searchItems"
                  class="h-8 rounded-lg px-3"
                  @click="searchItems = !searchItems"
                />
                <UButton
                  v-if="searchTerm"
                  variant="ghost"
                  color="neutral"
                  size="sm"
                  icon="i-heroicons-x-mark"
                  aria-label="Clear search"
                  class="h-8 w-8 justify-center rounded-lg"
                  @click="clearSearch"
                />
              </div>
            </div>

            <div class="flex w-full items-center gap-2 md:w-auto">
              <USelect
                v-model="sort"
                :items="sortOptions"
                aria-label="Sort playlists"
                :ui="fieldUi"
                class="w-full md:w-48"
                @update:model-value="onSortSelected"
              />
              <UButton
                variant="outline"
                color="neutral"
                :title="
                  sortDirection === SortDirection.Asc
                    ? 'Switch to descending'
                    : 'Switch to ascending'
                "
                :aria-label="sortDirection === SortDirection.Asc ? 'Ascending' : 'Descending'"
                :icon="
                  sortDirection === SortDirection.Asc ? 'mdi-sort-ascending' : 'mdi-sort-descending'
                "
                class="h-10 w-10 shrink-0 justify-center rounded-xl"
                @click="toggleSortDirection"
              />
            </div>
          </div>

          <div class="flex flex-col gap-4 md:flex-row md:items-center">
            <div
              role="group"
              aria-label="Collection"
              class="flex gap-1 rounded-xl bg-black/5 p-1 dark:bg-white/5 md:inline-flex"
            >
              <UButton
                v-for="option in originOptions"
                :key="option.value"
                :label="option.label"
                :aria-pressed="originFilter === option.value"
                :color="originFilter === option.value ? 'primary' : 'neutral'"
                :variant="originFilter === option.value ? 'soft' : 'ghost'"
                class="h-8 flex-1 justify-center rounded-lg px-4 md:flex-none"
                @click="originFilter = option.value"
              />
            </div>

            <span
              aria-hidden="true"
              class="hidden h-8 w-px bg-gray-200/70 dark:bg-gray-700/70 md:block"
            />

            <div role="group" aria-label="Kind" class="flex flex-wrap items-center gap-2">
              <span
                class="w-full text-sm font-semibold text-gray-600 dark:text-gray-400 md:w-auto md:pr-2"
              >
                Kind
              </span>
              <UButton
                v-for="option in kindFilterOptions"
                :key="option.value"
                :label="option.label"
                :aria-pressed="kindFilter === option.value"
                :color="kindFilter === option.value ? 'primary' : 'neutral'"
                :variant="kindFilter === option.value ? 'soft' : 'outline'"
                class="h-8 rounded-xl px-4"
                @click="kindFilter = option.value"
              />
            </div>
          </div>
        </div>
      </header>

      <!-- Results bar: the only element that pins. Everything above scrolls
           away naturally, so there is no scroll listener and no measuring. -->
      <div
        class="frosted-glass glass-surface-strong sticky top-0 z-10 flex flex-wrap items-center justify-between gap-x-4 gap-y-2 border-y border-gray-200/70 px-4 py-3 dark:border-gray-700/70 sm:px-6"
      >
        <div class="flex min-w-0 flex-wrap items-center gap-2">
          <p
            class="mr-2 text-sm font-semibold text-gray-900 tabular-nums dark:text-gray-100"
            role="status"
            aria-live="polite"
          >
            <template v-if="isFetching">Loading playlists…</template>
            <template v-else-if="playlistsQuery.status.value === 'error'"
              >Playlists unavailable</template
            >
            <template v-else-if="total === 0">0 playlists</template>
            <template v-else>{{ firstResult }}–{{ lastResult }} of {{ total }} playlists</template>
          </p>
          <span
            v-for="pill in activeFilterPills"
            :key="pill.key"
            class="inline-flex items-center gap-1 rounded-lg bg-primary/10 py-1 pl-3 pr-1 text-sm font-semibold text-primary"
          >
            {{ pill.label }}
            <UButton
              icon="i-heroicons-x-mark"
              size="xs"
              color="primary"
              variant="ghost"
              :aria-label="`Remove ${pill.label} filter`"
              class="justify-center rounded-md"
              @click="pill.clear()"
            />
          </span>
          <UButton
            v-if="hasActiveFilters"
            label="Clear all"
            color="neutral"
            variant="ghost"
            size="xs"
            @click="clearFilters"
          />
        </div>
        <UPagination
          v-if="totalPages > 1"
          v-model:page="page"
          :total="total"
          :items-per-page="pageSize"
          :sibling-count="1"
          :disabled="isFetching"
          :ui="{ first: 'hidden', last: 'hidden' }"
          color="neutral"
          variant="ghost"
          active-color="primary"
          active-variant="soft"
          size="sm"
          aria-label="Playlist pages"
          class="shrink-0"
        />
      </div>

      <div class="p-4 sm:p-6" :aria-busy="isFetching">
        <div v-if="playlistsQuery.status.value === 'pending'" class="flex justify-center py-16">
          <Icon
            icon="mdi:loading"
            class="w-6 h-6 animate-spin text-gray-600 dark:text-gray-400"
            aria-label="Loading playlists"
          />
        </div>

        <UAlert
          v-else-if="playlistsQuery.status.value === 'error'"
          icon="i-mdi-alert-circle-outline"
          color="error"
          variant="subtle"
          title="Failed to load playlists"
          description="Please try refreshing the page."
        />

        <div
          v-else-if="!playlists.length && hasActiveFilters"
          class="flex flex-col items-center justify-center gap-2 py-16 text-center"
        >
          <Icon icon="mdi:magnify" class="w-12 h-12 text-gray-400 dark:text-gray-600 mb-2" />
          <p class="font-medium text-gray-900 dark:text-gray-100">No playlists match</p>
          <p class="text-sm text-gray-600 dark:text-gray-400">
            Try another search, collection, or kind.
          </p>
          <UButton
            label="Clear filters"
            color="neutral"
            variant="outline"
            class="mt-2"
            @click="clearFilters"
          />
        </div>

        <div
          v-else-if="!playlists.length"
          class="flex flex-col items-center justify-center gap-2 py-16 text-center"
        >
          <Icon icon="mdi:playlist-music" class="w-12 h-12 text-gray-400 dark:text-gray-600 mb-2" />
          <p class="font-medium text-gray-900 dark:text-gray-100">No playlists yet</p>
          <p class="text-sm text-gray-600 dark:text-gray-400">
            Create your first playlist to get started.
          </p>
          <UButton
            label="New playlist"
            icon="i-heroicons-plus"
            color="neutral"
            variant="outline"
            class="mt-2"
            @click="showCreateModal = true"
          />
        </div>

        <div
          v-else
          class="grid grid-cols-2 gap-4 sm:grid-cols-[repeat(auto-fill,minmax(min(100%,14rem),1fr))] sm:gap-6"
        >
          <PlaylistCard
            v-for="playlist in playlists"
            :key="playlist.id"
            :playlist="playlist"
            :is-playing="playingPlaylistId === playlist.id && store.isPlaying"
            :is-loading="loadingPlaylistId === playlist.id"
            @open="navigateToPlaylist(playlist.id)"
            @edit="openEditModal(playlist)"
            @delete="confirmDelete(playlist)"
            @play="playPlaylist(playlist)"
            @add-to-queue="addPlaylistToQueue(playlist.id, playlist.name)"
            @favorite="toggleFavorite(playlist)"
          />
        </div>
      </div>
    </section>
  </div>

  <UModal
    v-model:open="showCreateModal"
    title="New playlist"
    description="Give your next collection a personal touch."
    :ui="playlistModalUi"
  >
    <template #body>
      <PlaylistForm
        :loading="isCreateLoading"
        @submit="handleCreate"
        @cancel="showCreateModal = false"
      />
    </template>
  </UModal>

  <UModal
    v-model:open="showEditModal"
    title="Edit playlist"
    description="Update the details and artwork for your collection."
    :ui="playlistModalUi"
  >
    <template #body>
      <PlaylistForm
        v-if="editTarget"
        :initial="editTarget"
        :loading="isUpdateLoading"
        @submit="handleUpdate"
        @cancel="showEditModal = false"
      />
    </template>
  </UModal>

  <UModal
    v-model:open="showDeleteModal"
    title="Delete Playlist"
    :ui="{ content: glassModalContent }"
  >
    <template #body>
      <p class="text-sm text-default">
        Are you sure you want to delete
        <span class="font-semibold">{{ deleteTarget?.name }}</span
        >? This cannot be undone.
      </p>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="outline"
          @click="showDeleteModal = false"
        />
        <UButton
          label="Delete"
          color="error"
          variant="solid"
          :loading="isDeleteLoading"
          @click="handleDelete"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery, useQueryCache } from "@pinia/colada";
import { useDebounceFn } from "@vueuse/core";
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import { type PlaylistResponse, playlistApi } from "@/api/playlist";
import PlaylistCard from "@/components/streaming/PlaylistCard.vue";
import PlaylistForm, { type PlaylistFormPayload } from "@/components/streaming/PlaylistForm.vue";
import { AutoGroupKind } from "@/enums/auto-group-kind";
import { PlaylistSort } from "@/enums/playlist-sort";
import { SortDirection } from "@/enums/SortDirection";
import {
  createPlaylist,
  deletePlaylist,
  setPlaylistFavorite,
  updatePlaylist,
} from "@/mutations/playlists";
import { PLAYLIST_QUERY_KEYS } from "@/queries/playlist";
import { usePlayerStore } from "@/stores/stream-player";
import { glassModalContent, playlistModalUi } from "@/utils/modalUi";
import {
  PLAYLIST_SEARCH_MAX_LENGTH,
  type PlaylistKindFilter,
  type PlaylistOriginFilter,
  naturalSortDirection,
  parsePlaylistBrowseQuery,
  playlistBrowseQuery,
} from "@/utils/playlist-display.utils";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const store = usePlayerStore();
const queryCache = useQueryCache();

// Shared control shape: 40px tall fields, 32px chips and in-field controls.
// Standalone controls use a 12px radius; nested items (segmented buttons, the
// in-field toggle) use rounded-lg so the corners stay concentric with their
// container. The search field reserves room on the right from md up for the
// absolutely placed toggle cluster.
const fieldUi = { base: "h-10 rounded-xl" };
const searchUi = { base: "h-10 rounded-xl md:pe-44" };

const initialBrowse = parsePlaylistBrowseQuery(route.query);
const page = ref(initialBrowse.page);
const pageSize = 15;

// Browse state initializes from the route query and stays synced there, so
// drilling into a playlist and back (or reloading) restores page + filters.
// Filtering, search, and sorting run server-side so page contents and totals
// always describe the same set.
const originFilter = ref<PlaylistOriginFilter>(initialBrowse.origin);
const kindFilter = ref<PlaylistKindFilter>(initialBrowse.kind);
const sort = ref<PlaylistSort>(initialBrowse.sort);
const sortDirection = ref<SortDirection>(initialBrowse.direction);

// The raw input drives the URL and the query only through the debounce, so
// typing does not fire a request per keystroke.
const searchTerm = ref(initialBrowse.q);
const searchItems = ref(initialBrowse.searchItems);
const debouncedQ = ref(initialBrowse.q);

const setDebouncedSearch = useDebounceFn((value: string) => {
  debouncedQ.value = value.trim();
}, 250);

watch(searchTerm, (value) => {
  setDebouncedSearch(value);
});

const clearSearch = () => {
  searchTerm.value = "";
  debouncedQ.value = "";
};

// Picking a different sort field resets to that field's natural direction;
// the toggle next to the select then flips from there.
const onSortSelected = (value: PlaylistSort) => {
  sortDirection.value = naturalSortDirection(value);
};

const toggleSortDirection = () => {
  sortDirection.value =
    sortDirection.value === SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;
};

const isAutoGeneratedParam = computed(() => {
  if (originFilter.value === "auto") {
    return true;
  }
  if (originFilter.value === "manual") {
    return false;
  }
  return undefined;
});

const kindParam = computed(() => (kindFilter.value === "all" ? undefined : kindFilter.value));

const isStarredParam = computed(() => (originFilter.value === "favorites" ? true : undefined));

const playlistsQuery = useQuery({
  key: () =>
    PLAYLIST_QUERY_KEYS.list(
      page.value,
      pageSize,
      isAutoGeneratedParam.value,
      kindParam.value,
      isStarredParam.value,
      sort.value,
      sortDirection.value,
      debouncedQ.value || null,
      searchItems.value,
    ),
  placeholderData: (prev) => prev,
  query: () =>
    playlistApi.getAll(page.value, pageSize, {
      isAutoGenerated: isAutoGeneratedParam.value,
      autoGroupKind: kindParam.value,
      isStarred: isStarredParam.value,
      sort: sort.value,
      sortDirection: sortDirection.value,
      query: debouncedQ.value || null,
      searchItems: searchItems.value,
    }),
});

const playlists = computed(() => playlistsQuery.data.value?.items ?? []);
const total = computed(() => playlistsQuery.data.value?.totalCount ?? 0);
const totalPages = computed(() => Math.ceil(total.value / pageSize));
const isFetching = computed(() => playlistsQuery.asyncStatus.value === "loading");
const firstResult = computed(() => (playlists.value.length ? (page.value - 1) * pageSize + 1 : 0));
const lastResult = computed(() =>
  Math.min((page.value - 1) * pageSize + playlists.value.length, total.value),
);

const originOptions: { label: string; value: PlaylistOriginFilter }[] = [
  { label: "All", value: "all" },
  { label: "Auto", value: "auto" },
  { label: "Manual", value: "manual" },
  { label: "Favorites", value: "favorites" },
];

const hasActiveFilters = computed(
  () => originFilter.value !== "all" || kindFilter.value !== "all" || debouncedQ.value !== "",
);

const kindFilterOptions = [
  { label: "All kinds", value: "all" as const },
  { label: "Artist", value: AutoGroupKind.Artist },
  { label: "Album", value: AutoGroupKind.Album },
  { label: "Theme", value: AutoGroupKind.Tag },
  { label: "Genre", value: AutoGroupKind.Genre },
  { label: "Decade", value: AutoGroupKind.Decade },
];

const sortOptions = [
  { label: "Created", value: PlaylistSort.Newest },
  { label: "Name", value: PlaylistSort.Name },
  { label: "Track count", value: PlaylistSort.TrackCount },
];

const clearFilters = () => {
  originFilter.value = "all";
  kindFilter.value = "all";
  sort.value = PlaylistSort.Newest;
  sortDirection.value = naturalSortDirection(PlaylistSort.Newest);
  clearSearch();
  searchItems.value = false;
};

interface FilterPill {
  key: string;
  label: string;
  clear: () => void;
}

// One removable pill per active filter, shown in the pinned results bar so
// the current filter set stays visible after the controls scroll away.
// Sort is left out on purpose, matching hasActiveFilters.
const activeFilterPills = computed(() => {
  const pills: FilterPill[] = [];

  if (originFilter.value !== "all") {
    const origin = originOptions.find((option) => option.value === originFilter.value);
    pills.push({
      key: "origin",
      label: origin?.label ?? "Collection",
      clear: () => {
        originFilter.value = "all";
      },
    });
  }

  if (kindFilter.value !== "all") {
    const kind = kindFilterOptions.find((option) => option.value === kindFilter.value);
    pills.push({
      key: "kind",
      label: kind?.label ?? "Kind",
      clear: () => {
        kindFilter.value = "all";
      },
    });
  }

  if (debouncedQ.value !== "") {
    pills.push({ key: "query", label: `"${debouncedQ.value}"`, clear: clearSearch });
  }

  return pills;
});

// A new filter set starts on page one: staying on a later page would show an
// empty grid when the filtered set is shorter. Pagination math itself is out
// of scope here.
watch([originFilter, kindFilter, sort, sortDirection, debouncedQ, searchItems], () => {
  page.value = 1;
});

// Outbound: reflect browse state in the URL (defaults omitted). Replace, not
// push, so paging does not spam history. Registered after the reset watcher
// so the reset page is what lands in the URL.
watch([page, originFilter, kindFilter, sort, sortDirection, debouncedQ, searchItems], () => {
  void router.replace({
    query: playlistBrowseQuery({
      page: page.value,
      origin: originFilter.value,
      kind: kindFilter.value,
      sort: sort.value,
      direction: sortDirection.value,
      q: debouncedQ.value,
      searchItems: searchItems.value,
    }),
  });
});

// Inbound: pick up query changes without a remount. Guarded to this route so
// leaving for a detail page never resets the restored state. Search fields are
// only adopted when they differ from local state: our own replace echoes them
// back and overwriting would clobber in-flight typing.
watch(
  () => route.query,
  () => {
    if (route.path !== "/streaming/playlists") {
      return;
    }
    const next = parsePlaylistBrowseQuery(route.query);
    page.value = next.page;
    originFilter.value = next.origin;
    kindFilter.value = next.kind;
    sort.value = next.sort;
    sortDirection.value = next.direction;

    if (next.q !== debouncedQ.value) {
      searchTerm.value = next.q;
      debouncedQ.value = next.q;
    }
    if (next.searchItems !== searchItems.value) {
      searchItems.value = next.searchItems;
    }
  },
);

const { mutateAsync: create, isLoading: isCreateLoading, state: createState } = createPlaylist();
const { mutateAsync: update, isLoading: isUpdateLoading, state: updateState } = updatePlaylist();
const { mutateAsync: remove, isLoading: isDeleteLoading, state: deleteState } = deletePlaylist();
const { mutateAsync: setFavorite, isLoading: isFavoritePending } = setPlaylistFavorite();

const showCreateModal = ref(false);
const showEditModal = ref(false);
const showDeleteModal = ref(false);

const editTarget = ref<PlaylistResponse | null>(null);
const deleteTarget = ref<PlaylistResponse | null>(null);

// Tracks which playlist is currently being loaded into the queue.
// Used to show a loading indicator on the card.
const loadingPlaylistId = ref<string | null>(null);

// The playlist the player is currently sourced from, so its card keeps a
// visible playing state after the load finishes.
const playingPlaylistId = computed(() => store.context?.ref.playlistId ?? null);

const playPlaylist = async (playlist: PlaylistResponse) => {
  if (loadingPlaylistId.value === playlist.id) return;

  if (playingPlaylistId.value === playlist.id) {
    store.togglePlay();
    return;
  }

  loadingPlaylistId.value = playlist.id;
  try {
    await store.startContext({ isVideo: false, playlistId: playlist.id }, { label: playlist.name });
  } finally {
    loadingPlaylistId.value = null;
  }
};

const openEditModal = (playlist: PlaylistResponse) => {
  editTarget.value = playlist;
  showEditModal.value = true;
};

const addingPlaylistId = ref<string | null>(null);

const addPlaylistToQueue = async (playlistId: string, label?: string) => {
  if (addingPlaylistId.value === playlistId) return;
  addingPlaylistId.value = playlistId;
  try {
    const queuedCount = await store.addPlaylistToQueue(playlistId, label ?? null);
    if (queuedCount === 0) {
      toast.add({
        title: "Playlist is empty",
        description: "Add some tracks before queueing.",
        color: "warning",
      });
      return;
    }
    toast.add({
      title: `Added ${queuedCount} track${queuedCount === 1 ? "" : "s"} to queue`,
      color: "success",
    });
  } catch {
    toast.add({
      title: "Failed to queue playlist",
      description: "Could not fetch tracks. Please try again.",
      color: "error",
    });
  } finally {
    addingPlaylistId.value = null;
  }
};

const confirmDelete = (playlist: PlaylistResponse) => {
  deleteTarget.value = playlist;
  showDeleteModal.value = true;
};

const toggleFavorite = async (playlist: PlaylistResponse) => {
  if (isFavoritePending.value) return;

  try {
    await setFavorite({ playlistId: playlist.id, isStarred: !playlist.isStarred });
  } catch {
    toast.add({ title: "Failed to update favorite", color: "error" });
  }
};

const navigateToPlaylist = (id: string) => {
  router.push(`/streaming/playlists/${id}`);
};

const handleCreate = async (payload: PlaylistFormPayload) => {
  try {
    const playlist = await playlistApi.create({
      name: payload.name,
      description: payload.description,
      hasCover: Boolean(payload.coverFile),
    });

    if (payload.coverFile) {
      await playlistApi.uploadPlaylistCover(playlist.id, payload.coverFile);

      if (payload.ambientTheme) {
        await playlistApi.update(playlist.id, { ambientTheme: payload.ambientTheme });
      }
    }

    await queryCache.invalidateQueries({ key: PLAYLIST_QUERY_KEYS.root });
    showCreateModal.value = false;
  } catch {
    toast.add({ title: "Failed to create playlist", color: "error" });
  }
};

const handleUpdate = async (payload: PlaylistFormPayload) => {
  if (!editTarget.value) return;

  const id = editTarget.value.id;

  try {
    // Upload the bytes before flagging the cover: the update bumps updatedAt
    // (new ?v= cache-buster), and a cover URL fetched before the upload lands
    // reads a 404 that nginx then caches.
    if (payload.coverFile) {
      await playlistApi.uploadPlaylistCover(id, payload.coverFile);
    }

    await update({
      id,
      req: {
        name: payload.name,
        description: payload.description,
        hasCover: Boolean(payload.coverFile),
      },
    });

    if (payload.ambientTheme) {
      await playlistApi.update(id, { ambientTheme: payload.ambientTheme });
    }

    if (!updateState.value.error) {
      await queryCache.invalidateQueries({ key: PLAYLIST_QUERY_KEYS.root });
      showEditModal.value = false;
      editTarget.value = null;
    }
  } catch {
    toast.add({ title: "Failed to update playlist", color: "error" });
  }
};

const handleDelete = async () => {
  if (!deleteTarget.value) return;
  await remove(deleteTarget.value.id);
  if (!deleteState.value.error) {
    showDeleteModal.value = false;
    deleteTarget.value = null;
  }
};
</script>
