<template>
  <div class="@container">
    <!-- Icon-only ring (unchanged) -->
    <div class="hidden @[80px]:hidden @[0px]:flex flex-col items-center gap-1 py-2">
      <!-- ...ring markup unchanged... -->
    </div>

    <div
      class="hidden @[80px]:block border border-gray-300/70 dark:border-gray-700/70 rounded-lg overflow-hidden bg-neutral/60 dark:bg-neutral/5 frosted-glass"
    >
      <button
        @click="isExpanded = !isExpanded"
        class="w-full p-2 flex flex-col gap-2 hover:bg-black/4 dark:hover:bg-white/5 transition-colors"
      >
        <div class="flex items-center justify-between w-full min-w-0">
          <div class="flex items-center gap-1 min-w-0">
            <UIcon name="mdi:harddisk" class="w-5 h-5 shrink-0" />
            <!-- Drop the label first, it's the least useful word on the card -->
            <span class="font-medium truncate hidden @[150px]:inline">Storage</span>
          </div>
          <div class="flex items-center gap-2 shrink-0">
            <div v-if="!isLoading && data" class="text-sm font-semibold">
              <span v-if="isUnlimited">∞</span>
              <span v-else>{{ Math.round(usagePercent) }}%</span>
            </div>
            <UIcon
              name="mdi:chevron-down"
              class="w-5 h-5 transition-transform duration-200"
              :class="{ 'rotate-180': isExpanded }"
            />
          </div>
        </div>

        <div
          class="grid transition-[grid-template-rows,opacity] duration-150 ease-in-out w-full"
          :class="isExpanded ? 'grid-rows-[0fr] opacity-0' : 'grid-rows-[1fr] opacity-100'"
        >
          <div class="overflow-hidden">
            <div v-if="!isLoading && data" class="w-full space-y-1">
              <UProgressGroup :max="progressMax" :items="barItems" size="xs" />
              <!-- Below 180px: bar only, no text at all. This is the fix for "clumped" -->
              <div
                class="hidden @[180px]:flex flex-wrap gap-x-2 gap-y-0.5 justify-between text-xs text-muted"
              >
                <span>{{ formatBytes(usedBytes) }} of {{ quotaLabel }} used</span>
                <span v-if="!isUnlimited">{{ remainingLabel }}</span>
              </div>
            </div>
            <div
              v-else-if="isLoading"
              class="w-full h-1.5 bg-black/10 dark:bg-white/10 rounded-full animate-pulse"
            />
          </div>
        </div>
      </button>

      <div
        class="grid transition-[grid-template-rows] duration-200 ease-[cubic-bezier(0.4,0,0.2,1)]"
        :class="isExpanded ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]'"
      >
        <div class="overflow-hidden">
          <div class="border-t border-gray-200/70 dark:border-gray-700/70">
            <div class="px-3 py-4">
              <div v-if="isLoading" class="flex items-center justify-center py-8">
                <UIcon name="mdi:loading" class="w-6 h-6 animate-spin text-muted" />
              </div>
              <div v-else-if="visibleError" class="text-sm text-muted">
                Failed to load storage information
              </div>
              <div v-else-if="data" class="space-y-2">
                <UProgressGroup
                  :max="progressMax"
                  :items="legendItems"
                  status
                  :ui="{ status: 'w-full justify-between' }"
                >
                  <template #status>
                    <p class="font-medium">{{ formatBytes(usedBytes) }} of {{ quotaLabel }} used</p>
                  </template>
                  <template #item-trailing="{ item }">
                    {{ formatBytes(item.value ?? 0) }}
                  </template>
                </UProgressGroup>
                <!-- Hide breakdown counts below 220px, per your ask -->
                <div class="hidden @[220px]:flex justify-between text-sm text-muted">
                  <span>{{ previewsCountLabel }}</span>
                  <span>{{ transcodedCountLabel }}</span>
                </div>
              </div>
            </div>
            <div v-if="data" class="border-t border-gray-200/70 dark:border-gray-700/70 p-2">
              <UButton
                variant="ghost"
                size="sm"
                class="w-full"
                color="neutral"
                @click="router.push('/my-storage')"
                icon="mdi:cog"
              >
                <span class="hidden @[150px]:inline">Manage Storage</span>
              </UButton>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ProgressGroupItem } from "@nuxt/ui";

import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";
import { useRouter } from "vue-router";

import { myStorage } from "@/queries/status";
import { formatBytes } from "@/utils/size.utils";

const router = useRouter();
const { defaultState } = defineProps<{ defaultState?: boolean }>();
const { data, isLoading, error } = useQuery(myStorage);

const isExpanded = ref(defaultState ?? false);

const isAuthError = (err: unknown): boolean => {
  if (!err) return false;
  const e = err as Record<string, unknown>;
  const status = (e.status ?? e.statusCode) as number | undefined;
  if (status === 401 || status === 403) return true;
  const msg = String(e.message ?? "").toLowerCase();
  return msg.includes("unauthorized") || msg.includes("401") || msg.includes("forbidden");
};

const visibleError = computed((): Error | null => {
  if (!error.value || isAuthError(error.value)) return null;
  return error.value;
});

const quotaBytes = computed(() => data.value?.quotaBytes ?? 0);
const usedBytes = computed(() => data.value?.usedBytes ?? 0);
const isUnlimited = computed(() => quotaBytes.value <= 0);
const remainingBytes = computed(() => quotaBytes.value - usedBytes.value);
const isOverQuota = computed(() => !isUnlimited.value && remainingBytes.value < 0);

const usagePercent = computed(() => {
  if (quotaBytes.value <= 0) return 0;
  return Math.min(100, (usedBytes.value / quotaBytes.value) * 100);
});

const progressMax = computed(() => {
  if (quotaBytes.value > 0) return quotaBytes.value;
  if (usedBytes.value > 0) return usedBytes.value;
  return 1;
});

const quotaLabel = computed(() => {
  if (isUnlimited.value) return "unlimited";
  return formatBytes(quotaBytes.value);
});

const remainingLabel = computed(() => {
  if (isUnlimited.value) return "";
  if (isOverQuota.value) return `${formatBytes(-remainingBytes.value)} over quota`;
  return `${formatBytes(remainingBytes.value)} remaining`;
});

const barItems = computed<ProgressGroupItem[]>(() => {
  if (!data.value) return [];
  return [
    { color: "primary", value: data.value.filesSize },
    { color: "info", value: data.value.previewsSize },
    { color: "warning", value: data.value.transcodedSize },
  ];
});

const legendItems = computed<ProgressGroupItem[]>(() => {
  if (!data.value) return [];
  return [
    { color: "primary", label: "Files", value: data.value.filesSize },
    { color: "info", label: "Previews", value: data.value.previewsSize },
    { color: "warning", label: "Transcoded", value: data.value.transcodedSize },
  ];
});

const previewsCountLabel = computed(() => {
  const count = data.value?.previewsCount ?? 0;
  if (count === 1) return "1 preview";
  return `${count} previews`;
});

const transcodedCountLabel = computed(() => {
  const count = data.value?.representationsCount ?? 0;
  if (count === 1) return "1 representation";
  return `${count} representations`;
});
</script>
