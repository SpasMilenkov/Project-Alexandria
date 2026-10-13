<script setup lang="ts">
import { useNow } from "@vueuse/core";
import { computed, ref, watch } from "vue";
import { RouterLink } from "vue-router";

import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import WrappedArt from "@/components/streaming/wrapped/WrappedArt.vue";
import { useHomeWidgetPreview, useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { useWrappedPalette } from "@/composables/useWrappedPalette";
import { HomeRecapStyle } from "@/enums/home-widget";
import { homeMonthlyRecap } from "@/queries/home-media";
import { useHomeStore } from "@/stores/home";
import { homeRecapFacts } from "@/utils/home-media";
import {
  wrappedMonth,
  wrappedMonthLabel,
  wrappedMonthLocation,
  wrappedMonthRange,
} from "@/utils/wrapped-month";
import { type WrappedPalette, wrappedCssColors } from "@/utils/wrapped-palette.utils";

const { widget } = defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();
const now = useNow({ interval: 60_000 });
const { palette: wrappedPalette } = useHomeWidgetPreview()
  ? { palette: ref<WrappedPalette | null>(null) }
  : useWrappedPalette();

const paletteStyle = computed(() => {
  const colors = widget.options.recapPalette;

  const artColors = wrappedCssColors(true, colors ?? wrappedPalette.value);

  if (!colors) return artColors;

  return {
    ...artColors,
    "--recap-gradient": `linear-gradient(rgb(0 0 0 / 55%), rgb(0 0 0 / 55%)), linear-gradient(135deg, ${colors.join(", ")})`,
  };
});

const scroller = ref<HTMLElement | null>(null);
const activeStory = ref(0);
const month = computed(() => wrappedMonth(widget.options.recapPeriod, now.value));
const label = computed(() => wrappedMonthLabel(month.value));
const final = computed(() => wrappedMonthRange(month.value, now.value).isFinal);
const location = computed(() => wrappedMonthLocation(month.value));

const query = useHomeWidgetQuery(
  () =>
    homeMonthlyRecap({ userId: store.ownerId ?? "", epoch: store.ownerEpoch, month: month.value }),
  (preview) => ({
    userId: store.ownerId ?? "",
    epoch: store.ownerEpoch,
    month: month.value,
    value: preview.recap,
  }),
);

const response = computed(() => {
  const value = query.data.value;

  if (
    value?.userId !== store.ownerId ||
    value.epoch !== store.ownerEpoch ||
    value.month !== month.value
  )
    return null;

  return value.value;
});

const facts = computed(() => (response.value ? homeRecapFacts(response.value) : []));
const cards = computed(() => response.value?.deck.cards ?? []);
const authPending = computed(() => isAuthError(query.error.value));

watch([month, () => store.ownerEpoch, () => widget.options.recapStyle], () => {
  activeStory.value = 0;
  if (scroller.value) scroller.value.scrollLeft = 0;
});

const updateStory = () => {
  const element = scroller.value;
  if (!element || !element.clientWidth) return;

  activeStory.value = Math.min(
    cards.value.length - 1,
    Math.round(element.scrollLeft / element.clientWidth),
  );
};

const showStory = (index: number) => {
  if (store.isEditing || !scroller.value) return;

  scroller.value.scrollTo({ left: index * scroller.value.clientWidth });
};
</script>

<template>
  <div
    class="recap-widget flex min-h-0 flex-1 flex-col"
    :style="paletteStyle"
    :class="{
      'pointer-events-none': store.isEditing,
      'recap-stories': widget.options.recapStyle === HomeRecapStyle.Stories && cards.length > 0,
    }"
    :data-recap-style="widget.options.recapStyle"
  >
    <HomeWidgetDataState
      :loading="authPending || (!response && !query.error.value)"
      :failed="Boolean(query.error.value) && !authPending"
      @retry="query.refresh()"
    >
      <div
        v-if="!cards.length"
        class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
      >
        <UIcon
          name="i-heroicons-musical-note"
          class="size-8 shrink-0 text-gray-500 dark:text-gray-500"
        />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No recap for {{ label }}</p>
        <p class="recap-extra text-xs text-gray-600 dark:text-gray-400">
          Music you play in Alexandria builds your listening story.
        </p>
        <UButton
          class="recap-extra"
          to="/streaming/music"
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="store.isEditing"
          >Browse music</UButton
        >
      </div>

      <template v-else-if="widget.options.recapStyle === HomeRecapStyle.Stories">
        <div
          class="recap-story-nav mb-2 flex shrink-0 gap-1 px-4 pt-4"
          role="group"
          aria-label="Recap stories"
        >
          <button
            v-for="(card, index) in cards"
            :key="card.id ?? card.type"
            type="button"
            class="h-2 min-w-0 flex-1 rounded-full focus-visible:outline-2 focus-visible:outline-primary"
            :class="activeStory === index ? 'bg-white' : 'bg-white/25'"
            :aria-label="`Show story ${index + 1}: ${card.headline}`"
            :aria-pressed="activeStory === index"
            :disabled="store.isEditing"
            @click="showStory(index)"
          />
        </div>

        <div
          ref="scroller"
          class="story-scroller relative flex min-h-0 flex-1 snap-x snap-mandatory overflow-x-auto overflow-y-hidden"
          aria-label="Swipe through your recap"
          @scroll.passive="updateStory"
        >
          <RouterLink
            v-for="card in cards"
            :key="card.id ?? card.type"
            :to="location"
            :tabindex="store.isEditing ? -1 : undefined"
            class="recap-story flex h-full w-full min-w-0 shrink-0 snap-start flex-col justify-center gap-2 overflow-hidden p-4 text-gray-900 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary dark:text-gray-100"
          >
            <WrappedArt
              class="story-art min-h-0 overflow-hidden"
              :card="card"
              :seed="response?.visualIdentity ?? month"
            />
            <p class="story-headline font-bold leading-tight">{{ card.headline }}</p>
            <p v-if="card.entries[0]" class="truncate text-sm text-gray-600 dark:text-gray-400">
              {{ card.entries[0].title }}
            </p>
            <p class="story-detail text-xs text-gray-600 dark:text-gray-400">{{ card.subline }}</p>
          </RouterLink>
        </div>

        <RouterLink
          :to="location"
          :tabindex="store.isEditing ? -1 : undefined"
          class="recap-extra mt-2 px-4 pb-4 text-xs text-gray-600 hover:underline dark:text-gray-400"
          >Open {{ label }} recap</RouterLink
        >
      </template>

      <RouterLink
        v-else-if="widget.options.recapStyle === HomeRecapStyle.Poster"
        :to="location"
        :tabindex="store.isEditing ? -1 : undefined"
        class="recap-poster group flex min-h-0 flex-1 flex-col items-start justify-end gap-2 overflow-hidden p-4 text-white focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary"
        :aria-label="`Open ${label} recap`"
      >
        <div class="poster-heading flex min-w-0 flex-col items-start gap-2">
          <p v-if="!(widget.showTitle ?? store.showWidgetTitles)" class="poster-eyebrow text-xs">
            Latest recap
          </p>
          <p class="poster-title font-bold leading-tight tracking-tight">
            Your {{ label }}
            <span class="whitespace-nowrap">{{ final ? "in music" : "so far" }}</span>
          </p>
          <span class="poster-status rounded-full bg-black/15 px-2 py-1 text-xs font-semibold">
            {{ final ? "Final" : "So far" }}
            <span class="poster-note font-normal">
              · {{ final ? "month complete" : "updates until month end" }}
            </span>
          </span>
        </div>

        <dl class="poster-facts flex flex-wrap gap-4">
          <div v-for="fact in facts.slice(0, 2)" :key="fact.label" class="min-w-0">
            <dd class="poster-value font-bold tabular-nums">{{ fact.value }}</dd>
            <dt class="text-xs">{{ fact.label }}</dt>
          </div>
        </dl>

        <span
          class="poster-open shrink-0 rounded-lg border border-white/40 bg-black/15 px-4 py-2 text-xs font-semibold transition-colors group-hover:bg-black/25"
        >
          Open recap →
        </span>
      </RouterLink>

      <RouterLink
        v-else
        :to="location"
        :tabindex="store.isEditing ? -1 : undefined"
        class="recap-face flex min-h-0 flex-1 flex-col justify-center gap-4 overflow-hidden p-4 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary"
        :aria-label="`Open ${label} recap`"
      >
        <div class="recap-heading min-w-0">
          <span
            class="recap-status inline-flex rounded-full bg-white/10 px-2 py-1 text-xs font-medium"
            >{{ final ? "Final" : "So far" }}</span
          >
          <p class="recap-title mt-2 font-bold leading-tight text-gray-900 dark:text-gray-100">
            {{ label }}
          </p>
          <p class="recap-extra mt-2 text-xs text-gray-600 dark:text-gray-400">
            Your month in music
          </p>
        </div>

        <dl class="recap-numbers grid min-w-0 gap-2">
          <div
            v-for="(fact, index) in facts"
            :key="fact.label"
            class="recap-fact flex min-w-0 items-center justify-between gap-2 rounded-xl bg-white/10 p-2"
            :class="{ 'recap-supporting': index > 1 }"
          >
            <dt class="shrink-0 text-xs text-gray-600 dark:text-gray-400">{{ fact.label }}</dt>
            <dd
              class="recap-value min-w-0 truncate text-right font-semibold leading-tight text-gray-900 dark:text-gray-100"
              :class="{ 'recap-primary-value': index < 2 }"
              :title="fact.value"
            >
              {{ fact.value }}
            </dd>
          </div>
        </dl>

        <span class="recap-extra text-xs text-gray-600 dark:text-gray-400">Open recap →</span>
      </RouterLink>
    </HomeWidgetDataState>
  </div>
</template>

<style scoped>
.recap-widget {
  container-name: recap-body;
  container-type: size;
  --recap-default-gradient: linear-gradient(135deg, #8a3b32, #4c1d95);
}

.recap-title {
  font-size: clamp(1rem, 9cqmin, 3rem);
}
.recap-face,
.recap-stories {
  background-image: var(--recap-gradient, var(--recap-default-gradient));
  color: white;
}

.recap-face :is(p, dd),
.recap-story .story-headline {
  color: white;
}

.recap-face :is(dt, span, .recap-extra),
.recap-story p:not(.story-headline),
.recap-stories > .recap-extra {
  color: #e5e7eb;
}
.recap-poster {
  background-image:
    radial-gradient(circle at 1px 1px, rgb(255 255 255 / 22%) 1.4px, transparent 1.6px),
    var(--recap-gradient, var(--recap-default-gradient));
  background-size:
    10px 10px,
    100% 100%,
    100% 100%;
}

.poster-title {
  max-width: 20ch;
  font-size: clamp(1.125rem, 12cqmin, 2rem);
}

.poster-value {
  font-size: clamp(1.125rem, 8cqmin, 1.5rem);
}

.poster-facts,
.poster-open,
.poster-note {
  display: none;
}

@container recap-body (min-width: 300px) {
  .poster-note {
    display: inline;
  }
}

@container recap-body (min-height: 280px) and (min-width: 300px) {
  .poster-facts {
    display: flex;
  }
}

@container recap-body (min-height: 180px) and (min-width: 280px) {
  .poster-open {
    display: block;
  }
}

@container recap-body (max-height: 170px) {
  .recap-poster,
  .recap-face,
  .recap-story {
    padding: 0.5rem;
  }

  .recap-story-nav {
    padding: 0.5rem 0.5rem 0;
  }

  .poster-eyebrow,
  .poster-note {
    display: none;
  }
}

@container home-widget (min-aspect-ratio: 2 / 1) {
  .recap-poster {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
  }

  .poster-heading {
    flex: 1;
  }

  .poster-facts {
    display: flex;
  }

  .poster-open {
    display: block;
  }
}

@container home-widget (min-aspect-ratio: 2 / 1) and (max-width: 480px) {
  .poster-facts,
  .poster-open,
  .poster-note {
    display: none;
  }
}

@container recap-body (min-width: 600px) and (min-height: 360px) {
  .recap-poster {
    display: grid;
    grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr);
    grid-template-rows: minmax(0, 1fr) auto;
    align-items: stretch;
    gap: 2rem;
    padding: 2rem;
  }

  .poster-heading {
    grid-column: 1;
    grid-row: 1;
    align-self: center;
    gap: 1rem;
  }

  .poster-title {
    max-width: 18ch;
    font-size: clamp(2rem, min(15cqh, 5cqw), 4.5rem);
    line-height: 1.05;
  }

  .poster-eyebrow,
  .poster-status,
  .poster-facts dt,
  .poster-open {
    font-size: 0.875rem;
  }

  .poster-facts {
    display: grid;
    grid-column: 2;
    grid-row: 1 / -1;
    grid-template-rows: repeat(2, minmax(0, 1fr));
    gap: 1.5rem;
    border-left: 1px solid rgb(255 255 255 / 25%);
    padding-left: 2rem;
  }

  .poster-facts > div {
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 0.5rem;
  }

  .poster-facts > div + div {
    border-top: 1px solid rgb(255 255 255 / 25%);
    padding-top: 1.5rem;
  }

  .poster-value {
    font-size: clamp(2rem, min(19cqh, 8cqw), 6rem);
    line-height: 1;
    white-space: nowrap;
  }

  .poster-open {
    grid-column: 1;
    grid-row: 2;
    justify-self: start;
    padding: 1rem 1.5rem;
  }
}

.story-headline {
  font-size: clamp(0.875rem, 7cqmin, 1.75rem);
}
.story-art :deep(figcaption) {
  display: none;
}
.story-scroller {
  scrollbar-width: thin;
  scroll-behavior: smooth;
}

@container home-widget (max-height: 200px) {
  .recap-extra,
  .recap-supporting,
  .story-detail {
    display: none;
  }
  .story-art {
    display: none;
  }
}

.recap-numbers {
  grid-template-columns: minmax(0, 1fr);
}

.recap-value {
  font-size: clamp(0.875rem, 5cqmin, 1.25rem);
}

.recap-primary-value {
  font-size: clamp(1.125rem, 11cqmin, 4rem);
}

.recap-supporting,
.recap-face .recap-extra {
  display: none;
}

@container recap-body (min-width: 300px) {
  .recap-numbers {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 1rem;
  }

  .recap-fact {
    flex-direction: column;
    align-items: flex-start;
    justify-content: center;
  }

  .recap-value {
    width: 100%;
    text-align: left;
  }
}

@container recap-body (min-height: 260px) {
  .recap-heading > .recap-extra {
    display: block;
  }

  .recap-fact {
    padding: 1rem;
  }
}

@container recap-body (min-width: 300px) and (min-height: 360px) {
  .recap-supporting {
    display: flex;
  }

  .recap-face > .recap-extra {
    display: block;
  }

  .recap-fact dt {
    font-size: 0.875rem;
  }
}

@container recap-body (min-width: 600px) and (min-height: 200px) and (min-aspect-ratio: 2 / 1) {
  .recap-face {
    flex-direction: row;
    align-items: center;
    gap: 2rem;
  }

  .recap-heading {
    flex: 1;
  }

  .recap-numbers {
    flex: 1.5;
  }

  .recap-supporting {
    display: flex;
  }

  .recap-face > .recap-extra {
    display: none;
  }
}

@container recap-body (max-height: 200px) {
  .recap-status {
    display: none;
  }

  .recap-title {
    margin-top: 0;
  }
}

@container recap-body (max-height: 170px) {
  .recap-face {
    gap: 0.5rem;
  }
}

@container recap-body (min-width: 300px) and (max-height: 200px) {
  .recap-face {
    flex-direction: row;
    align-items: center;
    gap: 1rem;
  }

  .recap-heading {
    flex: 1;
  }

  .recap-numbers {
    flex: 1.5;
  }
}

@container recap-body (max-width: 299px) and (max-height: 170px) {
  .recap-numbers > div:nth-child(2) {
    display: none;
  }
}

@container recap-body (max-width: 199px) {
  .recap-fact {
    flex-direction: column;
    align-items: flex-start;
  }

  .recap-value {
    width: 100%;
    text-align: left;
  }
}

@container recap-body (max-width: 199px) and (max-height: 260px) {
  .recap-face {
    gap: 0.5rem;
    padding: 0.5rem;
  }

  .recap-status {
    display: none;
  }

  .recap-title {
    margin-top: 0;
  }
}

@container recap-body (max-width: 199px) and (max-height: 140px) {
  .recap-fact dt {
    display: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .story-scroller {
    scroll-behavior: auto;
  }
}
</style>
