<template>
  <main class="mx-auto w-full max-w-7xl px-4 py-6 md:px-6 md:py-8">
    <header class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <p
          class="text-xs font-semibold tracking-[0.16em] text-gray-600 uppercase dark:text-gray-400"
        >
          A soundtrack worth remembering
        </p>

        <h1 class="mt-2 text-3xl font-black tracking-tight text-gray-900 dark:text-gray-100">
          Your listening
        </h1>

        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Your recent months, annual recaps, and daily listening history.
        </p>
      </div>

      <UButton
        :to="{ query: route.query, hash: '#timeline' }"
        color="neutral"
        variant="outline"
        icon="mdi:calendar-clock-outline"
        class="self-start shrink-0"
        @click="startSectionJump"
      >
        Jump to timeline
      </UButton>
    </header>

    <div :key="userId" class="space-y-8">
      <div id="recaps" ref="recapsSection" class="scroll-mt-8" tabindex="-1">
        <ListeningRecaps />
      </div>

      <div id="timeline" ref="timelineSection" class="scroll-mt-8" tabindex="-1">
        <ListeningTimeline />
      </div>
    </div>
  </main>
</template>

<script setup lang="ts">
import { useEventListener, useResizeObserver } from "@vueuse/core";
import { computed, nextTick, ref, useTemplateRef, watch } from "vue";
import { useRoute } from "vue-router";

import ListeningRecaps from "@/components/streaming/wrapped/ListeningRecaps.vue";
import ListeningTimeline from "@/components/streaming/wrapped/ListeningTimeline.vue";
import { useAuthStore } from "@/stores/auth";

const route = useRoute();
const auth = useAuthStore();

const userId = computed(() => auth.user?.user.id);

const timelineSection = useTemplateRef("timelineSection");
const recapsSection = useTemplateRef("recapsSection");
const followingJump = ref(false);

const alignSection = (focus = false) => {
  if (!followingJump.value) return;

  const target = route.hash === "#recaps" ? recapsSection.value : timelineSection.value;

  target?.scrollIntoView({ block: "start" });
  if (focus) target?.focus({ preventScroll: true });
};

const startSectionJump = async () => {
  followingJump.value = true;
  await nextTick();

  alignSection(true);
};

useResizeObserver([recapsSection, timelineSection], () => alignSection());

useEventListener(
  ["wheel", "touchstart", "pointerdown", "keydown"],
  () => {
    followingJump.value = false;
  },
  { passive: true },
);

watch(
  () => [route.query.view, route.hash],
  async () => {
    if (route.query.view !== "timeline" && route.hash !== "#timeline" && route.hash !== "#recaps") {
      followingJump.value = false;
      return;
    }

    await startSectionJump();
  },
  { immediate: true, flush: "post" },
);
</script>
