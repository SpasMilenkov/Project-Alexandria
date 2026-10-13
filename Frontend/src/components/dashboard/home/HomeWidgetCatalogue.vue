<script setup lang="ts">
import { useEventListener } from "@vueuse/core";
import { nextTick, ref, watch } from "vue";

import type { HomeWidgetType } from "@/enums/home-widget";

import HomeWidgetCatalogueItems from "@/components/dashboard/home/HomeWidgetCatalogueItems.vue";
import { glassOverlayContent } from "@/utils/modalUi";

const open = defineModel<boolean>("open", { required: true });
const {
  docked = false,
  disabledReason = "",
  dragging,
} = defineProps<{
  disabled: boolean;
  docked?: boolean;
  dragging: boolean;
  disabledReason?: string;
}>();

const emit = defineEmits<{
  add: [type: HomeWidgetType];
  pointerDrag: [type: HomeWidgetType, event: PointerEvent];
  showcase: [];
}>();

const panel = ref<HTMLElement | null>(null);
let trigger: HTMLElement | null = null;

const enterPanel = (element: Element) => {
  element.toggleAttribute("inert", dragging);
  element.removeAttribute("aria-hidden");
};

const leavePanel = (element: Element) => {
  element.setAttribute("inert", "");
  element.setAttribute("aria-hidden", "true");
};

watch(open, async (value) => {
  if (value) {
    trigger = document.activeElement as HTMLElement | null;
    await nextTick();
    panel.value?.querySelector<HTMLInputElement>("input")?.focus();
  } else {
    trigger?.focus({ preventScroll: true });
  }
});

useEventListener(window, "keydown", (event) => {
  if (!open.value || event.defaultPrevented || event.key !== "Escape") return;

  event.preventDefault();
  open.value = false;
});
</script>

<template>
  <button
    v-if="open && !dragging"
    type="button"
    tabindex="-1"
    class="absolute inset-0 z-40 cursor-default"
    aria-label="Dismiss widget catalogue"
    @click="open = false"
  />

  <Transition name="home-catalogue" @before-enter="enterPanel" @before-leave="leavePanel">
    <section
      v-if="open"
      ref="panel"
      aria-label="Widget catalogue"
      class="home-catalogue absolute z-50 flex min-h-0 flex-col overflow-hidden rounded-xl border border-gray-200/70 shadow-2xl transition-opacity dark:border-gray-700/70"
      :class="[
        glassOverlayContent,
        { 'is-mobile': !docked, 'pointer-events-none opacity-25': dragging },
      ]"
      :inert="dragging"
      @keydown.delete.stop
    >
      <header class="flex shrink-0 items-center justify-between gap-4 px-3 py-2">
        <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Add a widget</h2>
        <UButton
          color="neutral"
          variant="ghost"
          icon="i-heroicons-x-mark"
          aria-label="Close widget catalogue"
          @click="open = false"
        />
      </header>

      <div class="shrink-0 px-3 pb-3">
        <UButton
          color="neutral"
          variant="outline"
          icon="i-heroicons-book-open"
          class="w-full justify-center"
          @click="emit('showcase')"
          >Explore all sizes</UButton
        >
      </div>

      <HomeWidgetCatalogueItems
        :disabled="disabled"
        :disabled-reason="disabledReason"
        :draggable="docked"
        :dragging="dragging"
        @add="emit('add', $event)"
        @pointer-drag="(type, event) => emit('pointerDrag', type, event)"
      />

      <p
        class="shrink-0 border-t border-gray-200/70 px-3 py-2 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
      >
        Click to add<span v-if="docked">, or drag a card onto the dashboard</span>.
      </p>
    </section>
  </Transition>
</template>

<style scoped>
.home-catalogue {
  top: 0.5rem;
  right: 1rem;
  width: min(560px, calc(100% - 2rem));
  max-height: calc(100% - 1rem);
}

.is-mobile {
  inset: auto 0 0;
  width: 100%;
  max-height: 78%;
  border-radius: 1rem 1rem 0 0;
}

.home-catalogue-enter-active {
  transition:
    opacity 200ms ease-out,
    transform 200ms ease-out;
}

.home-catalogue-leave-active {
  pointer-events: none;
  transition:
    opacity 150ms ease-in,
    transform 150ms ease-in;
}

.home-catalogue-enter-from,
.home-catalogue-leave-to {
  opacity: 0;
  transform: translateY(-0.5rem);
}

.is-mobile.home-catalogue-enter-from,
.is-mobile.home-catalogue-leave-to {
  transform: translateY(1rem);
}

@media (prefers-reduced-motion: reduce) {
  .home-catalogue {
    transition: none;
  }

  .home-catalogue.home-catalogue-enter-from,
  .home-catalogue.home-catalogue-leave-to {
    opacity: 1;
    transform: none;
  }
}
</style>
