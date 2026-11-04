<script setup lang="ts">
import { useDocumentVisibility, useElementVisibility } from "@vueuse/core";
import { type Component, computed, ref } from "vue";

import { HomeGreetingColorMode, HomeGreetingDecoration } from "@/enums/home-widget";
import { HOME_GREETING_EMOJIS } from "@/utils/home-greeting";

import GreetingCat from "./greeting-artwork/GreetingCat.vue";
import GreetingCoffee from "./greeting-artwork/GreetingCoffee.vue";
import GreetingDog from "./greeting-artwork/GreetingDog.vue";
import GreetingDuck from "./greeting-artwork/GreetingDuck.vue";
import GreetingMoon from "./greeting-artwork/GreetingMoon.vue";
import GreetingPlant from "./greeting-artwork/GreetingPlant.vue";
import GreetingSun from "./greeting-artwork/GreetingSun.vue";
import GreetingVinyl from "./greeting-artwork/GreetingVinyl.vue";

const {
  decoration,
  colorMode = HomeGreetingColorMode.Monochrome,
  animated = true,
} = defineProps<{
  decoration: HomeGreetingDecoration;
  colorMode?: HomeGreetingColorMode;
  animated?: boolean;
}>();

const artwork: Partial<Record<HomeGreetingDecoration, Component>> = {
  [HomeGreetingDecoration.SunPixel]: GreetingSun,
  [HomeGreetingDecoration.MoonPixel]: GreetingMoon,
  [HomeGreetingDecoration.CoffeePixel]: GreetingCoffee,
  [HomeGreetingDecoration.PlantPixel]: GreetingPlant,
  [HomeGreetingDecoration.CatPixel]: GreetingCat,
  [HomeGreetingDecoration.DogPixel]: GreetingDog,
  [HomeGreetingDecoration.DuckPixel]: GreetingDuck,
  [HomeGreetingDecoration.VinylPixel]: GreetingVinyl,
};

const element = ref<HTMLElement | null>(null);
const visible = useElementVisibility(element);
const documentVisibility = useDocumentVisibility();

const running = computed(() => animated && visible.value && documentVisibility.value === "visible");
const illustration = computed(() => artwork[decoration]);
const emoji = computed(() => HOME_GREETING_EMOJIS[decoration]);
</script>

<template>
  <span
    v-if="decoration !== HomeGreetingDecoration.None"
    ref="element"
    class="greeting-decoration pointer-events-none block h-full w-full select-none"
    :data-animation-running="running"
    :data-animated="animated"
    aria-hidden="true"
  >
    <slot>
      <component
        :is="illustration"
        v-if="illustration"
        class="greeting-pixel-art"
        :data-color-mode="colorMode"
      />

      <span v-else-if="emoji" class="greeting-emoji">{{ emoji }}</span>
    </slot>
  </span>
</template>

<style scoped>
.greeting-decoration {
  container-type: inline-size;
}

.greeting-pixel-art {
  display: block;
  width: 100%;
  height: 100%;
}

.greeting-decoration[data-animation-running="false"] :deep(svg *) {
  animation-play-state: paused;
}

.greeting-decoration[data-animated="false"] :deep(svg *) {
  animation: none !important;
}

.greeting-emoji {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  font-family: "Apple Color Emoji", "Segoe UI Emoji", "Noto Color Emoji", sans-serif;
  font-size: 70cqi;
  line-height: 1;
}
</style>
