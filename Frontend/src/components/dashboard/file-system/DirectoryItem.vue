<template>
  <UDrawer
    :title="data.name"
    :description="'Created ' + formatDate(data.createdAt)"
    direction="right"
    v-model:open="openDrawer"
    :ui="{ container: 'md:max-w-[40rem]' }"
  >
    <!-- Grid View -->
    <UContextMenu v-if="viewMode === 'grid'" :items="contextMenuItems">
      <div class="relative group" tabindex="0" @contextmenu="handleClick">
        <button
          type="button"
          class="w-full flex flex-col items-center gap-2 p-4 rounded-lg transition-colors cursor-pointer"
          :class="[
            isSelected
              ? 'bg-primary/20 ring-2 ring-primary'
              : 'hover:bg-primary/40 dark:hover:bg-primary/35',
          ]"
          @click="handleClick"
          @dblclick="handleDoubleClick"
          @contextmenu="emit('contextmenu', $event)"
        >
          <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
          <span class="text-sm text-center line-clamp-2 w-full wrap-break-word font-medium">
            {{ data.name }}
          </span>
        </button>
      </div>
    </UContextMenu>

    <!-- List View -->
    <UContextMenu v-else :items="contextMenuItems">
      <div class="relative group" tabindex="0">
        <button
          type="button"
          class="w-full flex items-center gap-3 px-4 py-2 transition-colors cursor-pointer text-left border-b last:border-b-0"
          :class="[
            isSelected
              ? 'bg-primary/20 ring-2 ring-primary'
              : 'hover:bg-primary/40 dark:hover:bg-primary/35',
          ]"
          @click="handleClick"
          @dblclick="handleDoubleClick"
          @contextmenu="emit('contextmenu', $event)"
        >
          <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
          <span class="flex-1 truncate font-medium">{{ data.name }}</span>
          <Icon icon="mdi:chevron-right" class="w-4 h-4 shrink-0" />
        </button>
      </div>
    </UContextMenu>

    <template #body>
      <div class="flex flex-col gap-6 p-1">
        <!-- Folder Preview/Icon Section -->
        <div class="flex items-center gap-4 p-6 bg-gray-50 dark:bg-gray-900/50 rounded-lg">
          <div class="p-4 bg-white dark:bg-gray-800 rounded-lg shadow-sm">
            <Icon icon="mdi:folder" class="w-16 h-16 text-primary" />
          </div>
          <div class="flex-1 min-w-0">
            <h3 class="font-semibold text-lg truncate mb-1">
              {{ data.name }}
            </h3>
            <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
              <Icon icon="mdi:folder-open" class="w-4 h-4" />
              <span>Directory</span>
            </div>
          </div>
        </div>

        <!-- Directory Details Grid -->
        <div>
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">Details</h4>
          <div class="grid grid-cols-2 gap-4">
            <div class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg">
              <Icon icon="mdi:clock-outline" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Created</div>
                <div class="font-medium">
                  {{ formatDate(data.createdAt) }}
                </div>
              </div>
            </div>

            <div class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg">
              <Icon icon="mdi:update" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Modified</div>
                <div class="font-medium">
                  {{ formatDate(data.updatedAt) }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg col-span-2"
            >
              <Icon icon="mdi:identifier" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div class="min-w-0 flex-1">
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Directory ID</div>
                <div class="font-mono text-sm truncate">{{ data.id }}</div>
              </div>
            </div>

            <div
              v-if="data.parentId"
              class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg col-span-2"
            >
              <Icon icon="mdi:folder-arrow-up" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div class="min-w-0 flex-1">
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">
                  Parent Directory ID
                </div>
                <div class="font-mono text-sm truncate">
                  {{ data.parentId }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Owner Section -->
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <Icon icon="mdi:account" class="w-8 h-8 text-primary" />
              <span class="font-semibold">Owner</span>
            </div>
          </template>

          <div class="flex items-center gap-3">
            <UAvatar :alt="data.ownerUserDto.name" size="lg" />
            <div>
              <div class="font-medium">{{ data.ownerUserDto.name }}</div>
              <div
                class="text-sm text-gray-600 dark:text-gray-400 flex items-center gap-1.5 mt-0.5"
              >
                <Icon icon="mdi:email" class="w-4 h-4" />
                {{ data.ownerUserDto.email }}
              </div>
            </div>
          </div>
        </UCard>

        <!-- Quick Actions -->
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <Icon icon="mdi:lightning-bolt" class="w-8 h-8 text-primary" />
              <span class="font-semibold">Quick Actions</span>
            </div>
          </template>

          <div class="grid grid-cols-2 gap-2">
            <UButton
              icon="i-mdi-folder-open"
              color="primary"
              variant="soft"
              block
              @click="handleOpenDirectory"
            >
              Open
            </UButton>
            <UButton icon="i-mdi-pencil" color="neutral" variant="soft" block @click="handleRename">
              Rename
            </UButton>
            <UButton
              icon="i-mdi-folder-move"
              color="neutral"
              variant="soft"
              block
              @click="handleMove"
            >
              Move
            </UButton>
            <UButton
              icon="i-mdi-download"
              color="neutral"
              variant="soft"
              block
              @click="handleDownload"
            >
              Download
            </UButton>
          </div>
        </UCard>
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import type { DirectorySummaryDto } from "@/api/directory";
import { Icon } from "@iconify/vue";
import type { ContextMenuItem } from "@nuxt/ui";
import { computed, ref } from "vue";

import { useSettingsStore } from "@/stores/settings";
import { formatDate } from "@/utils/date-formatters";

const settingsStore = useSettingsStore();

const iconSize = computed(() =>
  props.viewMode === "grid" ? settingsStore.gridIconSize : settingsStore.listIconSize,
);

defineExpose({
  openDetails: () => {
    openDrawer.value = true;
  },
});

const props = defineProps<{
  data: DirectorySummaryDto;
  viewMode: "grid" | "list";
  isSelected: boolean;
  selectedCount?: number;
}>();

const emit = defineEmits<{
  navigate: [directoryId: string, dirName: string];
  click: [event: MouseEvent];
  open: [directoryId: string];
  rename: [directoryId: string];
  delete: [directoryIds: string[]];
  move: [directoryIds: string];
  copy: [directoryIds: string[]];
  download: [directoryIds: string[]];
  contextmenu: [event: PointerEvent];
}>();

const openDrawer = ref(false);

const contextMenuItems = computed(() => {
  const isMultiSelect = (props.selectedCount ?? 0) > 1;
  const count = props.selectedCount ?? 1;

  if (!isMultiSelect) {
    return [
      [
        {
          icon: "i-mdi-folder-open",
          label: "Open",
          onSelect: () => emit("open", props.data.id),
        },
      ],
      [
        {
          disabled: !canRename(),
          icon: "i-mdi-pencil-outline",
          label: "Rename",
          onSelect: () => emit("rename", props.data.id),
        },
      ],
      [
        {
          disabled: !canMove(),
          icon: "i-mdi-folder-move-outline",
          label: "Move to…",
          onSelect: () => emit("move", props.data.id),
        },
        {
          disabled: !canCopy(),
          icon: "i-mdi-content-copy",
          label: "Copy to…",
          onSelect: () => emit("copy", [props.data.id]),
        },
        {
          disabled: !canDownload(),
          icon: "i-mdi-download-outline",
          label: "Download",
          onSelect: () => emit("download", [props.data.id]),
        },
      ],
      [
        {
          color: "error" as const,
          disabled: !canDelete(),
          icon: "i-mdi-delete-outline",
          label: "Delete",
          onSelect: () => emit("delete", [props.data.id]),
        },
      ],
    ];
  }

  return [
    [
      {
        disabled: !canMove(),
        icon: "i-mdi-folder-move-outline",
        label: `Move ${count} items to…`,
        onSelect: () => emit("move", props.data.id),
      },
      {
        disabled: !canCopy(),
        icon: "i-mdi-content-copy",
        label: `Copy ${count} items to…`,
        onSelect: () => emit("copy", []),
      },
      {
        disabled: !canDownload(),
        icon: "i-mdi-download-multiple-outline",
        label: `Download ${count} items`,
        onSelect: () => emit("download", []),
      },
    ],
    [
      {
        color: "error" as const,
        disabled: !canDelete(),
        icon: "i-mdi-delete-sweep-outline",
        label: `Delete ${count} items`,
        onSelect: () => emit("delete", []),
      },
    ],
  ];
});

// Permission check stubs
const canRename = (): boolean => true;

const canMove = (): boolean => true;

const canCopy = (): boolean => true;

const canDownload = (): boolean => true;

const canDelete = (): boolean => true;

const handleClick = (event: MouseEvent) => {
  if (!props.isSelected && event.button === 2) {
    emit("click", event);
    return;
  }
  emit("click", event);
};

const handleDoubleClick = () => {
  openDrawer.value = false;
  emit("navigate", props.data.id, props.data.name);
};

// Quick action handlers
const handleOpenDirectory = () => {
  openDrawer.value = false;
  emit("navigate", props.data.id, props.data.name);
};

const handleRename = () => {
  openDrawer.value = false;
  emit("rename", props.data.id);
};

const handleMove = () => {
  openDrawer.value = false;
  emit("move", props.data.id);
};

const handleDownload = () => {
  openDrawer.value = false;
  emit("download", [props.data.id]);
};
</script>

<style scoped></style>
