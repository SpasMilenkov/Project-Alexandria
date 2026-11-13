<script setup lang="ts">
import { useElementSize, useMediaQuery, useParentElement } from "@vueuse/core";
import { computed } from "vue";

import ActiveSessionsCard from "@/components/dashboard/account/ActiveSessionsCard.vue";
import ProfileCard from "@/components/dashboard/account/ProfileCard.vue";
import RecentActivityCard from "@/components/dashboard/account/RecentActivityCard.vue";
import SignInHistoryCard from "@/components/dashboard/account/SignInHistoryCard.vue";

// Matches the p-3 page padding above and below the sticky column.
const PAGE_PADDING_Y = 24;

const isDesktop = useMediaQuery("(min-width: 1024px)");

// From xl the cards sit side by side at the pane's height and size their contents to fit.
const isWide = useMediaQuery("(min-width: 1280px)");

// The layout's scroll pane shrinks when the audio player docks, so measure it instead of the viewport.
const scrollPane = useParentElement();
const { height: paneHeight } = useElementSize(scrollPane);

const profileColumnStyle = computed(() => {
  if (!isDesktop.value || paneHeight.value === 0) return undefined;

  return { height: `${paneHeight.value - PAGE_PADDING_Y}px` };
});
</script>

<template>
  <div
    class="grid min-h-full w-full grid-cols-1 content-start gap-3 p-3 lg:grid-cols-[20rem_minmax(0,1fr)] lg:grid-rows-[auto_1fr] 2xl:grid-cols-[24rem_minmax(0,1fr)]"
  >
    <!-- On a glass bar like every other heading, so a busy background picture can't wash it out. -->
    <header
      class="flex shrink-0 items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 lg:col-start-2 lg:row-start-1 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon name="i-lucide-user" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">My account</h1>
      </div>
    </header>

    <!-- Spans the full pane height and stays in place while the security history scrolls. -->
    <aside
      class="flex flex-col lg:sticky lg:top-3 lg:col-start-1 lg:row-span-2 lg:row-start-1 lg:self-start"
      :style="profileColumnStyle"
      aria-label="Profile"
    >
      <ProfileCard class="lg:min-h-0 lg:flex-1" />
    </aside>

    <div class="flex min-w-0 flex-col gap-3 lg:col-start-2 lg:row-start-2">
      <div
        class="grid grid-cols-1 items-start gap-3 xl:min-h-0 xl:flex-1 xl:grid-cols-2 xl:items-stretch"
      >
        <div class="flex min-w-0 flex-col gap-3">
          <ActiveSessionsCard />

          <RecentActivityCard :fill="isWide" :class="{ 'flex-1': isWide }" />
        </div>

        <SignInHistoryCard :fill="isWide" />
      </div>
    </div>
  </div>
</template>
