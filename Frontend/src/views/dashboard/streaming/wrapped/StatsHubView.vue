<template>
  <main class="mx-auto flex w-full flex-col gap-3 p-3">
    <template v-if="listeningEmpty">
      <ListeningWelcomeHero />

      <ListeningPreviews />
    </template>

    <template v-else>
      <ListeningHero :key="userId" />

      <div
        :key="userId"
        class="space-y-8 rounded-[14px] border border-gray-200/70 p-4 frosted-glass glass-surface md:p-5 dark:border-gray-700/70"
      >
        <div id="recaps" ref="recapsSection" class="scroll-mt-3" tabindex="-1">
          <ListeningRecaps />
        </div>

        <div id="timeline" ref="timelineSection" class="scroll-mt-3" tabindex="-1">
          <ListeningTimeline />
        </div>
      </div>
    </template>
  </main>
</template>

<script setup lang="ts">
import { useEventListener, useResizeObserver } from "@vueuse/core";
import { computed, nextTick, ref, useTemplateRef, watch } from "vue";
import { useRoute } from "vue-router";

import ListeningHero from "@/components/streaming/wrapped/listening/ListeningHero.vue";
import ListeningPreviews from "@/components/streaming/wrapped/listening/ListeningPreviews.vue";
import ListeningTimeline from "@/components/streaming/wrapped/listening/ListeningTimeline.vue";
import ListeningWelcomeHero from "@/components/streaming/wrapped/listening/ListeningWelcomeHero.vue";
import ListeningRecaps from "@/components/streaming/wrapped/recaps/ListeningRecaps.vue";
import { useListeningHistoryEmpty } from "@/composables/useListeningHistoryEmpty";
import { useAuthStore } from "@/stores/auth";

const route = useRoute();
const auth = useAuthStore();

const userId = computed(() => auth.user?.user.id);

const { listeningEmpty } = useListeningHistoryEmpty(userId);

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
