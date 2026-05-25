<template>
  <div class="px-6 py-5">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-xl font-semibold text-highlighted">Playlists</h1>
        <p class="text-sm text-muted mt-0.5">Organize your streaming library</p>
      </div>
      <UButton
        icon="i-heroicons-plus"
        label="New Playlist"
        color="primary"
        variant="solid"
        @click="showCreateModal = true"
      />
    </div>

    <div v-if="playlistsQuery.status.value === 'pending'" class="flex justify-center py-16">
      <UIcon name="mdi:loading" class="w-6 h-6 animate-spin text-muted" />
    </div>

    <UAlert
      v-else-if="playlistsQuery.status.value === 'error'"
      icon="mdi:alert-circle-outline"
      color="error"
      variant="subtle"
      title="Failed to load playlists"
      description="Please try refreshing the page."
      class="mb-4"
    />

    <div
      v-else-if="!playlists.length"
      class="flex flex-col items-center justify-center py-20 text-center"
    >
      <UIcon name="mdi:playlist-music" class="w-12 h-12 text-muted mb-3" />
      <p class="font-medium text-highlighted">No playlists yet</p>
      <p class="text-sm text-muted mt-1">Create your first playlist to get started.</p>
      <UButton
        label="New Playlist"
        icon="i-heroicons-plus"
        color="primary"
        variant="solid"
        class="mt-4"
        @click="showCreateModal = true"
      />
    </div>

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
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

    <div v-if="totalPages > 1" class="flex justify-center mt-8">
      <UPagination v-model:page="page" :total="total" :page-count="pageSize" />
    </div>
  </div>

  <UModal v-model:open="showCreateModal" title="New Playlist">
    <template #body>
      <PlaylistForm
        :loading="isCreateLoading"
        @submit="handleCreate"
        @cancel="showCreateModal = false"
      />
    </template>
  </UModal>

  <UModal v-model:open="showEditModal" title="Edit Playlist">
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

  <UModal v-model:open="showDeleteModal" title="Delete Playlist">
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
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";
import { useRouter } from "vue-router";

import type { CreatePlaylistSchema, UpdatePlaylistSchema } from "@/schemas/playlist";

import { type PlaylistResponse, playlistApi } from "@/api/playlist";
import { streamingApi } from "@/api/streaming";
import PlaylistCard from "@/components/streaming/PlaylistCard.vue";
import PlaylistForm from "@/components/streaming/PlaylistForm.vue";
import { createPlaylist, deletePlaylist, updatePlaylist } from "@/mutations/playlists";
import { PLAYLIST_QUERY_KEYS } from "@/queries/playlist";
import { usePlayerStore } from "@/stores/stream-player";

const router = useRouter();
const toast = useToast();
const store = usePlayerStore();

const page = ref(1);
const pageSize = 18;

const playlistsQuery = useQuery({
  key: () => PLAYLIST_QUERY_KEYS.list(page.value, pageSize),
  placeholderData: (prev) => prev,
  query: () => playlistApi.getAll(page.value, pageSize),
});

const playlists = computed(() => playlistsQuery.data.value?.items ?? []);
const total = computed(() => playlistsQuery.data.value?.totalCount ?? 0);
const totalPages = computed(() => Math.ceil(total.value / pageSize));

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
    });

    if (!result.items.length) {
      toast.add({
        title: "Playlist is empty",
        description: "Add some tracks before playing.",
        color: "warning",
      });
      return;
    }

    store.setQueue(result.items, 0, playlistId);
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

const navigateToPlaylist = (id: string) => {
  router.push(`/streaming/playlists/${id}`);
};

const handleCreate = async (payload: CreatePlaylistSchema) => {
  await create(payload);
  if (!createState.value.error) showCreateModal.value = false;
};

const handleUpdate = async (payload: UpdatePlaylistSchema) => {
  if (!editTarget.value) return;
  await update({ id: editTarget.value.id, req: payload });
  if (!updateState.value.error) {
    showEditModal.value = false;
    editTarget.value = null;
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
