<template>
  <DetailsDrawerShell
    v-model:open="isOpen"
    :title="displayDirectory?.name ?? ''"
    title-label="Folder name"
    :description="drawerDescription"
  >
    <template #icon>
      <Icon icon="mdi:folder" class="h-6 w-6" />
    </template>

    <template #meta>
      <span>Folder</span>

      <template v-if="displayDirectory?.isStarred">
        <span aria-hidden="true">·</span>
        <span class="flex items-center gap-1">
          <Icon icon="mdi:star" class="h-3.5 w-3.5" aria-hidden="true" />
          Favorite
        </span>
      </template>
    </template>

    <template #actions>
      <UTooltip v-if="displayDirectory" :text="favoriteLabel">
        <UButton
          :icon="displayDirectory.isStarred ? 'i-mdi-star' : 'i-mdi-star-outline'"
          :loading="favoritePending"
          :aria-label="favoriteLabel"
          :aria-pressed="displayDirectory.isStarred"
          color="neutral"
          variant="ghost"
          square
          class="justify-center"
          @click="emit('favorite', displayDirectory)"
        />
      </UTooltip>
    </template>

    <template #toolbar>
      <div class="flex items-center gap-2">
        <UButton
          icon="i-mdi-pencil-outline"
          label="Rename"
          size="sm"
          color="neutral"
          variant="outline"
          @click="handleRename"
        />

        <UButton
          icon="i-mdi-folder-move-outline"
          label="Move"
          size="sm"
          color="neutral"
          variant="outline"
          @click="handleMove"
        />

        <UButton
          icon="i-mdi-download-outline"
          label="Download"
          size="sm"
          color="neutral"
          variant="outline"
          @click="handleDownload"
        />

        <span
          aria-hidden="true"
          class="ms-auto h-5 w-px shrink-0 bg-gray-200/70 dark:bg-gray-700/70"
        />

        <UTooltip text="Delete folder">
          <UButton
            icon="i-mdi-delete-outline"
            size="sm"
            color="error"
            variant="ghost"
            square
            class="justify-center"
            aria-label="Delete folder"
            @click="handleDelete"
          />
        </UTooltip>
      </div>
    </template>

    <template v-if="displayDirectory">
      <DetailsSection title="Details">
        <div class="grid grid-cols-2 gap-2">
          <DetailsTile label="Created" :hint="formatTimeAgo(displayDirectory.createdAt)">
            {{ formatCalendarDate(displayDirectory.createdAt) }}
          </DetailsTile>

          <DetailsTile label="Modified" :hint="formatTimeAgo(modifiedAt)">
            {{ formatCalendarDate(modifiedAt) }}
          </DetailsTile>

          <DetailsTile label="Folder ID" :copy-value="displayDirectory.id" class="col-span-2">
            <span class="block truncate font-mono text-xs font-normal">
              {{ displayDirectory.id }}
            </span>
          </DetailsTile>
        </div>
      </DetailsSection>

      <DetailsSection title="Owner">
        <DetailsOwner
          :name="displayDirectory.ownerUserDto.name"
          :email="displayDirectory.ownerUserDto.email"
        />
      </DetailsSection>

      <PolicySection :key="displayDirectory.id" :directory-id="displayDirectory.id" />

      <EntityHistorySection
        :entity-type="EntityType.Directory"
        :entity-id="displayDirectory.id"
        :enabled="isOpen"
      />
    </template>

    <template #footer>
      <UButton
        icon="i-mdi-folder-open-outline"
        label="Open folder"
        color="primary"
        block
        @click="handleOpenDirectory"
      />
    </template>
  </DetailsDrawerShell>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed, ref, watch } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";

import DetailsDrawerShell from "@/components/common/details/DetailsDrawerShell.vue";
import DetailsOwner from "@/components/common/details/DetailsOwner.vue";
import DetailsSection from "@/components/common/details/DetailsSection.vue";
import DetailsTile from "@/components/common/details/DetailsTile.vue";
import EntityHistorySection from "@/components/dashboard/audit/EntityHistorySection.vue";
import PolicySection from "@/components/policy/PolicySection.vue";
import { EntityType } from "@/enums/entity-type";
import { formatCalendarDate, formatTimeAgo } from "@/utils/date-formatters";

const { directory, favoritePending = false } = defineProps<{
  directory: DirectorySummaryDto | null;
  favoritePending?: boolean;
}>();

const emit = defineEmits<{
  "update:directory": [directory: DirectorySummaryDto | null];
  navigate: [directoryId: string, dirName: string];
  rename: [directoryId: string];
  move: [directoryIds: string];
  download: [directoryIds: string[]];
  delete: [directoryIds: string[]];
  favorite: [directory: DirectorySummaryDto];
}>();

// Keeps the last directory rendered during the close transition, so the drawer
// doesn't blank out mid-animation when the parent nulls the active directory.
const displayDirectory = ref<DirectorySummaryDto | null>(null);

watch(
  () => directory,
  (dir) => {
    if (dir) displayDirectory.value = dir;
  },
  { immediate: true },
);

const isOpen = computed({
  get: () => directory !== null,
  set: (val: boolean) => {
    if (!val) emit("update:directory", null);
  },
});

const drawerDescription = computed(() => {
  if (!displayDirectory.value) return undefined;
  return `Created ${formatTimeAgo(displayDirectory.value.createdAt)}`;
});

const modifiedAt = computed(() => {
  if (!displayDirectory.value) return "";
  return displayDirectory.value.updatedAt ?? displayDirectory.value.createdAt;
});

const favoriteLabel = computed(() => {
  if (displayDirectory.value?.isStarred) return "Remove from favorites";
  return "Add to favorites";
});

const closeAndEmit = (action: (dir: DirectorySummaryDto) => void) => {
  const dir = displayDirectory.value;
  isOpen.value = false;
  if (dir) action(dir);
};

const handleOpenDirectory = () => closeAndEmit((dir) => emit("navigate", dir.id, dir.name));
const handleRename = () => closeAndEmit((dir) => emit("rename", dir.id));
const handleMove = () => closeAndEmit((dir) => emit("move", dir.id));
const handleDownload = () => closeAndEmit((dir) => emit("download", [dir.id]));
const handleDelete = () => closeAndEmit((dir) => emit("delete", [dir.id]));
</script>
