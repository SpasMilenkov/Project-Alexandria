<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { useClipboard } from "@vueuse/core";
import { computed, ref } from "vue";

import type { ShareLinkSummaryDto } from "@/api/shareLinks";

import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { createShareLink, revokeShareLink } from "@/mutations/shareLinks";
import { getShareLinksForFile } from "@/queries/shareLinks";
import { getFileIcon } from "@/utils/icon.utils";

const { mutateAsync: createLink } = createShareLink();
const { mutateAsync: revokeLink } = revokeShareLink();

const { fileId, fileName, fileVersionId, versionNumber } = defineProps<{
  fileId: string;
  fileName: string;
  /** When set, links are scoped to this version. Omit for whole-file (current version) links. */
  fileVersionId?: string | null;
  versionNumber?: number | null;
}>();

const emit = defineEmits<{ close: [changed: boolean] }>();

useModalBackGuard(() => emit("close", false));

const toast = useToast();
const { copy } = useClipboard();

const {
  data: links,
  refresh: refreshLinks,
  isLoading: linksLoading,
} = useQuery<ShareLinkSummaryDto[]>(() => getShareLinksForFile(fileId));

// Only show links that match this modal's scope: pinned to this version,
// or the "always current version" links when no version is specified.
const relevantLinks = computed(() => {
  if (!links.value) return [];
  return links.value.filter((link) =>
    fileVersionId ? link.fileVersionId === fileVersionId : link.fileVersionId === null,
  );
});

const modalTitle = computed(() =>
  fileVersionId ? `Share version ${versionNumber}` : "Share link",
);

const copyWithFeedback = async (value: string, label: string) => {
  await copy(value);
  toast.add({
    color: "success",
    duration: 2000,
    icon: "i-mdi-check-circle",
    title: `${label} copied`,
  });
};

const EXPIRE_OPTIONS = [
  { label: "1 hour", value: "01:00:00" },
  { label: "1 day", value: "1.00:00:00" },
  { label: "7 days", value: "7.00:00:00" },
  { label: "30 days", value: "30.00:00:00" },
];

const MAX_ACCESS_OPTIONS = [
  { label: "Unlimited", value: "unlimited" },
  { label: "1 download", value: "1" },
  { label: "5 downloads", value: "5" },
  { label: "10 downloads", value: "10" },
  { label: "25 downloads", value: "25" },
];

const selectedExpiry = ref("7.00:00:00");
const selectedMaxAccess = ref("unlimited");

interface CreatedLinkInfo {
  token: string;
  url: string;
  maxAccessCount: number | null;
}

const createdLink = ref<CreatedLinkInfo | null>(null);
const isCreating = ref(false);
const createError = ref<string | null>(null);

const shareBaseUrl = computed(() => `${window.location.origin}/share/`);

const formatDate = (dateStr: string) =>
  new Date(dateStr).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

const formatAccessCount = (link: ShareLinkSummaryDto) => {
  if (link.maxAccessCount === null) return `${link.accessCount} downloads`;
  return `${link.accessCount} / ${link.maxAccessCount} downloads`;
};

const isExpired = (link: ShareLinkSummaryDto) => new Date(link.expiresAt) < new Date();

const isLimitReached = (link: ShareLinkSummaryDto) =>
  link.maxAccessCount !== null && link.accessCount >= link.maxAccessCount;

// "revoked" is checked first — a revoked link may also be technically expired,
// but revocation is the more intentional and prominent terminal state.
type LinkStatus = "active" | "expired" | "limit-reached" | "revoked";

const getLinkStatus = (link: ShareLinkSummaryDto): LinkStatus => {
  if (link.isRevoked) return "revoked";
  if (isExpired(link)) return "expired";
  if (isLimitReached(link)) return "limit-reached";
  return "active";
};

const LINK_STATUS_ICONS: Record<LinkStatus, string> = {
  active: "mdi:link-variant",
  expired: "mdi:clock-outline",
  "limit-reached": "mdi:download-off-outline",
  revoked: "mdi:link-variant-remove",
};

// null = no badge for active links
const LINK_STATUS_LABELS: Record<LinkStatus, string | null> = {
  active: null,
  expired: "Expired",
  "limit-reached": "Limit reached",
  revoked: "Revoked",
};

// Badge color tokens — revoked uses error (red), others use warning (amber)
const LINK_STATUS_BADGE_CLASS: Record<LinkStatus, string> = {
  active: "",
  expired: "bg-warning/10 text-warning",
  "limit-reached": "bg-warning/10 text-warning",
  revoked: "bg-error/10 text-error",
};

// Row-level border/bg tint to make revoked visually distinct from mere expiry
const LINK_STATUS_ROW_CLASS: Record<LinkStatus, string> = {
  active: "bg-white/60 dark:bg-white/5 border-gray-200/70 dark:border-gray-700/70",
  expired: "bg-white/60 dark:bg-white/5 border-gray-200/70 dark:border-gray-700/70",
  "limit-reached": "bg-white/60 dark:bg-white/5 border-gray-200/70 dark:border-gray-700/70",
  revoked: "bg-error/5 dark:bg-error/5 border-error/20 dark:border-error/20",
};

const getLinkIcon = (link: ShareLinkSummaryDto) => LINK_STATUS_ICONS[getLinkStatus(link)];
const getLinkStatusLabel = (link: ShareLinkSummaryDto) => LINK_STATUS_LABELS[getLinkStatus(link)];
const getLinkStatusBadgeClass = (link: ShareLinkSummaryDto) =>
  LINK_STATUS_BADGE_CLASS[getLinkStatus(link)];
const getLinkRowClass = (link: ShareLinkSummaryDto) => LINK_STATUS_ROW_CLASS[getLinkStatus(link)];

const handleCreate = async () => {
  isCreating.value = true;
  createError.value = null;
  createdLink.value = null;
  try {
    const maxAccessCount =
      selectedMaxAccess.value === "unlimited" ? null : Number(selectedMaxAccess.value);

    const result = await createLink({
      fileId,
      expiry: selectedExpiry.value,
      fileVersionId: fileVersionId ?? null,
      maxAccessCount,
    });

    createdLink.value = {
      token: result.token,
      url: `${shareBaseUrl.value}${result.token}`,
      maxAccessCount: result.maxAccessCount,
    };
    refreshLinks();
  } catch (err: any) {
    createError.value = err?.message ?? err?.response?.data?.message ?? "Failed to create link";
  } finally {
    isCreating.value = false;
  }
};

const handleRevoke = async (id: string) => {
  try {
    // Capture the token before the mutation so we can clear createdLink if needed
    const targetToken = links.value?.find((l) => l.id === id)?.token;
    await revokeLink({ id, fileId });
    refreshLinks();
    if (createdLink.value && targetToken === createdLink.value.token) {
      createdLink.value = null;
    }
  } catch {
    toast.add({
      color: "error",
      duration: 3000,
      icon: "i-mdi-alert-circle",
      title: "Failed to revoke link",
    });
  }
};

const handleClose = () => emit("close", createdLink.value !== null);

const sortByDate = (a: ShareLinkSummaryDto, b: ShareLinkSummaryDto) =>
  new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();

const sortedLinks = computed(() => [...relevantLinks.value].sort(sortByDate));
</script>

<template>
  <UModal :title="modalTitle" :description="fileName" :close="{ onClick: handleClose }">
    <template #body>
      <div class="space-y-6 p-1">
        <!-- File header -->
        <div class="flex items-center gap-3">
          <div class="flex items-center justify-center w-10 h-10 rounded-lg bg-primary/10 shrink-0">
            <Icon :icon="getFileIcon(fileName)" class="w-5 h-5 text-primary" />
          </div>
          <div class="min-w-0 flex-1">
            <p class="text-sm font-medium truncate text-gray-900 dark:text-white">{{ fileName }}</p>
            <p v-if="fileVersionId" class="text-xs text-muted">Version {{ versionNumber }}</p>
          </div>
        </div>

        <!-- Existing links -->
        <div>
          <div class="flex items-center justify-between mb-3">
            <h4 class="text-xs font-semibold uppercase tracking-widest text-dimmed">
              Existing links
            </h4>
            <span v-if="sortedLinks.length" class="text-xs text-muted">
              {{ sortedLinks.length }}
            </span>
          </div>

          <USkeleton v-if="linksLoading" class="h-16 w-full rounded-lg" />

          <div v-else-if="!sortedLinks.length" class="text-center py-6 space-y-2">
            <Icon icon="mdi:link-variant-off" class="w-8 h-8 mx-auto text-muted" />
            <p class="text-xs text-muted">No share links yet</p>
          </div>

          <div v-else class="space-y-2">
            <div
              v-for="link in sortedLinks"
              :key="link.id"
              class="flex items-center gap-3 p-3 rounded-lg border transition-colors"
              :class="[
                getLinkRowClass(link),
                {
                  'opacity-50':
                    getLinkStatus(link) === 'expired' || getLinkStatus(link) === 'limit-reached',
                },
              ]"
            >
              <Icon
                :icon="getLinkIcon(link)"
                class="w-4 h-4 shrink-0"
                :class="getLinkStatus(link) === 'revoked' ? 'text-error' : 'text-muted'"
              />
              <div class="flex-1 min-w-0 space-y-0.5">
                <div class="flex items-center gap-1.5">
                  <span
                    class="text-xs font-mono truncate"
                    :class="getLinkStatus(link) === 'revoked' ? 'line-through text-muted' : ''"
                  >
                    {{ shareBaseUrl }}{{ link.token }}
                  </span>
                  <span
                    v-if="getLinkStatusLabel(link)"
                    class="shrink-0 text-[10px] px-1.5 py-0.5 rounded font-medium"
                    :class="getLinkStatusBadgeClass(link)"
                  >
                    {{ getLinkStatusLabel(link) }}
                  </span>
                </div>
                <div class="flex items-center gap-2 text-[11px] text-muted flex-wrap">
                  <span>{{ formatDate(link.createdAt) }}</span>
                  <template v-if="!link.isRevoked">
                    <span>·</span>
                    <span>Expires {{ formatDate(link.expiresAt) }}</span>
                  </template>
                  <span>·</span>
                  <span>{{ formatAccessCount(link) }}</span>
                </div>
              </div>
              <div class="flex items-center gap-1 shrink-0">
                <!-- Copy is still useful for auditing even on inactive links -->
                <UButton
                  icon="i-mdi-content-copy"
                  size="xs"
                  variant="ghost"
                  color="neutral"
                  aria-label="Copy link URL"
                  @click="copyWithFeedback(`${shareBaseUrl}${link.token}`, 'Link URL')"
                />
                <UButton
                  v-if="getLinkStatus(link) === 'active'"
                  icon="i-mdi-close-circle-outline"
                  size="xs"
                  variant="ghost"
                  color="error"
                  aria-label="Revoke link"
                  @click="handleRevoke(link.id)"
                />
              </div>
            </div>
          </div>
        </div>

        <USeparator />

        <!-- Create new link -->
        <div class="space-y-4">
          <h4 class="text-xs font-semibold uppercase tracking-widest text-dimmed">
            Generate new link
          </h4>

          <div class="flex flex-wrap items-end gap-3">
            <UFormField label="Expires after" class="flex-1 min-w-[140px]">
              <USelect v-model="selectedExpiry" :items="EXPIRE_OPTIONS" class="w-full" />
            </UFormField>
            <UFormField label="Download limit" class="flex-1 min-w-[140px]">
              <USelect v-model="selectedMaxAccess" :items="MAX_ACCESS_OPTIONS" class="w-full" />
            </UFormField>
            <UButton color="primary" :loading="isCreating" @click="handleCreate">
              <Icon icon="mdi:link-variant-plus" class="w-4 h-4" />
              Generate
            </UButton>
          </div>

          <UAlert
            v-if="createError"
            color="error"
            variant="subtle"
            icon="i-mdi-alert-circle"
            :title="createError"
          />

          <div
            v-if="createdLink"
            class="p-3 rounded-lg bg-primary/5 border border-primary/20 space-y-2"
          >
            <div class="flex items-center gap-1.5 text-xs text-primary font-medium">
              <Icon icon="mdi:check-circle" class="w-4 h-4" />
              Link created
            </div>
            <div class="flex items-center gap-2">
              <UInput
                :model-value="createdLink.url"
                readonly
                class="flex-1 font-mono text-xs"
                size="sm"
              />
              <UButton
                icon="i-mdi-content-copy"
                size="sm"
                variant="outline"
                color="primary"
                aria-label="Copy link URL"
                @click="copyWithFeedback(createdLink.url, 'Link URL')"
              />
            </div>
            <p v-if="createdLink.maxAccessCount !== null" class="text-[11px] text-muted">
              Limited to {{ createdLink.maxAccessCount }} download{{
                createdLink.maxAccessCount !== 1 ? "s" : ""
              }}
            </p>
          </div>
        </div>
      </div>
    </template>
  </UModal>
</template>
