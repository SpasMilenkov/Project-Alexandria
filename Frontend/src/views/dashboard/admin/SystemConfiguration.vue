<script setup lang="ts">
import { ref, watch } from "vue";
import {
  resetUploadPolicy,
  updateUploadPolicy,
} from "@/mutations/adminSettings";
import { Icon } from "@iconify/vue";
import { uploadPolicy } from "@/queries/adminSettings";
import { useDebounceFn } from "@vueuse/core";
import { useQuery } from "@pinia/colada";

const { data: policy, isLoading, error } = useQuery(uploadPolicy());

const { mutate: doUpdate, isLoading: isSaving } = updateUploadPolicy();
const { mutate: doReset, isLoading: isResetting } = resetUploadPolicy();

const localPolicy = ref({
  skipClientValidationForTrustedUploads: false,
  skipUploadOnHashMatch: false,
});

watch(
  policy,
  (val) => {
    if (val) {
      localPolicy.value = { ...val };
    }
  },
  { immediate: true },
);

const debouncedSave = useDebounceFn(() => {
  doUpdate(localPolicy.value);
}, 600);

const handleReset = () => {
  doReset();
};

const lastSaved = ref<Date | null>(null);
watch(isSaving, (saving) => {
  if (!saving) {
    lastSaved.value = new Date();
  }
});
</script>

<template>
  <div class="min-h-screen px-6 py-10">
    <!-- Page header -->
    <div class="max-w-4xl mx-auto">
      <div class="mb-8">
        <div class="flex items-center gap-3 mb-1">
          <Icon icon="mdi:shield-crown-outline" class="w-6 h-6 text-primary" />
          <h1
            class="text-2xl font-semibold tracking-tight text-gray-900 dark:text-gray-50"
          >
            Admin Dashboard
          </h1>
        </div>
        <p class="text-sm text-gray-500 dark:text-gray-400 ml-9">
          System configuration and global settings
        </p>
      </div>

      <!-- Coming soon banner -->
      <UAlert
        color="neutral"
        variant="subtle"
        class="mb-8 border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm"
        :ui="{ root: 'rounded-xl' }"
      >
        <template #icon>
          <Icon icon="mdi:clock-outline" class="w-4 h-4 text-primary" />
        </template>
        <template #title>
          <span class="font-medium text-gray-700 dark:text-gray-300"
            >More settings on the way</span
          >
        </template>
        <template #description>
          <span class="text-gray-500 dark:text-gray-400">
            Global admin settings are still being built out. Additional controls
            for session management, storage quotas, authentication policies, and
            audit logging will appear here in upcoming releases.
          </span>
        </template>
      </UAlert>

      <!-- Main content grid -->
      <div class="grid gap-6">
        <!-- Upload Policy Card -->
        <div
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm overflow-hidden"
        >
          <!-- Card header -->
          <div
            class="flex items-center justify-between px-6 py-5 border-b border-gray-200/50 dark:border-gray-700/50"
          >
            <div class="flex items-center gap-3">
              <div
                class="w-8 h-8 rounded-lg bg-primary/10 flex items-center justify-center"
              >
                <Icon icon="mdi:upload-outline" class="w-4 h-4 text-primary" />
              </div>
              <div>
                <h2
                  class="text-sm font-semibold text-gray-800 dark:text-gray-200"
                >
                  Upload Policy
                </h2>
                <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                  Control file upload validation and deduplication behavior
                </p>
              </div>
            </div>
            <div class="flex items-center gap-2">
              <!-- Autosave indicator -->
              <Transition
                enter-active-class="transition-all duration-200 ease-out"
                leave-active-class="transition-all duration-150 ease-in"
                enter-from-class="opacity-0 translate-y-1"
                leave-to-class="opacity-0 translate-y-1"
              >
                <span
                  v-if="isSaving"
                  class="text-xs text-gray-400 dark:text-gray-500 flex items-center gap-1"
                >
                  <Icon icon="mdi:loading" class="w-3.5 h-3.5 animate-spin" />
                  Saving…
                </span>
              </Transition>

              <UButton
                color="error"
                variant="outline"
                size="xs"
                :loading="isResetting"
                @click="handleReset"
              >
                <template #leading>
                  <Icon icon="mdi:restore" class="w-3.5 h-3.5" />
                </template>
                Reset defaults
              </UButton>
            </div>
          </div>

          <!-- Loading state -->
          <div v-if="isLoading" class="px-6 py-10 flex justify-center">
            <Icon
              icon="mdi:loading"
              class="w-6 h-6 text-primary animate-spin"
            />
          </div>

          <!-- Error state -->
          <div v-else-if="error" class="px-6 py-6">
            <UAlert
              color="error"
              variant="subtle"
              :description="error?.message ?? 'Failed to load upload policy.'"
            />
          </div>

          <!-- Settings -->
          <div
            v-else
            class="divide-y divide-gray-200/50 dark:divide-gray-700/50"
          >
            <!-- Setting: Skip client validation -->
            <div class="px-6 py-5 flex items-start justify-between gap-6">
              <UFormField
                label="Skip client validation for trusted uploads"
                description="Bypass client-side file validation checks when the upload originates from a trusted source. Use with caution — only enable if trusted sources are strictly enforced server-side."
                class="flex-1"
              />
              <USwitch  
                v-model="localPolicy.skipClientValidationForTrustedUploads"
                color="primary"
                size="md"
                class="mt-0.5 shrink-0"
                @update:model-value="debouncedSave"
              />
            </div>

            <!-- Setting: Skip upload on hash match -->
            <div class="px-6 py-5 flex items-start justify-between gap-6">
              <UFormField
                label="Skip upload on hash match"
                description="If an identical file (by content hash) already exists in storage, skip the upload and return the existing reference. Reduces redundant storage and speeds up repeat uploads."
                class="flex-1"
              />
              <USwitch  
                v-model="localPolicy.skipUploadOnHashMatch"
                color="primary"
                size="md"
                class="mt-0.5 shrink-0"
                @update:model-value="debouncedSave"
              />
            </div>
          </div>
        </div>

        <!-- Future settings placeholder cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div
            v-for="section in [
              'Session Management',
              'Storage Quotas',
              'Auth Policies',
              'Audit Logging',
            ]"
            :key="section"
            class="rounded-2xl border border-dashed border-gray-200 dark:border-gray-700/60 bg-white/30 dark:bg-white/3 px-5 py-5 flex items-center gap-3"
          >
            <Icon
              icon="mdi:lock-outline"
              class="w-4 h-4 text-gray-400 dark:text-gray-600 shrink-0"
            />
            <div>
              <p class="text-sm font-medium text-gray-500 dark:text-gray-500">
                {{ section }}
              </p>
              <p class="text-xs text-gray-400 dark:text-gray-600 mt-0.5">
                Coming soon
              </p>
            </div>
          </div>
        </div>
      </div>

      <!-- Footer note -->
      <p class="mt-8 text-center text-xs text-gray-400 dark:text-gray-600">
        Changes to toggle settings are saved automatically.
      </p>
    </div>
  </div>
</template>
