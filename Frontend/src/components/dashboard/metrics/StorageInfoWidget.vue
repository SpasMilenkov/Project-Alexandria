<template>
  <div
    class="border border-gray-200 dark:border-neutral-800 rounded-lg overflow-hidden"
  >
    <!-- Collapsible Header -->
    <button
      @click="isExpanded = !isExpanded"
      class="w-full p-3 flex items-center justify-between hover:bg-gray-50 dark:hover:bg-neutral-800 transition-colors"
    >
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
    </button>

    <!-- Collapsible Content -->
    <Transition
      enter-active-class="transition-all duration-200 ease-out"
      enter-from-class="max-h-0"
      enter-to-class="max-h-96"
      leave-active-class="transition-all duration-200 ease-in"
      leave-from-class="max-h-96"
      leave-to-class="max-h-0"
    >
      <div v-show="isExpanded" class="overflow-hidden">
        <div class="border-t border-gray-200 dark:border-neutral-800">
          <!-- Body -->
          <div class="px-3 py-4">
            <div v-if="isLoading" class="flex items-center justify-center py-8">
              <UIcon
                name="mdi:loading"
                class="w-6 h-6 animate-spin opacity-50"
              />
            </div>
            <div v-else-if="error" class="text-sm opacity-70">
              Failed to load storage information
            </div>
            <div v-else-if="data" class="space-y-4">
              <!-- Progress Bar -->
              <div class="space-y-2">
                <div
                  class="h-2 bg-gray-200 dark:bg-neutral-800 rounded-full overflow-hidden"
                >
                  <div
                    class="h-full bg-primary transition-all duration-300 rounded-full"
                    :style="{ width: `${data.dataUsagePercentage}%` }"
                  />
                </div>
                <!-- Storage Details -->
                <div class="flex justify-between text-sm opacity-70">
                  <span
                    >{{ data.dataAvailableGB.toFixed(2) }} GB available</span
                  >
                  <span>{{ data.dataTotalGB.toFixed(2) }} GB total</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Footer -->
          <div
            v-if="data"
            class="border-t border-gray-200 dark:border-neutral-800 p-2"
          >
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
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { storageInfo } from "@/queries/status";
import { useQuery } from "@pinia/colada";
import { ref } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();

const props = defineProps<{ defaultState?: boolean }>();

const { data, isLoading, error } = useQuery(storageInfo);
const isExpanded = ref(props.defaultState ?? false);
</script>

<style scoped></style>
