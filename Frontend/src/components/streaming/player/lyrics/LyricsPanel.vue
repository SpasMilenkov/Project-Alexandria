<template>
  <!-- Mobile-only dim backdrop: on desktop the panel pushes layout instead of overlaying it, so no backdrop is needed there -->
  <Transition
    enter-active-class="transition-opacity duration-200 ease-out"
    enter-from-class="opacity-0"
    enter-to-class="opacity-100"
    leave-active-class="transition-opacity duration-150 ease-in"
    leave-from-class="opacity-100"
    leave-to-class="opacity-0"
  >
    <div
      v-if="open"
      class="fixed inset-0 z-20 bg-black/30 frosted-glass sm:hidden"
      @click="onClose"
    />
  </Transition>

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
      class="fixed inset-y-0 right-0 z-30 sm:static sm:inset-auto sm:z-auto sm:shrink-0 flex flex-col w-full sm:w-96 border-l border-gray-200/70 dark:border-gray-700/70 bg-white dark:bg-neutral-900 md:bg-white/60 md:dark:bg-white/[0.06] md:frosted-glass"
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
