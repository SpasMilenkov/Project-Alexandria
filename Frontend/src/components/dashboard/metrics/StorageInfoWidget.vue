<template>
  <div class="@container">
    <div class="hidden @[80px]:hidden @[0px]:flex flex-col items-center gap-1 py-2">
      <div class="relative w-8 h-8">
        <svg viewBox="0 0 32 32" class="w-8 h-8 -rotate-90">
          <circle
            cx="16"
            cy="16"
            r="12"
            fill="none"
            stroke="currentColor"
            stroke-width="3"
            class="opacity-10"
          />
          <circle
            v-if="data"
            cx="16"
            cy="16"
            r="12"
            fill="none"
            stroke="currentColor"
            stroke-width="3"
            stroke-linecap="round"
            class="text-primary transition-all duration-300"
            :stroke-dasharray="`${2 * Math.PI * 12}`"
            :stroke-dashoffset="`${2 * Math.PI * 12 * (1 - data.dataUsagePercentage / 100)}`"
          />
          
        </svg>
        <UIcon name="mdi:harddisk" class="absolute inset-0 m-auto w-3.5 h-3.5 opacity-60" />
      </div>
      <span v-if="data" class="text-[10px] font-semibold opacity-50 tabular-nums">
        {{ Math.round(data.dataUsagePercentage) }}%
      </span>
    </div>

    <div
      class="hidden @[80px]:block border border-gray-300/70 dark:border-gray-700/70 rounded-lg overflow-hidden bg-neutral/60 dark:bg-neutral/5 backdrop-blur-sm"
    >
      <button
        @click="isExpanded = !isExpanded"
        class="w-full p-3 flex flex-col gap-2 hover:bg-black/4 dark:hover:bg-white/5 transition-colors"
      >
        <div class="flex items-center justify-between w-full">
          <div class="flex items-center gap-2">
            <UIcon name="mdi:harddisk" class="w-5 h-5" />
            <span class="font-medium">Storage</span>
          </div>
          <div class="flex items-center gap-3">
            <div v-if="!isLoading && data" class="text-sm font-semibold">
              {{ Math.round(data.dataUsagePercentage) }}%
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
              <div class="h-1.5 bg-black/10 dark:bg-white/10 rounded-full overflow-hidden">
                <div
                  class="h-full bg-primary transition-all duration-300 rounded-full"
                  :style="{ width: `${data.dataUsagePercentage}%` }"
                />
              </div>
              <div class="flex justify-between text-xs opacity-50">
                <span>{{ data.dataAvailableGB.toFixed(2) }} GB free</span>
                <span>{{ data.dataTotalGB.toFixed(2) }} GB</span>
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
                <UIcon name="mdi:loading" class="w-6 h-6 animate-spin opacity-50" />
              </div>
              <div v-else-if="error" class="text-sm opacity-70">
                Failed to load storage information
              </div>
              <div v-else-if="data" class="space-y-2">
                <div class="h-2 bg-black/10 dark:bg-white/10 rounded-full overflow-hidden">
                  <div
                    class="h-full bg-primary transition-all duration-300 rounded-full"
                    :style="{ width: `${data.dataUsagePercentage}%` }"
                  />
                </div>
                <div class="flex justify-between text-sm opacity-70">
                  <span>{{ data.dataAvailableGB.toFixed(2) }} GB available</span>
                  <span>{{ data.dataTotalGB.toFixed(2) }} GB total</span>
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
                label="Manage Storage"
                icon="mdi:cog"
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { storageInfo } from "@/queries/status";
import { useQuery } from "@pinia/colada";
import { ref } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();
const { defaultState } = defineProps<{ defaultState?: boolean }>();
const { data, isLoading, error } = useQuery(storageInfo);

const isExpanded = ref(defaultState ?? false);
</script>
