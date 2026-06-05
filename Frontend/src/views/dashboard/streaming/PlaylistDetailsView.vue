<template>
  <div class="px-2 md:px-6 py-5">
    <!-- Back -->
    <UButton
      icon="mdi:arrow-left"
      label="Playlists"
      color="neutral"
      variant="ghost"
      size="sm"
      class="mb-4 -ml-1"
      @click="router.push('/streaming/playlists')"
    />

    <!-- Loading -->
    <div v-if="detailQuery.status.value === 'pending'" class="flex justify-center py-16">
      <UIcon name="mdi:loading" class="w-6 h-6 animate-spin text-muted" />
    </div>

    <UAlert
      v-else-if="detailQuery.status.value === 'error'"
      icon="mdi:alert-circle-outline"
      color="error"
      variant="subtle"
      title="Failed to load playlist"
    />

    <template v-else-if="playlist">
      <!-- Header card with blurred cover background -->
      <div
        class="relative rounded-2xl overflow-hidden mb-6 border border-gray-200/70 dark:border-gray-700/70"
      >
        <!-- Background: blurred cover image or ambient tint -->
        <div class="absolute inset-0">
          <img
            v-if="!isCoverUrlLoading && coverUrl"
            :src="coverUrl"
            :alt="playlist.name"
            class="w-full h-full object-cover blur-sm scale-125 opacity-25 dark:opacity-15"
          />
          <div
            v-else-if="playlist.ambientTheme"
            class="w-full h-full"
            :style="{ backgroundColor: playlist.ambientTheme, opacity: 0.08 }"
          />
        </div>
        <!-- <div class="absolute inset-0 bg-white/60 dark:bg-gray-950/70" /> -->

        <!-- Content -->
        <div class="relative flex flex-col md:flex-row items-center md:items-end gap-5 p-5 md:p-6">
          <!-- Cover image -->
          <div
            class="w-48 shrink-0 aspect-square rounded-xl overflow-hidden shadow-lg"
            :style="coverShadowStyle"
          >
            <img
              v-if="!isCoverUrlLoading && coverUrl"
              :src="coverUrl"
              :alt="playlist.name"
              class="w-full h-full object-cover"
            />
            <div
              v-else
              class="w-full h-full bg-gray-200/50 dark:bg-gray-700/40 flex items-center justify-center"
            >
              <UIcon name="mdi:playlist-music" class="w-12 h-12 text-muted" />
            </div>
          </div>

          <!-- Info + actions -->
          <div class="flex-1 min-w-0 w-full text-center md:text-left">
            <h1 class="text-2xl font-bold text-highlighted">{{ playlist.name }}</h1>
            <p v-if="playlist.description" class="text-sm text-muted mt-1">
              {{ playlist.description }}
            </p>
            <UBadge
              :label="`${localItems.length} ${localItems.length === 1 ? 'item' : 'items'}`"
              color="neutral"
              variant="subtle"
              size="sm"
              class="mt-2"
            />

            <!-- Action buttons -->
            <div class="flex flex-wrap items-center justify-center md:justify-start gap-2 mt-4">
              <UButton
                icon="mdi:play"
                label="Play all"
                color="primary"
                variant="solid"
                size="sm"
                :loading="isLoadingQueue"
                :disabled="!localItems.length"
                @click="playAll"
              />
              <UButton
                :icon="showSearch ? 'i-heroicons-x-mark' : 'i-heroicons-plus'"
                :label="showSearch ? 'Cancel' : 'Add tracks'"
                color="primary"
                variant="outline"
                size="sm"
                @click="showSearch = !showSearch"
              />
              <UButton
                icon="mdi:pencil"
                label="Edit"
                color="neutral"
                variant="outline"
                size="sm"
                @click="showEditModal = true"
              />
              <UButton
                icon="i-heroicons-trash"
                color="error"
                variant="outline"
                size="sm"
                @click="showDeleteModal = true"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Track search panel -->
      <div
        v-if="showSearch"
        class="mb-5 p-4 rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/40 dark:bg-white/3 backdrop-blur-sm"
      >
        <p class="text-sm font-medium text-highlighted mb-3">Add tracks</p>
        <PlaylistTrackSearch
          :adding-ids="addingIds"
          :added-ids="addedItemJobIds"
          @add="handleAddItem"
        />
      </div>

      <!-- Empty state -->
      <div
        v-if="!localItems.length"
        class="flex flex-col items-center justify-center py-16 text-center border border-dashed border-gray-200/70 dark:border-gray-700/70 rounded-xl"
      >
        <UIcon name="mdi:music-note-off" class="w-10 h-10 text-muted mb-3" />
        <p class="font-medium text-highlighted">No items in this playlist</p>
        <p class="text-sm text-muted mt-1">Use "Add tracks" above to get started.</p>
      </div>

      <!-- Item list with ambient color tint -->
      <div
        v-else
        class="relative rounded-xl overflow-hidden border border-gray-200/70 dark:border-gray-700/70"
      >
        <!-- Ambient color background overlay -->
        <div
          v-if="playlist.ambientTheme"
          class="absolute inset-0 pointer-events-none"
          :style="{ backgroundColor: playlist.ambientTheme, opacity: ambientListOpacity }"
        />
        <!-- Ambient top accent line -->
        <div
          v-if="playlist.ambientTheme"
          class="absolute top-0 inset-x-0 h-0.5 pointer-events-none z-10"
          :style="{ backgroundColor: playlist.ambientTheme, opacity: 0.4 }"
        />

        <div class="relative flex flex-col divide-y divide-gray-100/50 dark:divide-gray-800/50">
          <PlaylistItemRow
            v-for="item in localItems"
            :key="item.id"
            :item="item"
            :dragged-item-id="draggedItemId"
            @remove="confirmRemoveItem(item.id)"
            @drag-start="onDragStart"
            @drag-end="onDragEnd"
            @drag-over="onDragOver"
            @drop="onDrop"
            @play="playFromItem(item.transpilationJobId)"
          />
        </div>
      </div>

      <!-- Reorder saving indicator -->
      <p v-if="isReorderingPlaylistItem" class="text-xs text-muted text-center mt-2">
        Saving order...
      </p>
    </template>
  </div>

  <!-- Edit modal -->
  <UModal v-model:open="showEditModal" title="Edit Playlist">
    <template #body>
      <PlaylistForm
        v-if="playlist"
        :initial="playlist as any"
        :loading="isUpdating"
        @submit="handleUpdate"
        @cancel="showEditModal = false"
      />
    </template>
  </UModal>

  <!-- Delete confirm modal -->
  <UModal v-model:open="showDeleteModal" title="Delete Playlist">
    <template #body>
      <p class="text-sm text-default">
        Are you sure you want to delete
        <span class="font-semibold">{{ playlist?.name }}</span
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
          :loading="isDeletingPlaylist"
          @click="handleDelete"
        />
      </div>
    </template>
  </UModal>

  <!-- Remove item confirm modal -->
  <UModal v-model:open="showRemoveItemModal" title="Remove Item">
    <template #body>
      <p class="text-sm text-default">Remove this item from the playlist?</p>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="outline"
          @click="showRemoveItemModal = false"
        />
        <UButton
          label="Remove"
          color="error"
          variant="solid"
          :loading="isRemovingPlaylistItem"
          @click="handleRemoveItem"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import type { PlaylistItemResponse } from "@/api/playlist";
import type { UpdatePlaylistSchema } from "@/schemas/playlist";

import { playlistApi } from "@/api/playlist";
import { streamingApi } from "@/api/streaming";
import PlaylistForm from "@/components/streaming/PlaylistForm.vue";
import PlaylistItemRow from "@/components/streaming/PlaylistItemRow.vue";
import PlaylistTrackSearch from "@/components/streaming/PlaylistTrackSearch.vue";
import { useTheme } from "@/composables/useTheme";
import {
  addPlaylistItem,
  deletePlaylist,
  removePlaylistItem,
  reorderPlaylistItems,
  updatePlaylist,
} from "@/mutations/playlists";
import { PLAYLIST_QUERY_KEYS, getPlaylistCover } from "@/queries/playlist";
import { usePlayerStore } from "@/stores/stream-player";

const store = usePlayerStore();
const toast = useToast();
const { isDark } = useTheme();

const isLoadingQueue = ref(false);

const playAll = async () => {
  isLoadingQueue.value = true;
  try {
    const result = await streamingApi.getFilesForStreaming({
      page: 1,
      pageSize: 500,
      playlistId: playlistId.value,
      isVideo: false,
    });
    if (!result.items.length) {
      toast.add({ title: "Playlist is empty", color: "warning" });
      return;
    }
    store.playNow(result.items);
  } catch {
    toast.add({ title: "Failed to load playlist", color: "error" });
  } finally {
    isLoadingQueue.value = false;
  }
};

const route = useRoute();
const router = useRouter();

const playlistId = computed(() => route.params.id as string);

const detailQuery = useQuery({
  key: () => PLAYLIST_QUERY_KEYS.detail(playlistId.value),
  query: () => playlistApi.getById(playlistId.value),
  staleTime: 30_000,
});

const playlist = computed(() => detailQuery.data.value ?? null);

const { data: coverUrl, isLoading: isCoverUrlLoading } = useQuery({
  ...getPlaylistCover(playlistId.value),
  enabled: computed(() => (playlist.value === null ? false : playlist.value.hasCover)),
});

// Ambient color helpers
const coverShadowStyle = computed(() => {
  if (!playlist.value?.ambientTheme) return {};
  return {
    boxShadow: `0 8px 24px -4px color-mix(in srgb, ${playlist.value.ambientTheme} 30%, transparent)`,
  };
});

const ambientListOpacity = computed(() => (isDark.value ? 0.1 : 0.06));

const { mutateAsync: update, isLoading: isUpdating, state: updateState } = updatePlaylist();
const { mutateAsync: remove, isLoading: isDeletingPlaylist, state: deleteState } = deletePlaylist();
const { mutateAsync: addItem } = addPlaylistItem();
const {
  mutateAsync: removeItem,
  isLoading: isRemovingPlaylistItem,
  state: removeItemState,
} = removePlaylistItem();
const {
  mutateAsync: reorder,
  isLoading: isReorderingPlaylistItem,
  state: reorderState,
} = reorderPlaylistItems();

// Local item list
const localItems = ref<PlaylistItemResponse[]>([]);

watch(
  () => playlist.value?.items,
  (items) => {
    if (items) localItems.value = [...items];
  },
  { immediate: true },
);

const addedItemJobIds = computed(() => new Set(localItems.value.map((i) => i.transpilationJobId)));

const addingIds = ref<Set<string>>(new Set());

// Modal state
const showSearch = ref(false);
const showEditModal = ref(false);
const showDeleteModal = ref(false);
const showRemoveItemModal = ref(false);
const removeItemTarget = ref<string | null>(null);

// Drag-and-drop state
const draggedItemId = ref<string | null>(null);

const playFromItem = async (transpilationJobId: string) => {
  isLoadingQueue.value = true;
  try {
    const result = await streamingApi.getFilesForStreaming({
      page: 1,
      pageSize: 500,
      playlistId: playlistId.value,
      isVideo: false,
    });
    if (!result.items.length) {
      toast.add({ title: "Playlist is empty", color: "warning" });
      return;
    }
    const idx = result.items.findIndex((f) => f.transpilationJobId === transpilationJobId);
    store.playNow(result.items.slice(idx === -1 ? 0 : idx));
  } catch {
    toast.add({ title: "Failed to load playlist", color: "error" });
  } finally {
    isLoadingQueue.value = false;
  }
};

const onDragStart = (id: string) => {
  draggedItemId.value = id;
};

const onDragEnd = () => {
  draggedItemId.value = null;
};

const onDragOver = (targetId: string) => {
  if (!draggedItemId.value || draggedItemId.value === targetId) return;

  const items = [...localItems.value];
  const fromIndex = items.findIndex((i) => i.id === draggedItemId.value);
  const toIndex = items.findIndex((i) => i.id === targetId);
  if (fromIndex === -1 || toIndex === -1) return;

  const [moved] = items.splice(fromIndex, 1);
  items.splice(toIndex, 0, moved);
  localItems.value = items;
};

const onDrop = async (_targetId: string) => {
  draggedItemId.value = null;
  await reorder({
    playlistId: playlistId.value,
    req: { orderedItemIds: localItems.value.map((i) => i.id) },
  });
};

const handleAddItem = async (transpilationJobId: string) => {
  addingIds.value = new Set([...addingIds.value, transpilationJobId]);
  try {
    await addItem({ playlistId: playlistId.value, req: { transpilationJobId } });
  } finally {
    addingIds.value = new Set([...addingIds.value].filter((id) => id !== transpilationJobId));
  }
};

const confirmRemoveItem = (itemId: string) => {
  removeItemTarget.value = itemId;
  showRemoveItemModal.value = true;
};

const handleUpdate = async (payload: UpdatePlaylistSchema) => {
  await update({
    id: playlistId.value,
    req: {
      name: payload.name,
      description: payload.description,
      hasCover: Boolean(payload.coverFile),
    },
  });
  if (!updateState.value.error) showEditModal.value = false;
};

const handleDelete = async () => {
  await remove(playlistId.value);
  if (!deleteState.value.error) router.push("/streaming/playlists");
};

const handleRemoveItem = async () => {
  if (!removeItemTarget.value) return;
  await removeItem({ playlistId: playlistId.value, itemId: removeItemTarget.value });
  if (!removeItemState.value.error) {
    showRemoveItemModal.value = false;
    removeItemTarget.value = null;
  }
};
</script>
