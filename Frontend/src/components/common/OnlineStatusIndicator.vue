<script setup lang="ts">
import { useOnlineStatus } from "@/composables/composables/useOnlineStatus";
const { isOnline } = useOnlineStatus();
</script>

<template>
  <UTooltip :text="isOnline ? 'Connected' : 'Offline — some content may be unavailable'">
    <div class="flex items-center gap-1.5">
      <div class="relative flex size-3">
        <span
          v-if="isOnline"
          class="absolute inline-flex h-full w-full animate-ping rounded-full bg-green-400 opacity-75"
        />
        <span
          class="relative inline-flex size-3 rounded-full ring-2"
          :class="isOnline ? 'bg-green-500 ring-green-700/30' : 'bg-yellow-400 ring-yellow-600/40'"
        />
      </div>
    </div>
  </UTooltip>
</template>
