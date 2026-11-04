<script setup lang="ts">
import { useElementSize } from "@vueuse/core";
import { computed, ref, useId, watch } from "vue";

import { HomeLibraryDestination, HomeLibraryStyle } from "@/enums/home-widget";
import { type HomeLibraryLink, homeLibraryDisplayOrder } from "@/utils/home-library";

import HomeLibrarySpotlightArt from "./HomeLibrarySpotlightArt.vue";

const {
  links,
  libraryStyle,
  featuredDestination = null,
  disabled = false,
} = defineProps<{
  links: HomeLibraryLink[];
  libraryStyle: HomeLibraryStyle;
  featuredDestination?: HomeLibraryDestination | null;
  disabled?: boolean;
}>();

const body = ref<HTMLElement | null>(null);
const { width, height } = useElementSize(body);
const expanded = ref(false);
const listId = `library-shortcuts-${useId()}`;

const compact = computed(() => height.value > 0 && height.value < 208 && width.value < 560);

const limit = computed(() => (width.value >= 256 ? 2 : 1));

const hasOverflow = computed(
  () => libraryStyle !== HomeLibraryStyle.Icons && compact.value && links.length > limit.value,
);

const orderedLinks = computed(() =>
  homeLibraryDisplayOrder(links, libraryStyle, featuredDestination),
);

const featuredSpan = computed(() => Math.min(3, Math.max(1, links.length - 1)));

const visibleLinks = computed(() => {
  if (hasOverflow.value && !expanded.value) return orderedLinks.value.slice(0, limit.value);

  return orderedLinks.value;
});

const moreLabel = computed(() => {
  if (expanded.value) return "Fewer shortcuts";

  return `More shortcuts (${links.length - limit.value})`;
});

watch([() => links, compact], () => {
  expanded.value = false;
});

const navigate = (event: MouseEvent) => {
  if (disabled) event.preventDefault();
};
</script>

<template>
  <div
    ref="body"
    class="library-body flex min-h-0 min-w-0 flex-1 flex-col gap-2"
    :data-library-style="libraryStyle"
    :data-compact="compact"
    :data-single="links.length === 1"
    :style="{
      '--library-tile-rows': Math.ceil(links.length / 2),
      '--library-side-rows': Math.ceil((links.length - 1 + featuredSpan) / 2),
      '--library-feature-span': featuredSpan,
    }"
  >
    <nav
      :id="listId"
      aria-label="Library destinations"
      class="library-scroll min-h-0 min-w-0 flex-1 overflow-auto"
    >
      <div
        class="library-items"
        :class="{
          'library-launchpad': libraryStyle === HomeLibraryStyle.Launchpad,
          'library-directory': libraryStyle === HomeLibraryStyle.Directory,
          'library-spotlight': libraryStyle === HomeLibraryStyle.Spotlight,
          'library-icons': libraryStyle === HomeLibraryStyle.Icons,
        }"
      >
        <UTooltip
          v-for="(link, index) in visibleLinks"
          :key="link.destination"
          :text="link.label"
          :disabled="libraryStyle !== HomeLibraryStyle.Icons || disabled"
          :delay-duration="150"
          :ignore-non-keyboard-focus="false"
        >
          <RouterLink
            :to="link.to"
            :aria-label="link.label"
            :aria-disabled="disabled || undefined"
            :tabindex="disabled ? -1 : undefined"
            :title="libraryStyle === HomeLibraryStyle.Icons ? undefined : link.label"
            :data-featured="libraryStyle === HomeLibraryStyle.Spotlight && index === 0"
            class="library-link text-gray-900 transition-colors focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-primary dark:text-gray-100 motion-reduce:transition-none"
            @click="navigate"
          >
            <span class="library-icon text-gray-600 dark:text-gray-400">
              <UIcon :name="link.icon" class="library-glyph size-5" />
            </span>

            <template v-if="libraryStyle === HomeLibraryStyle.Spotlight && index === 0">
              <span
                class="library-feature-marker text-xs font-medium text-gray-600 dark:text-gray-400"
                aria-hidden="true"
                >Spotlight</span
              >

              <HomeLibrarySpotlightArt class="library-feature-art" :icon="link.icon" />
            </template>

            <template v-if="libraryStyle !== HomeLibraryStyle.Icons">
              <span class="library-copy min-w-0">
                <span class="library-label block text-sm font-semibold">{{ link.label }}</span>
                <span class="library-description mt-1 text-xs text-gray-600 dark:text-gray-400">
                  {{ link.description }}
                </span>
              </span>

              <UIcon
                name="i-heroicons-arrow-right"
                class="library-arrow size-4 text-gray-500 dark:text-gray-500"
              />
            </template>
          </RouterLink>
        </UTooltip>
      </div>
    </nav>

    <button
      v-if="hasOverflow"
      type="button"
      :aria-expanded="expanded"
      :aria-controls="listId"
      :disabled="disabled"
      class="min-h-9 shrink-0 rounded-lg border border-gray-200/70 px-2 text-left text-xs font-medium text-gray-600 hover:bg-gray-100/50 focus-visible:outline-2 focus-visible:outline-primary disabled:cursor-default dark:border-gray-700/70 dark:text-gray-400 dark:hover:bg-gray-800/50"
      @click="expanded = !expanded"
    >
      {{ moreLabel }}
    </button>
  </div>
</template>

<style scoped>
.library-items {
  display: grid;
  gap: 1rem;
  min-height: 100%;
  align-content: start;
}

.library-link {
  display: grid;
  grid-template-columns: 2rem minmax(0, 1fr) 1rem;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
  min-height: 4rem;
  padding: 0.5rem;
  border: 1px solid var(--ui-border-muted);
  border-radius: 0.75rem;
}

.library-link:hover {
  background: var(--ui-bg-accented);
}

.library-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  border-radius: 0.5rem;
  background: var(--ui-bg-elevated);
}

.library-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.library-description {
  display: none;
}

.library-feature-marker,
.library-feature-art {
  display: none;
}

.library-directory {
  gap: 0;
}

.library-directory .library-link {
  border: 0;
  border-radius: 0;
}

.library-directory .library-link + .library-link {
  border-top: 1px solid var(--ui-border-muted);
}

.library-directory .library-icon {
  background: transparent;
}

[data-compact="true"] .library-items {
  gap: 0.5rem;
}

[data-compact="true"] .library-link {
  min-height: 2.75rem;
  padding: 0.5rem;
}

@container home-widget (max-width: 240px) {
  .library-link {
    grid-template-columns: 2rem minmax(0, 1fr);
  }

  .library-arrow {
    display: none;
  }
}

@container home-widget (min-width: 256px) {
  [data-compact="true"] .library-items {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@container home-widget (min-width: 360px) and (min-height: 240px) {
  .library-launchpad {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    grid-template-rows: repeat(var(--library-tile-rows), minmax(112px, 1fr));
  }

  .library-launchpad .library-link {
    grid-template-columns: minmax(0, 1fr) 1rem;
    grid-template-rows: auto 1fr;
    align-items: start;
    gap: 1rem;
    padding: 1rem;
  }

  .library-launchpad .library-copy {
    grid-column: 1 / -1;
    grid-row: 2;
    align-self: end;
  }

  .library-launchpad .library-arrow {
    grid-column: 2;
    grid-row: 1;
    margin-top: 0.5rem;
  }

  .library-launchpad .library-label {
    white-space: normal;
  }
}

@container home-widget (min-width: 400px) and (min-height: 240px) {
  .library-description {
    display: block;
  }

  .library-directory .library-link {
    min-height: 4rem;
    padding: 0.5rem;
  }
}

@container home-widget (min-width: 480px) and (min-height: 240px) {
  .library-spotlight {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    grid-template-rows: repeat(var(--library-side-rows), minmax(64px, 1fr));
  }

  .library-spotlight .library-link[data-featured="true"] {
    container: library-feature / size;
    grid-row: 1 / span var(--library-feature-span);
    grid-template-columns: minmax(0, 1fr) 1rem;
    grid-template-rows: minmax(0, 1fr) auto;
    align-items: start;
    gap: 1rem;
    padding: 1rem;
  }

  .library-spotlight [data-featured="true"] .library-icon {
    display: none;
  }

  .library-spotlight .library-feature-marker {
    z-index: 1;
    display: block;
    grid-column: 1;
    grid-row: 1;
  }

  .library-spotlight .library-feature-art {
    display: block;
    grid-column: 1 / -1;
    grid-row: 1;
    align-self: stretch;
    margin-top: 1.5rem;
  }

  .library-spotlight [data-featured="true"] .library-copy {
    grid-column: 1 / -1;
    grid-row: 2;
    align-self: end;
  }

  .library-spotlight [data-featured="true"] .library-label {
    font-size: clamp(1.25rem, min(7cqi, 10cqb), 2rem);
    line-height: 1.25;
    white-space: normal;
    overflow-wrap: anywhere;
  }

  .library-spotlight [data-featured="true"] .library-description {
    margin-top: 0.5rem;
    font-size: clamp(0.75rem, 3cqi, 0.875rem);
  }

  .library-spotlight [data-featured="true"] .library-arrow {
    grid-column: 2;
    grid-row: 1;
    z-index: 1;
  }
}

@container home-widget (min-width: 640px) and (min-height: 360px) {
  .library-launchpad .library-icon {
    width: 3rem;
    height: 3rem;
  }

  .library-launchpad .library-glyph {
    width: 2rem;
    height: 2rem;
  }

  .library-launchpad .library-label {
    font-size: 1.125rem;
    line-height: 1.5;
  }

  .library-launchpad .library-description {
    font-size: 0.875rem;
  }
}

@container home-widget (min-width: 560px) and (max-height: 239px) {
  .library-items {
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 0.5rem;
    align-content: center;
  }

  .library-link {
    grid-template-columns: 2rem minmax(0, 1fr);
    min-height: 3rem;
  }

  .library-arrow {
    display: none;
  }

  .library-directory .library-link + .library-link {
    border-top: 0;
    border-left: 1px solid var(--ui-border-muted);
  }
}

[data-single="true"] .library-items {
  grid-template-columns: minmax(0, 1fr);
}

.library-items.library-icons {
  --library-square-size: 3rem;
  --library-glyph-size: 1.25rem;
  display: flex;
  flex-wrap: wrap;
  align-content: center;
  justify-content: center;
  gap: 0.5rem;
}

.library-icons .library-link {
  display: flex;
  flex: 0 0 var(--library-square-size);
  align-items: center;
  justify-content: center;
  width: var(--library-square-size);
  height: var(--library-square-size);
  min-height: var(--library-square-size);
  padding: 0;
  border-radius: 0.5rem;
}

.library-icons .library-icon {
  width: var(--library-glyph-size);
  height: var(--library-glyph-size);
  background: transparent;
}

.library-icons .library-glyph {
  width: var(--library-glyph-size);
  height: var(--library-glyph-size);
}

@container home-widget (min-width: 360px) and (min-height: 200px) {
  .library-items.library-icons {
    --library-square-size: clamp(3rem, min(12cqi, 24cqb), 5rem);
    --library-glyph-size: clamp(1.25rem, min(5cqi, 10cqb), 2rem);
  }
}
</style>
