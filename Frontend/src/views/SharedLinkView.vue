<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";
import { useRoute } from "vue-router";

import type { ShareDownloadResponse, SharedFileMetadataDto } from "@/api/shareLinks";

import { getSharedFileDownloadUrl, getSharedFileMetadata } from "@/queries/shareLinks";
import { getFileIcon } from "@/utils/icon.utils";
import { formatBytes } from "@/utils/size.utils";

const route = useRoute();
const token = route.params.token as string;

const cardUi = {
  root: "ring-gray-200/70 dark:ring-gray-700/70 shadow-xl shadow-black/5 dark:shadow-black/30",
  body: "p-8",
};

const { data: metadata, isLoading } = useQuery<SharedFileMetadataDto>(() =>
  getSharedFileMetadata(token),
);

const {
  data: downloadData,
  isLoading: isDownloadUrlLoading,
  error,
} = useQuery<ShareDownloadResponse>(() => getSharedFileDownloadUrl(token));

const isDownloading = ref(false);

const fileIcon = computed(() => getFileIcon(metadata.value?.fileName ?? ""));
const formattedSize = computed(() => formatBytes(Number(metadata.value?.size ?? 0)));

const isExpired = computed(() => {
  if (!metadata.value) return false;
  return new Date(metadata.value.expiresAt) < new Date();
});

const formattedExpires = computed(() => {
  if (!metadata.value) return "";
  return new Date(metadata.value.expiresAt).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
});

// Map HTTP status codes to specific terminal states so each gets its own message.
// 410 Gone  → owner explicitly revoked the link
// 429       → download cap was exhausted (adjust if your backend uses a different code)
// anything else → generic not-found
const errorStatus = computed(() => (error.value as any)?.response?.status ?? null);

type DisplayError = "expired" | "revoked" | "limit-reached" | "not-found";

const displayError = computed((): DisplayError | null => {
  if ((metadata.value as any)?.isRevoked) return "revoked";
  if (isExpired.value) return "expired";
  if (!error.value)
    return null;
  if (errorStatus.value === 410) return "revoked";
  if (errorStatus.value === 429) return "limit-reached";
  return "not-found";
});

const isImageFile = computed(() => metadata.value?.mimeType.startsWith("image/") ?? false);
const canPreview = computed(() => isImageFile.value && Boolean(downloadData.value?.presignedUrl));

const handleDownload = () => {
  if (!downloadData.value?.presignedUrl) return;
  isDownloading.value = true;
  try {
    const a = document.createElement("a");
    a.href = downloadData.value.presignedUrl;
    a.download = downloadData.value.fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  } finally {
    isDownloading.value = false;
  }
};
</script>

<template>
  <div
    class="min-h-screen bg-gradient-to-br from-gray-50 via-white to-primary/5 dark:from-gray-950 dark:via-gray-900 dark:to-primary/10 flex flex-col items-center justify-center gap-5 p-6 w-full"
  >
    <!-- Loading -->
    <UCard v-if="isLoading" class="w-full max-w-md bg-white dark:bg-gray-900" :ui="cardUi">
      <div class="flex items-start gap-4">
        <USkeleton class="w-16 h-16 rounded-2xl shrink-0" />
        <div class="flex-1 space-y-2.5 pt-1">
          <USkeleton class="h-5 w-3/4" />
          <USkeleton class="h-3.5 w-1/2" />
        </div>
      </div>
      <USeparator class="my-6" />
      <div class="space-y-4">
        <div class="flex items-center justify-between">
          <USkeleton class="h-4 w-16" />
          <USkeleton class="h-4 w-20" />
        </div>
        <div class="flex items-center justify-between">
          <USkeleton class="h-4 w-12" />
          <USkeleton class="h-4 w-32" />
        </div>
        <div class="flex items-center justify-between">
          <USkeleton class="h-4 w-14" />
          <USkeleton class="h-5 w-16 rounded-full" />
        </div>
      </div>
      <USkeleton class="h-12 w-full mt-6 rounded-xl" />
    </UCard>

    <!-- Error: not found -->
    <UCard v-else-if="displayError === 'not-found'" class="w-full max-w-md" :ui="cardUi">
      <div class="flex flex-col items-center gap-4 text-center py-4">
        <div
          class="flex items-center justify-center w-16 h-16 rounded-2xl bg-gray-100 dark:bg-gray-800"
        >
          <Icon icon="mdi:link-variant-off" class="w-8 h-8 text-gray-400 dark:text-gray-500" />
        </div>
        <div class="space-y-1.5">
          <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Link not found</h2>
          <p class="text-sm text-muted max-w-xs leading-relaxed">
            This share link doesn't exist or has already been removed.
          </p>
        </div>
        <p class="text-xs text-dimmed mt-1">
          If someone sent you this, ask them to share a new link.
        </p>
      </div>
    </UCard>

    <!-- Error: expired -->
    <UCard v-else-if="displayError === 'expired'" class="w-full max-w-md" :ui="cardUi">
      <div class="flex flex-col items-center gap-4 text-center py-4">
        <div class="flex items-center justify-center w-16 h-16 rounded-2xl bg-warning/10">
          <Icon icon="mdi:clock-alert-outline" class="w-8 h-8 text-warning" />
        </div>
        <div class="space-y-1.5">
          <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Link expired</h2>
          <p class="text-sm text-muted max-w-xs leading-relaxed">
            This link expired on
            <span class="font-medium text-gray-700 dark:text-gray-300">{{ formattedExpires }}</span
            >.
          </p>
        </div>
        <p class="text-xs text-dimmed mt-1">Ask the sender to create a new link for this file.</p>
      </div>
    </UCard>

    <!-- Error: revoked — visually distinct from expiry; the action was deliberate -->
    <UCard v-else-if="displayError === 'revoked'" class="w-full max-w-md" :ui="cardUi">
      <div class="flex flex-col items-center gap-4 text-center py-4">
        <div class="flex items-center justify-center w-16 h-16 rounded-2xl bg-error/10">
          <Icon icon="mdi:link-variant-remove" class="w-8 h-8 text-error" />
        </div>
        <div class="space-y-1.5">
          <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Link revoked</h2>
          <p class="text-sm text-muted max-w-xs leading-relaxed">
            The owner has revoked access to this link.
          </p>
        </div>
        <p class="text-xs text-dimmed mt-1">
          Contact the sender if you still need access to this file.
        </p>
      </div>
    </UCard>

    <!-- Error: download cap exhausted -->
    <UCard v-else-if="displayError === 'limit-reached'" class="w-full max-w-md" :ui="cardUi">
      <div class="flex flex-col items-center gap-4 text-center py-4">
        <div class="flex items-center justify-center w-16 h-16 rounded-2xl bg-warning/10">
          <Icon icon="mdi:download-off-outline" class="w-8 h-8 text-warning" />
        </div>
        <div class="space-y-1.5">
          <h2 class="text-xl font-semibold text-gray-900 dark:text-white">
            Download limit reached
          </h2>
          <p class="text-sm text-muted max-w-xs leading-relaxed">
            This link has reached its maximum number of downloads.
          </p>
        </div>
        <p class="text-xs text-dimmed mt-1">Ask the sender to create a new link for this file.</p>
      </div>
    </UCard>

    <!-- Success -->
    <template v-else-if="metadata">
      <!-- Contextual header above card -->
      <UCard class="w-full max-w-md bg-white dark:bg-gray-900" :ui="cardUi">
        <!-- File identity -->
        <div class="flex items-start gap-4">
          <div
            class="flex items-center justify-center w-16 h-16 rounded-2xl bg-primary/10 shrink-0"
          >
            <Icon :icon="fileIcon" class="w-8 h-8 text-primary" />
          </div>
          <div class="min-w-0 flex-1 pt-0.5">
            <h1 class="text-lg font-semibold text-gray-900 dark:text-white leading-snug break-all">
              {{ metadata.fileName }}
            </h1>
            <div class="flex items-center gap-2 mt-1.5 flex-wrap">
              <span class="text-xs text-muted">{{ metadata.mimeType }}</span>
              <UBadge color="neutral" variant="subtle" size="xs">
                v{{ metadata.versionNumber
                }}{{ metadata.isPinnedVersion ? " · pinned" : " · latest" }}
              </UBadge>
            </div>
          </div>
        </div>

        <!-- Inline image preview -->
        <div
          v-if="canPreview"
          class="mt-5 rounded-xl overflow-hidden border border-gray-200/70 dark:border-gray-700/70 bg-gray-50 dark:bg-gray-800/50"
        >
          <img
            :src="downloadData!.presignedUrl"
            :alt="metadata.fileName"
            class="w-full max-h-64 object-contain"
          />
        </div>

        <USeparator class="my-5" />

        <!-- Metadata rows -->
        <div class="divide-y divide-gray-100 dark:divide-gray-800/60">
          <div class="flex items-center justify-between py-2.5">
            <div class="flex items-center gap-2 text-muted">
              <Icon icon="mdi:file-outline" class="w-4 h-4" />
              <span class="text-sm">Size</span>
            </div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ formattedSize }}
            </span>
          </div>
          <div class="flex items-center justify-between py-2.5">
            <div class="flex items-center gap-2 text-muted">
              <Icon icon="mdi:history" class="w-4 h-4" />
              <span class="text-sm">Version</span>
            </div>
            <UBadge color="neutral" variant="subtle" size="sm">
              {{ metadata.versionNumber }}{{ metadata.isPinnedVersion ? " (pinned)" : " (latest)" }}
            </UBadge>
          </div>
          <div class="flex items-center justify-between py-2.5">
            <div class="flex items-center gap-2 text-muted">
              <Icon icon="mdi:calendar-clock-outline" class="w-4 h-4" />
              <span class="text-sm">Link expires</span>
            </div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              {{ formattedExpires }}
            </span>
          </div>
        </div>

        <!-- Download -->
        <UButton
          color="primary"
          size="lg"
          block
          class="mt-6"
          :loading="isDownloading || isDownloadUrlLoading"
          :disabled="isExpired"
          @click="handleDownload"
        >
          <Icon icon="mdi:tray-arrow-down" class="w-5 h-5" />
          Download
        </UButton>
      </UCard>

      <!-- Expiry reminder below card -->
      <p class="text-[11px] text-dimmed text-center">Link expires {{ formattedExpires }}</p>
    </template>
  </div>
</template>
