<template>
  <!-- Desktop only: the layout hides this below md, where the player island's sheet shows lyrics instead. -->
  <Transition
    enter-active-class="transition-all duration-200 ease-out"
    enter-from-class="opacity-0 translate-x-4"
    enter-to-class="opacity-100 translate-x-0"
    leave-active-class="transition-all duration-150 ease-in"
    leave-from-class="opacity-100 translate-x-0"
    leave-to-class="opacity-0 translate-x-4"
  >
    <aside
      v-if="open"
      class="flex w-96 shrink-0 flex-col border-l border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface-strong"
      aria-label="Lyrics"
    >
      <LyricsContent @close="onClose" />
    </aside>
  </Transition>
</template>

<script setup lang="ts">
import LyricsContent from "./LyricsContent.vue";

const open = defineModel<boolean>("open", { default: false });

const onClose = () => {
  open.value = false;
};
</script>
