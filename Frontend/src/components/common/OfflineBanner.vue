<script setup lang="ts">
import { useOnlineStatus } from "@/composables/useOnlineStatus";
const { isOnline } = useOnlineStatus();
</script>

<template>
  <div class="flex flex-col">
    <Transition name="banner">
      <div
        v-if="!isOnline"
        class="w-full bg-yellow-500 text-yellow-950 text-sm font-medium text-center py-2 px-4"
      >
        You are currently offline. Some content may be unavailable.
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.banner-enter-active,
.banner-leave-active {
  transition: all 0.2s ease;
}
.banner-enter-from,
.banner-leave-to {
  opacity: 0;
  transform: translateY(-100%);
}
</style>
