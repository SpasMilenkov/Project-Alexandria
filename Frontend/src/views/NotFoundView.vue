<script setup lang="ts">
import { computed } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";

import ErrorGlyph from "@/components/common/errors/ErrorGlyph.vue";
import ErrorScene from "@/components/common/errors/ErrorScene.vue";
import ErrorSlip from "@/components/common/errors/ErrorSlip.vue";
import ErrorSlipRow from "@/components/common/errors/ErrorSlipRow.vue";
import { useAuthStore } from "@/stores/auth";

interface Shortcut {
  icon: string;
  label: string;
  to: string;
}

const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

// The router records the previous in-app entry here; a link opened in a fresh tab has none.
const canGoBack = typeof window.history.state?.back === "string";

const shortcuts = computed<Shortcut[]>(() => {
  const lastShortcut = streamingEnabled
    ? { icon: "i-lucide-circle-play", label: "Media", to: "/media" }
    : { icon: "i-lucide-tag", label: "Tags", to: "/dashboard/tags" };

  return [
    { icon: "i-lucide-house", label: "Home", to: "/home" },
    { icon: "i-lucide-folder", label: "Files", to: "/dashboard" },
    { icon: "i-lucide-images", label: "Photos", to: "/gallery" },
    lastShortcut,
  ];
});

const goBack = () => {
  router.back();
};
</script>

<template>
  <ErrorScene eyebrow="Error 404" title="We couldn't find that page">
    <template #mark>
      <span>4</span>
      <ErrorGlyph icon="i-lucide-library-big" />
      <span>4</span>
    </template>

    <template #description>
      The address may be mistyped, or the page may have been moved or deleted.
    </template>

    <template #actions>
      <UButton
        v-if="authStore.isSessionVerified"
        to="/home"
        color="primary"
        class="rounded-full px-5"
      >
        Go to Home
      </UButton>

      <UButton v-else to="/auth" color="primary" class="rounded-full px-5">Sign in</UButton>

      <UButton
        v-if="canGoBack"
        color="neutral"
        variant="outline"
        class="rounded-full px-5"
        @click="goBack"
      >
        Go back
      </UButton>
    </template>

    <template #aside>
      <ErrorSlip title="Details" stamp="404">
        <ErrorSlipRow label="Address">
          <span class="font-mono break-all">{{ route.path }}</span>
        </ErrorSlipRow>

        <ErrorSlipRow label="Status">
          <UBadge color="error" variant="subtle">Not found</UBadge>
        </ErrorSlipRow>

        <template #footer>
          <template v-if="authStore.isSessionVerified">
            <p class="mb-3 text-sm font-medium text-gray-600 dark:text-gray-400">Jump to</p>

            <nav class="grid grid-cols-2 gap-2" aria-label="Shortcuts">
              <RouterLink
                v-for="shortcut in shortcuts"
                :key="shortcut.to"
                :to="shortcut.to"
                class="flex items-center gap-3 rounded-xl border border-gray-200/70 px-4 py-3 text-sm font-medium text-gray-900 transition-colors hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary dark:border-gray-700/70 dark:text-gray-100"
              >
                <UIcon
                  :name="shortcut.icon"
                  class="size-4 shrink-0 text-gray-600 dark:text-gray-400"
                />
                {{ shortcut.label }}
              </RouterLink>
            </nav>
          </template>

          <p v-else class="text-sm text-gray-600 dark:text-gray-400">
            Sign in to get to your files.
          </p>
        </template>
      </ErrorSlip>
    </template>
  </ErrorScene>
</template>
