<template>
  <main
    class="mx-auto w-full max-w-5xl px-4 pt-6 pb-[calc(6rem+env(safe-area-inset-bottom))] md:px-6 md:py-8"
  >
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
          Your daily rhythm and the years worth keeping.
        </p>
      </div>

      <UButton
        :to="`/stats/wrapped/${currentYear}`"
        color="neutral"
        variant="outline"
        icon="mdi:gift-outline"
        class="self-start shrink-0"
      >
        {{ currentYear }} Wrapped
      </UButton>
    </header>

    <nav
      aria-label="Listening sections"
      class="frosted-glass glass-surface-strong fixed inset-x-0 bottom-0 z-20 flex justify-center gap-2 border-t border-gray-200/70 px-4 pt-2 pb-[calc(0.5rem+env(safe-area-inset-bottom))] dark:border-gray-700/70 md:static md:mx-auto md:mb-6 md:w-fit md:justify-start md:rounded-full md:border md:p-2"
    >
      <RouterLink
        v-for="section in sections"
        :key="section.value"
        v-slot="{ href, navigate }"
        custom
        :to="{ path: '/stats', query: { ...route.query, view: section.value }, hash: route.hash }"
      >
        <a
          :href="href"
          :aria-current="activeSection === section.value ? 'page' : undefined"
          class="flex min-h-12 flex-1 items-center justify-center gap-2 rounded-full px-4 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary md:min-h-10 md:flex-none motion-reduce:transition-none"
          :class="
            activeSection === section.value
              ? 'bg-primary/10 text-primary'
              : 'text-gray-600 hover:bg-gray-100/50 dark:text-gray-400 dark:hover:bg-gray-800/50'
          "
          @click="navigate"
        >
          <UIcon :name="section.icon" class="h-4 w-4" />
          {{ section.label }}
        </a>
      </RouterLink>
    </nav>

    <KeepAlive :key="userId">
      <component :is="activePanel" />
    </KeepAlive>
  </main>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";

import ListeningRecaps from "@/components/streaming/wrapped/ListeningRecaps.vue";
import ListeningTimeline from "@/components/streaming/wrapped/ListeningTimeline.vue";
import { useAuthStore } from "@/stores/auth";

const route = useRoute();
const auth = useAuthStore();

const userId = computed(() => auth.user?.user.id);

const currentYear = new Date().getUTCFullYear();

const sections = [
  { value: "timeline", label: "Timeline", icon: "mdi:calendar-clock-outline" },
  { value: "recaps", label: "Recaps", icon: "mdi:gift-outline" },
];

const activeSection = computed(() => (route.query.view === "timeline" ? "timeline" : "recaps"));

const panels = { timeline: ListeningTimeline, recaps: ListeningRecaps };

const activePanel = computed(() => panels[activeSection.value]);
</script>
