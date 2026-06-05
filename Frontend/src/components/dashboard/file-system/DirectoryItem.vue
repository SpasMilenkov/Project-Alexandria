<template>
  <UDrawer
    :title="data.name"
    :description="'Created ' + formatDate(data.createdAt)"
    :direction="isMobile ? 'bottom' : 'right'"
    v-model:open="openDrawer"
    :ui="
      isMobile
        ? { container: 'h-[85vh] rounded-t-2xl' }
        : { container: 'md:max-w-[40rem] lg:min-w-[40rem]' }
    "
    :handle-only="!isMobile"
  >
    <!-- Grid View -->
    <UContextMenu
      v-if="viewMode === 'grid'"
      :items="contextMenuItems"
      :ui="{ content: 'lg:min-w-56' }"
    >
      <div class="relative group" tabindex="0">
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
    <UContextMenu v-else :items="contextMenuItems" :ui="{ content: 'lg:min-w-56' }">
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
        <div
          class="flex items-center bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
          :class="isMobile ? 'gap-3 p-3' : 'gap-4 p-6'"
        >
          <div
            class="bg-white dark:bg-neutral-800 rounded-lg shadow-sm shrink-0"
            :class="isMobile ? 'p-2' : 'p-4'"
          >
            <Icon
              icon="mdi:folder"
              class="text-primary"
              :class="isMobile ? 'w-10 h-10' : 'w-16 h-16'"
            />
          </div>
          <div class="flex flex-col min-w-0 flex-1">
            <div
              class="flex items-center gap-1 group mb-1 min-w-0"
              :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
              @click="isMobile && copyWithFeedback(data.name, 'Directory name')"
            >
              <h3 class="font-semibold truncate" :class="isMobile ? 'text-base' : 'text-lg'">
                {{ data.name }}
              </h3>
              <UButton
                icon="i-mdi-content-copy"
                size="xs"
                variant="ghost"
                color="neutral"
                :class="
                  isMobile
                    ? 'shrink-0 opacity-50'
                    : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity'
                "
                aria-label="Copy directory name"
                @click.stop="copyWithFeedback(data.name, 'Directory name')"
              />
            </div>
            <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
              <Icon icon="mdi:folder-open" class="w-4 h-4 shrink-0" />
              <span>Directory</span>
            </div>
          </div>
        </div>

        <!-- Directory Details Grid -->
        <div class="flex flex-col gap-4">
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">Details</h4>
          <div class="grid gap-4" :class="isMobile ? 'grid-cols-1' : 'grid-cols-2'">
            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <Icon icon="mdi:clock-outline" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Created</div>
                <div class="font-medium text-sm">
                  {{ formatDate(data.createdAt) }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <Icon icon="mdi:update" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Modified</div>
                <div class="font-medium text-sm">
                  {{ formatDate(data.updatedAt ?? data.createdAt) }}
                </div>
              </div>
            </div>

            <div
              v-if="data.parentId"
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
              :class="isMobile ? '' : 'col-span-2'"
            >
              <Icon icon="mdi:folder-arrow-up" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
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

        <!-- Directory ID -->
        <div
          class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
          :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
          @click="isMobile && copyWithFeedback(data.id, 'Directory ID')"
        >
          <Icon icon="mdi:identifier" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
          <div class="min-w-0 flex-1">
            <div class="text-xs mb-0.5 text-gray-500 dark:text-gray-400">Directory ID</div>
            <div class="flex items-center gap-1 group min-w-0">
              <div class="font-mono text-sm truncate">{{ data.id }}</div>
              <UButton
                icon="i-mdi-content-copy"
                size="xs"
                variant="ghost"
                color="neutral"
                :class="
                  isMobile
                    ? 'shrink-0 opacity-50 ml-auto'
                    : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity'
                "
                aria-label="Copy directory ID"
                @click.stop="copyWithFeedback(data.id, 'Directory ID')"
              />
            </div>
          </div>
        </div>

        <!-- Owner Section -->
        <UCard :ui="isMobile ? { body: 'p-3' } : {}">
          <template #header>
            <div class="flex items-center gap-2" :class="isMobile ? 'p-3 pb-0' : ''">
              <Icon icon="mdi:account-outline" class="w-5 h-5 text-gray-500 dark:text-gray-400" />
              <span class="font-semibold text-sm">Owner</span>
            </div>
          </template>
          <div class="flex items-center gap-3">
            <UAvatar :alt="data.ownerUserDto.name" :size="isMobile ? 'md' : 'lg'" />
            <div class="min-w-0 flex-1">
              <div class="font-medium text-sm">{{ data.ownerUserDto.name }}</div>
              <div
                class="text-sm flex items-center gap-1.5 mt-0.5 text-gray-600 dark:text-gray-400 group"
                :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
                @click="isMobile && copyWithFeedback(data.ownerUserDto.email, 'Email')"
              >
                <Icon icon="mdi:email-outline" class="w-4 h-4 shrink-0" />
                <span class="truncate">{{ data.ownerUserDto.email }}</span>
                <UButton
                  icon="i-mdi-content-copy"
                  size="xs"
                  variant="ghost"
                  color="neutral"
                  :class="
                    isMobile
                      ? 'shrink-0 opacity-50 ml-auto'
                      : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity ml-auto'
                  "
                  aria-label="Copy owner email"
                  @click.stop="copyWithFeedback(data.ownerUserDto.email, 'Email')"
                />
              </div>
            </div>
          </div>
        </UCard>

        <!-- Quick Actions -->
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <Icon
                icon="mdi:lightning-bolt-outline"
                class="w-5 h-5 text-gray-500 dark:text-gray-400"
              />
              <span class="font-semibold text-sm">Quick Actions</span>
            </div>
          </template>

          <div class="flex flex-col gap-3">
            <!-- Primary action -->
            <UButton
              icon="i-mdi-folder-open"
              color="primary"
              variant="solid"
              block
              @click="handleOpenDirectory"
            >
              Open Directory
            </UButton>

            <!-- Secondary action tiles -->
            <div class="grid grid-cols-3 gap-2">
              <button
                type="button"
                class="flex flex-col items-center gap-1.5 p-3 rounded-lg bg-neutral-100 dark:bg-neutral-800/50 hover:bg-neutral-200 dark:hover:bg-neutral-700/50 transition-colors group"
                @click="handleRename"
              >
                <Icon
                  icon="mdi:pencil-outline"
                  class="w-5 h-5 text-gray-600 dark:text-gray-400 group-hover:text-gray-900 dark:group-hover:text-gray-100 transition-colors"
                />
                <span
                  class="text-xs font-medium text-gray-600 dark:text-gray-400 group-hover:text-gray-900 dark:group-hover:text-gray-100 transition-colors"
                >
                  Rename
                </span>
              </button>

              <button
                type="button"
                class="flex flex-col items-center gap-1.5 p-3 rounded-lg bg-neutral-100 dark:bg-neutral-800/50 hover:bg-neutral-200 dark:hover:bg-neutral-700/50 transition-colors group"
                @click="handleMove"
              >
                <Icon
                  icon="mdi:folder-move-outline"
                  class="w-5 h-5 text-gray-600 dark:text-gray-400 group-hover:text-gray-900 dark:group-hover:text-gray-100 transition-colors"
                />
                <span
                  class="text-xs font-medium text-gray-600 dark:text-gray-400 group-hover:text-gray-900 dark:group-hover:text-gray-100 transition-colors"
                >
                  Move
                </span>
              </button>

              <button
                type="button"
                class="flex flex-col items-center gap-1.5 p-3 rounded-lg bg-neutral-100 dark:bg-neutral-800/50 hover:bg-neutral-200 dark:hover:bg-neutral-700/50 transition-colors group"
                @click="handleDownload"
              >
                <Icon
                  icon="mdi:download-outline"
                  class="w-5 h-5 text-gray-600 dark:text-gray-400 group-hover:text-gray-900 dark:group-hover:text-gray-100 transition-colors"
                />
                <span
                  class="text-xs font-medium text-gray-600 dark:text-gray-400 group-hover:text-gray-900 dark:group-hover:text-gray-100 transition-colors"
                >
                  Download
                </span>
              </button>
            </div>

            <!-- Destructive action — separated per skill guidelines -->
            <UButton
              icon="i-mdi-delete-outline"
              color="error"
              variant="outline"
              block
              size="sm"
              @click="handleDelete"
            >
              Delete
            </UButton>
          </div>
        </UCard>
        <PolicySection :directory-id="data.id" />
        
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { breakpointsTailwind, useBreakpoints, useClipboard } from "@vueuse/core";
import { computed, ref } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";

import { useSettingsStore } from "@/stores/settings";
import { formatDate } from "@/utils/date-formatters";
import PolicySection from "@/components/policy/PolicySection.vue";

const settingsStore = useSettingsStore();

const toast = useToast();
const { copy } = useClipboard();

const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller("md");

const copyWithFeedback = async (value: string, label: string) => {
  await copy(value);
  toast.add({
    color: "success",
    duration: 2000,
    icon: "i-mdi-check-circle",
    title: `${label} copied`,
  });
};

const props = defineProps<{
  data: DirectorySummaryDto;
  viewMode: "grid" | "list";
  isSelected: boolean;
  selectedCount?: number;
}>();

defineExpose({
  openDetails: () => {
    openDrawer.value = true;
  },
});

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

const iconSize = computed(() =>
  props.viewMode === "grid" ? settingsStore.gridIconSize : settingsStore.listIconSize,
);

const canRename = (): boolean => true;
const canMove = (): boolean => true;
const canCopy = (): boolean => true;
const canDownload = (): boolean => true;
const canDelete = (): boolean => true;

const openDrawer = ref(false);

const contextMenuItems = computed(() => {
  const isMultiSelect = (props.selectedCount ?? 0) > 1;
  const count = props.selectedCount ?? 1;

  if (!isMultiSelect) {
    return [
      [
        {
          icon: "i-mdi-information-outline",
          label: "View details",
          kbds: [{ value: "alt" }, { value: "enter" }],
          onSelect: () => {
            openDrawer.value = true;
          },
        },
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
          kbds: ["R"],
          onSelect: () => emit("rename", props.data.id),
        },
      ],
      [
        {
          disabled: !canMove(),
          icon: "i-mdi-folder-move-outline",
          label: "Move to…",
          kbds: ["⌘", "X"],
          onSelect: () => emit("move", props.data.id),
        },
        {
          disabled: !canCopy(),
          icon: "i-mdi-content-copy",
          label: "Copy to…",
          kbds: ["⌘", "C"],
          onSelect: () => emit("copy", [props.data.id]),
        },
        {
          disabled: !canDownload(),
          icon: "i-mdi-download-outline",
          label: "Download",
          kbds: ["D"],
          onSelect: () => emit("download", [props.data.id]),
        },
      ],
      [
        {
          color: "error" as const,
          disabled: !canDelete(),
          icon: "i-mdi-delete-outline",
          label: "Delete",
          kbds: ["Del"],
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
        kbds: ["⌘", "X"],
        onSelect: () => emit("move", props.data.id),
      },
      {
        disabled: !canCopy(),
        icon: "i-mdi-content-copy",
        label: `Copy ${count} items to…`,
        kbds: ["⌘", "C"],
        onSelect: () => emit("copy", []),
      },
      {
        disabled: !canDownload(),
        icon: "i-mdi-download-multiple-outline",
        label: `Download ${count} items`,
        kbds: ["D"],
        onSelect: () => emit("download", []),
      },
    ],
    [
      {
        color: "error" as const,
        disabled: !canDelete(),
        icon: "i-mdi-delete-sweep-outline",
        label: `Delete ${count} items`,
        kbds: ["Del"],
        onSelect: () => emit("delete", []),
      },
    ],
  ];
});

defineShortcuts(extractShortcuts(contextMenuItems.value));

// Permission check stubs

const handleClick = (event: MouseEvent) => emit("click", event);

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

const handleDelete = () => {
  openDrawer.value = false;
  emit("delete", [props.data.id]);
};
</script>

<style scoped></style>
