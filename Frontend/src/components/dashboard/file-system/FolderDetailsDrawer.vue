<template>
  <UDrawer
    :title="displayDirectory?.name"
    :description="
      displayDirectory ? 'Created ' + formatDate(displayDirectory.createdAt) : undefined
    "
    :direction="isMobile ? 'bottom' : 'right'"
    v-model:open="isOpen"
    :ui="drawerUi"
    :handle-only="!isMobile"
  >
    <template #body>
      <div v-if="displayDirectory" class="flex flex-col gap-6 p-1">
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
              @click="isMobile && copyWithFeedback(displayDirectory.name, 'Directory name')"
            >
              <h3 class="font-semibold truncate" :class="isMobile ? 'text-base' : 'text-lg'">
                {{ displayDirectory.name }}
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
                @click.stop="copyWithFeedback(displayDirectory.name, 'Directory name')"
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
                  {{ formatDate(displayDirectory.createdAt) }}
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
                  {{ formatDate(displayDirectory.updatedAt ?? displayDirectory.createdAt) }}
                </div>
              </div>
            </div>

            <div
              v-if="displayDirectory.parentId"
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
              :class="isMobile ? '' : 'col-span-2'"
            >
              <Icon icon="mdi:folder-arrow-up" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div class="min-w-0 flex-1">
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">
                  Parent Directory ID
                </div>
                <div class="font-mono text-sm truncate">
                  {{ displayDirectory.parentId }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Directory ID -->
        <div
          class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
          :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
          @click="isMobile && copyWithFeedback(displayDirectory.id, 'Directory ID')"
        >
          <Icon icon="mdi:identifier" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
          <div class="min-w-0 flex-1">
            <div class="text-xs mb-0.5 text-gray-500 dark:text-gray-400">Directory ID</div>
            <div class="flex items-center gap-1 group min-w-0">
              <div class="font-mono text-sm truncate">{{ displayDirectory.id }}</div>
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
                @click.stop="copyWithFeedback(displayDirectory.id, 'Directory ID')"
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
            <UAvatar :alt="displayDirectory.ownerUserDto.name" :size="isMobile ? 'md' : 'lg'" />
            <div class="min-w-0 flex-1">
              <div class="font-medium text-sm">{{ displayDirectory.ownerUserDto.name }}</div>
              <div
                class="text-sm flex items-center gap-1.5 mt-0.5 text-gray-600 dark:text-gray-400 group"
                :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
                @click="isMobile && copyWithFeedback(displayDirectory.ownerUserDto.email, 'Email')"
              >
                <Icon icon="mdi:email-outline" class="w-4 h-4 shrink-0" />
                <span class="truncate">{{ displayDirectory.ownerUserDto.email }}</span>
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
                  @click.stop="copyWithFeedback(displayDirectory.ownerUserDto.email, 'Email')"
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
        <PolicySection
          :key="displayDirectory.id"
          :directory-id="displayDirectory.id"
        />
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { breakpointsTailwind, useBreakpoints, useClipboard } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";

import PolicySection from "@/components/policy/PolicySection.vue";
import { formatDate } from "@/utils/date-formatters";
import { glassDrawerContent } from "@/utils/modalUi";

const toast = useToast();
const { copy } = useClipboard();

const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller("md");

const drawerUi = computed(() => {
  if (isMobile.value) {
    return { content: glassDrawerContent, container: "h-[85vh] rounded-t-2xl" };
  }
  return { content: glassDrawerContent, container: "md:max-w-[40rem] lg:min-w-[40rem]" };
});

const { directory } = defineProps<{
  directory: DirectorySummaryDto | null;
}>();

const emit = defineEmits<{
  "update:directory": [directory: DirectorySummaryDto | null];
  navigate: [directoryId: string, dirName: string];
  rename: [directoryId: string];
  move: [directoryIds: string];
  download: [directoryIds: string[]];
  delete: [directoryIds: string[]];
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

const copyWithFeedback = async (value: string, label: string) => {
  await copy(value);
  toast.add({
    color: "success",
    duration: 2000,
    icon: "i-mdi-check-circle",
    title: `${label} copied`,
  });
};

const handleOpenDirectory = () => {
  const dir = displayDirectory.value;
  isOpen.value = false;
  if (dir) emit("navigate", dir.id, dir.name);
};

const handleRename = () => {
  const dir = displayDirectory.value;
  isOpen.value = false;
  if (dir) emit("rename", dir.id);
};

const handleMove = () => {
  const dir = displayDirectory.value;
  isOpen.value = false;
  if (dir) emit("move", dir.id);
};

const handleDownload = () => {
  const dir = displayDirectory.value;
  isOpen.value = false;
  if (dir) emit("download", [dir.id]);
};

const handleDelete = () => {
  const dir = displayDirectory.value;
  isOpen.value = false;
  if (dir) emit("delete", [dir.id]);
};
</script>

<style scoped></style>
