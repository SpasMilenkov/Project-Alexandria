<template>
  <div class="px-4 py-4 sm:px-6 sm:py-5">
    <section
      class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70"
      aria-labelledby="playlists-heading"
    >
      <header
        class="sticky top-0 z-10 rounded-t-2xl border-b border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface-strong p-4 sm:px-6"
      >
        <div class="flex items-start justify-between gap-4">
          <div class="min-w-0">
            <h1
              id="playlists-heading"
              class="text-xl font-semibold text-gray-900 dark:text-gray-100"
            >
              Playlists
            </h1>
            <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
              Your collections and automatic mixes.
            </p>
          </div>
          <UButton
            icon="i-heroicons-plus"
            label="New playlist"
            color="primary"
            variant="solid"
            class="shrink-0"
            @click="showCreateModal = true"
          />
        </div>

        <div class="mt-4 flex flex-col gap-4 md:flex-row md:items-end">
          <div class="min-w-0">
            <p
              id="playlist-origin-label"
              class="mb-2 text-sm font-medium text-gray-900 dark:text-gray-100"
            >
              Collection
            </p>
            <div
              role="group"
              aria-labelledby="playlist-origin-label"
              class="inline-flex max-w-full gap-1 rounded-lg bg-black/5 dark:bg-white/5 p-1"
            >
              <UButton
                v-for="option in originOptions"
                :key="option.value"
                :label="option.label"
                :aria-pressed="originFilter === option.value"
                :color="originFilter === option.value ? 'primary' : 'neutral'"
                :variant="originFilter === option.value ? 'soft' : 'ghost'"
                size="sm"
                @click="originFilter = option.value"
              />
            </div>
          </div>
          <div class="grid grid-cols-2 gap-4 md:w-80">
            <UFormField label="Kind" name="playlist-kind">
              <USelect
                v-model="kindFilter"
                :items="kindFilterOptions"
                aria-label="Playlist kind"
                class="w-full"
              />
            </UFormField>
            <UFormField label="Sort by" name="playlist-sort">
              <USelect
                v-model="sort"
                :items="sortOptions"
                aria-label="Sort playlists"
                class="w-full"
              />
            </UFormField>
          </div>
        </div>

        <div
          class="mt-4 flex flex-wrap items-center gap-4 border-t border-gray-200/70 dark:border-gray-700/70 pt-4"
        >
          <div class="flex flex-wrap items-center gap-x-4 gap-y-2 min-w-0">
            <p
              class="text-sm text-gray-600 dark:text-gray-400 tabular-nums"
              role="status"
              aria-live="polite"
            >
              <template v-if="isFetching">Loading playlists…</template>
              <template v-else-if="playlistsQuery.status.value === 'error'"
                >Playlists unavailable</template
              >
              <template v-else-if="total === 0">0 playlists</template>
              <template v-else
                >{{ firstResult }}–{{ lastResult }} of {{ total }} playlists</template
              >
            </p>
            <UButton
              v-if="hasActiveFilters"
              label="Clear filters"
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
      </header>

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
          <p class="text-sm text-gray-600 dark:text-gray-400">Try another collection or kind.</p>
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

        <div v-else class="grid grid-cols-[repeat(auto-fill,minmax(min(100%,14rem),1fr))] gap-4">
          <PlaylistCard
            v-for="playlist in playlists"
            :key="playlist.id"
            :playlist="playlist"
            :is-playing="loadingPlaylistId === playlist.id"
            @open="navigateToPlaylist(playlist.id)"
            @edit="openEditModal(playlist)"
            @delete="confirmDelete(playlist)"
            @play="playPlaylist(playlist.id)"
          />
        </div>
      </div>
    </section>
  </div>

  <UModal
    v-model:open="showCreateModal"
    title="New Playlist"
    :ui="{ body: ' sm:p-0 p-0 overflow-hidden bg-transparent!', content: 'lg:min-w-2xl max-w-3xl' }"
  >
    <template #body>
      <div
        class="relative transition-colors duration-500"
        :style="
          createAmbient
            ? `background: linear-gradient(to left, ${createAmbient}35 65%, transparent 100%)`
            : 'bg-neutral-500'
        "
      >
        <PlaylistForm
          :loading="isCreateLoading"
          @submit="handleCreate"
          @cancel="showCreateModal = false"
          @ambient-change="createAmbient = $event"
        />
      </div>
    </template>
  </UModal>

  <UModal
    v-model:open="showEditModal"
    title="Edit Playlist"
    :ui="{
      body: 'sm:p-0 p-0 overflow-hidden bg-transparent! min-h-90',
      content: 'lg:min-w-2xl max-w-3xl',
    }"
  >
    <template #body>
      <div
        class="relative transition-colors duration-500 pb-6"
        :style="
          editAmbient
            ? `background: linear-gradient(to left, ${editAmbient}35 65%, transparent 100%)`
            : undefined
        "
      >
        <PlaylistForm
          v-if="editTarget"
          :initial="editTarget"
          :loading="isUpdateLoading"
          @submit="handleUpdate"
          @cancel="showEditModal = false"
          @ambient-change="editAmbient = $event"
        />
      </div>
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
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import type { PlaylistKindFilter, PlaylistOriginFilter } from "@/utils/playlist-display.utils";

import { fileApi } from "@/api/file";
import { type PlaylistResponse, playlistApi } from "@/api/playlist";
import { streamingApi } from "@/api/streaming";
import PlaylistCard from "@/components/streaming/PlaylistCard.vue";
import PlaylistForm, { type PlaylistFormPayload } from "@/components/streaming/PlaylistForm.vue";
import { AutoGroupKind } from "@/enums/auto-group-kind";
import { PlaylistSort } from "@/enums/playlist-sort";
import { createPlaylist, deletePlaylist, updatePlaylist } from "@/mutations/playlists";
import { PLAYLIST_QUERY_KEYS } from "@/queries/playlist";
import { usePlayerStore } from "@/stores/stream-player";
import { glassModalContent } from "@/utils/modalUi";
import { parsePlaylistBrowseQuery, playlistBrowseQuery } from "@/utils/playlist-display.utils";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const store = usePlayerStore();
const queryCache = useQueryCache();

const initialBrowse = parsePlaylistBrowseQuery(route.query);
const page = ref(initialBrowse.page);
const pageSize = 10;

// Browse state initializes from the route query and stays synced there, so
// drilling into a playlist and back (or reloading) restores page + filters.
// Filtering and sorting run server-side so page contents and totals always
// describe the same set.
const originFilter = ref<PlaylistOriginFilter>(initialBrowse.origin);
const kindFilter = ref<PlaylistKindFilter>(initialBrowse.kind);
const sort = ref<PlaylistSort>(initialBrowse.sort);

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

const playlistsQuery = useQuery({
  key: () =>
    PLAYLIST_QUERY_KEYS.list(
      page.value,
      pageSize,
      isAutoGeneratedParam.value,
      kindParam.value,
      sort.value,
    ),
  placeholderData: (prev) => prev,
  query: () =>
    playlistApi.getAll(page.value, pageSize, {
      isAutoGenerated: isAutoGeneratedParam.value,
      autoGroupKind: kindParam.value,
      sort: sort.value,
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
];

const hasActiveFilters = computed(() => originFilter.value !== "all" || kindFilter.value !== "all");

const kindFilterOptions = [
  { label: "All kinds", value: "all" as const },
  { label: "Artist", value: AutoGroupKind.Artist },
  { label: "Album", value: AutoGroupKind.Album },
  { label: "Tag", value: AutoGroupKind.Tag },
  { label: "Genre", value: AutoGroupKind.Genre },
  { label: "Parent genre", value: AutoGroupKind.ParentGenre },
  { label: "Decade", value: AutoGroupKind.Decade },
];

const sortOptions = [
  { label: "Newest", value: PlaylistSort.Newest },
  { label: "Name", value: PlaylistSort.Name },
  { label: "Track count", value: PlaylistSort.TrackCount },
];

const clearFilters = () => {
  originFilter.value = "all";
  kindFilter.value = "all";
  sort.value = PlaylistSort.Newest;
};

// A new filter set starts on page one: staying on a later page would show an
// empty grid when the filtered set is shorter. Pagination math itself is out
// of scope here.
watch([originFilter, kindFilter, sort], () => {
  page.value = 1;
});

// Outbound: reflect browse state in the URL (defaults omitted). Replace, not
// push, so paging does not spam history. Registered after the reset watcher
// so the reset page is what lands in the URL.
watch([page, originFilter, kindFilter, sort], () => {
  void router.replace({
    query: playlistBrowseQuery({
      page: page.value,
      origin: originFilter.value,
      kind: kindFilter.value,
      sort: sort.value,
    }),
  });
});

// Inbound: pick up query changes without a remount. Guarded to this route so
// leaving for a detail page never resets the restored state.
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
  },
);

const { mutateAsync: create, isLoading: isCreateLoading, state: createState } = createPlaylist();
const { mutateAsync: update, isLoading: isUpdateLoading, state: updateState } = updatePlaylist();
const { mutateAsync: remove, isLoading: isDeleteLoading, state: deleteState } = deletePlaylist();

const showCreateModal = ref(false);
const showEditModal = ref(false);
const showDeleteModal = ref(false);

const editTarget = ref<PlaylistResponse | null>(null);
const deleteTarget = ref<PlaylistResponse | null>(null);

// Tracks which playlist is currently being loaded into the queue.
// Used to show a loading indicator on the card.
const loadingPlaylistId = ref<string | null>(null);

const playPlaylist = async (playlistId: string) => {
  if (loadingPlaylistId.value === playlistId) return;

  loadingPlaylistId.value = playlistId;
  try {
    const result = await streamingApi.getFilesForStreaming({
      page: 1,
      pageSize: 500,
      playlistId,
      isVideo: false,
    });

    if (!result.items.length) {
      toast.add({
        title: "Playlist is empty",
        description: "Add some tracks before playing.",
        color: "warning",
      });
      return;
    }

    store.playNow(result.items);
  } catch {
    toast.add({
      title: "Failed to load playlist",
      description: "Could not fetch tracks. Please try again.",
      color: "error",
    });
  } finally {
    loadingPlaylistId.value = null;
  }
};

const openEditModal = (playlist: PlaylistResponse) => {
  editTarget.value = playlist;
  showEditModal.value = true;
};

const confirmDelete = (playlist: PlaylistResponse) => {
  deleteTarget.value = playlist;
  showDeleteModal.value = true;
};

const createAmbientBg = computed(() =>
  createAmbient.value
    ? `linear-gradient(to bottom right, ${createAmbient.value}28 0%, transparent 60%)`
    : "transparent",
);

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
      const { uploadUrl } = await playlistApi.getCoverUploadUrl({
        playlistId: playlist.id,
        mimeType: payload.coverFile.type,
        fileSize: payload.coverFile.size,
      });

      await fileApi.uploadToS3(uploadUrl, payload.coverFile);

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
    // (new ?v= cache-buster), and a cover URL fetched before the PUT lands
    // reads a 404 that nginx then caches.
    if (payload.coverFile) {
      const { uploadUrl } = await playlistApi.getCoverUploadUrl({
        playlistId: id,
        mimeType: payload.coverFile.type,
        fileSize: payload.coverFile.size,
      });

      await fileApi.uploadToS3(uploadUrl, payload.coverFile);
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

const createAmbient = ref<string | null>(null);
const editAmbient = ref<string | null>(null);

watch(showCreateModal, (open) => {
  if (!open) createAmbient.value = null;
});

watch(showEditModal, (open) => {
  if (!open) editAmbient.value = null;
  else editAmbient.value = editTarget.value?.ambientTheme ?? null;
});
</script>

<style lang="css">
.ambient-cover {
  background: v-bind(createAmbientBg);
}
</style>
