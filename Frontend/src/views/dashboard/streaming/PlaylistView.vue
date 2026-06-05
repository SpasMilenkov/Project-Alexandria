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

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-4">
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
import { useQuery, useQueryCache } from "@pinia/colada";
import { computed, ref, watch } from "vue";
import { useRouter } from "vue-router";

import { fileApi } from "@/api/file";
import { type PlaylistResponse, playlistApi } from "@/api/playlist";
import { streamingApi } from "@/api/streaming";
import PlaylistCard from "@/components/streaming/PlaylistCard.vue";
import PlaylistForm, { type PlaylistFormPayload } from "@/components/streaming/PlaylistForm.vue";
import { createPlaylist, deletePlaylist, updatePlaylist } from "@/mutations/playlists";
import { PLAYLIST_QUERY_KEYS } from "@/queries/playlist";
import { usePlayerStore } from "@/stores/stream-player";

const router = useRouter();
const toast = useToast();
const store = usePlayerStore();
const queryCache = useQueryCache();

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
    await update({
      id,
      req: {
        name: payload.name,
        description: payload.description,
        hasCover: Boolean(payload.coverFile),
      },
    });

    if (payload.coverFile) {
      const { uploadUrl } = await playlistApi.getCoverUploadUrl({
        playlistId: id,
        mimeType: payload.coverFile.type,
        fileSize: payload.coverFile.size,
      });

      await fileApi.uploadToS3(uploadUrl, payload.coverFile);

      if (payload.ambientTheme) {
        await playlistApi.update(id, { ambientTheme: payload.ambientTheme });
      }
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
