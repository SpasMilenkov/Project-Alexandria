<script setup lang="ts">
import type { HomeGreetingOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import {
  HOME_GREETING_COLOR_MODES,
  HOME_GREETING_DECORATIONS,
  HOME_GREETING_STYLES,
  isHomeGreetingPixelArt,
} from "@/utils/home-greeting";
import { HOME_TEXT_ALIGNMENTS } from "@/utils/home-widget-options";

import HomeGreetingDecoration from "./HomeGreetingDecoration.vue";
import HomeGreetingStylePreview from "./HomeGreetingStylePreview.vue";
import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeGreetingOptions;
  section?: HomeOptionsSection;
}>();

const emit = defineEmits<{ change: [options: Partial<HomeGreetingOptions>] }>();
</script>

<template>
  <UFormField
    v-if="section !== 'options'"
    label="Greeting style"
    description="Choose your welcome."
  >
    <HomeStylePicker
      label="Greeting style"
      :styles="HOME_GREETING_STYLES"
      :selected="options.greetingStyle"
      :columns="2"
      @select="emit('change', { greetingStyle: $event })"
    >
      <template #preview="{ value }">
        <HomeGreetingStylePreview :greeting-style="value" />
      </template>
    </HomeStylePicker>
  </UFormField>

  <template v-if="section !== 'style'">
    <UFormField
      label="Heading"
      description="Leave blank for a greeting that follows the time of day."
    >
      <UInput
        :model-value="options.greetingHeading ?? ''"
        placeholder="Automatic greeting"
        aria-label="Greeting heading"
        :maxlength="80"
        class="w-full"
        @update:model-value="emit('change', { greetingHeading: String($event) || null })"
      />
    </UFormField>

    <UFormField label="Subtitle" description="Leave blank to hide the subtitle.">
      <UInput
        :model-value="options.greetingSubtitle"
        aria-label="Greeting subtitle"
        :maxlength="160"
        class="w-full"
        @update:model-value="emit('change', { greetingSubtitle: String($event) })"
      />
    </UFormField>

    <UFormField
      label="Decoration"
      description="Choose animated pixel art, an emoji, or text only. Animations respect reduced motion."
    >
      <USelect
        :model-value="options.greetingDecoration"
        :items="HOME_GREETING_DECORATIONS"
        aria-label="Greeting decoration"
        class="w-full"
        :ui="{
          base: isHomeGreetingPixelArt(options.greetingDecoration) ? 'min-h-10 ps-10' : 'min-h-10',
          leading: 'ps-2',
          item: 'min-h-10 gap-2',
        }"
        @update:model-value="emit('change', { greetingDecoration: $event })"
      >
        <template v-if="isHomeGreetingPixelArt(options.greetingDecoration)" #leading>
          <span class="size-6 shrink-0 text-gray-600 dark:text-gray-400">
            <HomeGreetingDecoration
              :decoration="options.greetingDecoration"
              :color-mode="options.greetingColorMode"
              :animated="false"
            />
          </span>
        </template>

        <template #item-leading="{ item }">
          <span
            v-if="isHomeGreetingPixelArt(item.value)"
            class="size-6 shrink-0 text-gray-600 dark:text-gray-400"
          >
            <HomeGreetingDecoration
              :decoration="item.value"
              :color-mode="options.greetingColorMode"
              :animated="false"
            />
          </span>
        </template>
      </USelect>
    </UFormField>

    <UFormField
      v-if="isHomeGreetingPixelArt(options.greetingDecoration)"
      label="Artwork colors"
      description="Keep the artwork monochrome or give it a little color."
    >
      <USelect
        :model-value="options.greetingColorMode"
        :items="HOME_GREETING_COLOR_MODES"
        aria-label="Greeting artwork colors"
        class="w-full"
        @update:model-value="emit('change', { greetingColorMode: $event })"
      />
    </UFormField>

    <UFormField label="Alignment">
      <USelect
        :model-value="options.greetingAlignment"
        :items="HOME_TEXT_ALIGNMENTS"
        aria-label="Greeting alignment"
        class="w-full"
        @update:model-value="emit('change', { greetingAlignment: $event })"
      />
    </UFormField>

    <UFormField label="Account name" description="Include your account name after the greeting.">
      <USwitch
        :model-value="options.greetingShowName"
        aria-label="Show name in greeting"
        @update:model-value="emit('change', { greetingShowName: $event })"
      />
    </UFormField>

    <UFormField label="Date">
      <USwitch
        :model-value="options.greetingShowDate"
        aria-label="Show date in greeting"
        @update:model-value="emit('change', { greetingShowDate: $event })"
      />
    </UFormField>
  </template>
</template>
