<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import type { UserSession } from "@/api/auth";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import ConfirmModal from "@/components/dashboard/ConfirmModal.vue";
import { useAppToast } from "@/composables/useAppToast";
import { revokeSession } from "@/mutations/sessions";
import { userSessions } from "@/queries/sessions";
import { useAuthStore } from "@/stores/auth";
import { formatCalendarDate, formatDateTime, formatTimeAgo } from "@/utils/date-formatters";
import { describeUserAgent } from "@/utils/user-agent";

import SessionsGhost from "./SessionsGhost.vue";

const appToast = useAppToast("account");
const authStore = useAuthStore();

const { data, isPending, error, refetch } = useQuery(() => userSessions(authStore.user?.user.id ?? ""));

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

// The current session first, then the most recently active.
const sessions = computed(() =>
  [...(data.value ?? [])].sort((a, b) => {
    if (a.isCurrent !== b.isCurrent) return a.isCurrent ? -1 : 1;

    return Date.parse(b.lastActiveAt) - Date.parse(a.lastActiveAt);
  }),
);

const deviceLabel = (session: UserSession) => describeUserAgent(session.userAgent).label;

const deviceIcon = (session: UserSession) => {
  if (describeUserAgent(session.userAgent).isMobile) return "i-lucide-smartphone";

  return "i-lucide-monitor";
};

const pendingRevoke = ref<UserSession | null>(null);

const { mutateAsync: revoke, isLoading: isRevoking } = revokeSession();

const confirmRevoke = async (confirmed: boolean) => {
  const session = pendingRevoke.value;
  if (!confirmed || !session) {
    pendingRevoke.value = null;
    return;
  }

  try {
    await revoke(session.id);
    appToast.success("Signed out that session", `${deviceLabel(session)} has to sign in again.`);
  } catch (err) {
    appToast.error("Couldn't sign that session out", err);
  } finally {
    pendingRevoke.value = null;
  }
};
</script>

<template>
  <section
    class="overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    aria-labelledby="active-sessions-heading"
  >
    <div class="border-b border-gray-200/70 px-4 py-3 md:px-5 dark:border-gray-700/70">
      <h2
        id="active-sessions-heading"
        class="text-sm font-semibold text-gray-900 dark:text-gray-100"
      >
        Active sessions
      </h2>
      <p class="mt-0.5 text-xs text-gray-600 dark:text-gray-400">
        Browsers and devices signed in to your account.
      </p>
    </div>

    <div v-if="visibleError" class="flex px-4 py-8 md:px-5">
      <GlassEmptyState
        icon="i-lucide-triangle-alert"
        title="Couldn't load your sessions"
        :description="visibleError.message"
        class="m-auto"
      >
        <UButton color="neutral" variant="outline" class="rounded-full px-5" @click="refetch()">
          Try again
        </UButton>
      </GlassEmptyState>
    </div>

    <div v-else-if="isPending && !data" class="flex h-24 items-center justify-center">
      <BlockSpinner :size="20" aria-label="Loading sessions" class="text-gray-500" />
    </div>

    <EmptyStatePanel v-else-if="!sessions.length" :panel="false">
      <template #ghost>
        <SessionsGhost />
      </template>

      <GlassEmptyState
        icon="i-lucide-monitor"
        title="No signed-in devices"
        description="Every browser and phone you sign in from shows up here, so you can sign out the ones you no longer use."
        class="m-auto"
      />
    </EmptyStatePanel>

    <ul
      v-else
      class="divide-y divide-gray-100/50 px-4 py-1 md:px-5 dark:divide-gray-800/50"
      aria-label="Active sessions"
    >
      <li v-for="session in sessions" :key="session.id" class="flex items-center gap-3 py-3">
        <UIcon
          :name="deviceIcon(session)"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />

        <div class="min-w-0 flex-1">
          <p class="flex items-center gap-2 text-sm font-medium text-gray-900 dark:text-gray-100">
            <span class="truncate">{{ deviceLabel(session) }}</span>
            <UBadge v-if="session.isCurrent" color="info" variant="subtle" size="sm">
              This device
            </UBadge>
          </p>
          <p class="mt-0.5 truncate text-xs text-gray-600 dark:text-gray-400">
            <span v-if="session.ipAddress" class="font-mono">{{ session.ipAddress }} · </span>
            <span>Signed in {{ formatCalendarDate(session.sessionStartedAt) }}</span>
          </p>
        </div>

        <span
          class="hidden shrink-0 text-xs text-gray-600 sm:block dark:text-gray-400"
          :title="formatDateTime(session.lastActiveAt)"
        >
          Active {{ formatTimeAgo(session.lastActiveAt) }}
        </span>

        <UButton
          v-if="!session.isCurrent"
          label="Sign out"
          size="xs"
          color="error"
          variant="outline"
          class="shrink-0"
          @click="pendingRevoke = session"
        />
      </li>
    </ul>

    <ConfirmModal
      :open="pendingRevoke !== null"
      title="Sign out this session?"
      :description="
        pendingRevoke ? `${deviceLabel(pendingRevoke)} will need to sign in again.` : undefined
      "
      confirm-label="Sign out"
      confirm-icon="i-lucide-log-out"
      danger-mode
      :loading="isRevoking"
      :alert="{
        title: 'It can stay open for a few minutes',
        description: 'Pages already open there keep working until their short sign-in expires.',
        color: 'warning',
      }"
      @close="confirmRevoke"
    />
  </section>
</template>
