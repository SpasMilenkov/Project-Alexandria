<template>
  <button
    type="button"
    class="group/fav flex items-center justify-center rounded-full border border-gray-200/70 bg-default text-gray-700 shadow-md transition-all duration-200 ease-[cubic-bezier(0.34,1.56,0.64,1)] hover:scale-110 hover:bg-elevated active:scale-90 motion-reduce:transition-none disabled:pointer-events-none disabled:opacity-60 dark:border-gray-700/70 dark:text-gray-300 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
    :class="containerClass"
    :disabled="disabled"
    :data-starred="playlist.isStarred"
    :aria-label="playlist.isStarred ? 'Remove from favorites' : 'Add to favorites'"
    :aria-pressed="playlist.isStarred"
    @click.stop="emit('favorite')"
  >
    <span class="relative flex items-center justify-center" :class="iconBoxClass">
      <UIcon
        name="mdi:heart-outline"
        class="absolute inset-0 h-full w-full transition-all duration-200 ease-out group-hover/fav:text-primary group-data-[starred=true]/fav:scale-0 group-data-[starred=true]/fav:opacity-0"
      />

      <Transition
        enter-active-class="fav-enter"
        leave-active-class="transition-all duration-150 ease-in"
        leave-to-class="scale-50 opacity-0"
        :duration="{ enter: 600, leave: 150 }"
      >
        <span
          v-if="playlist.isStarred"
          class="absolute inset-0 flex items-center justify-center text-primary"
        >
          <span
            class="fav-ring absolute inset-0 rounded-full border-2 border-primary"
            aria-hidden="true"
          />
          <span
            v-for="i in 6"
            :key="i"
            class="fav-dot absolute top-1/2 left-1/2 -mt-0.5 -ml-0.5 h-1 w-1 rounded-full bg-primary"
            :style="{ '--angle': `${i * 60}deg` }"
            aria-hidden="true"
          />
          <span class="fav-icon flex" :class="iconBoxClass">
            <UIcon name="mdi:heart" class="h-full w-full" />
          </span>
        </span>
      </Transition>
    </span>
  </button>
</template>

<script setup lang="ts">
import { computed } from "vue";

const { playlist, variant = "cover", disabled } = defineProps<{
  playlist: { isStarred: boolean };
  variant?: "cover" | "inline";
  disabled?: boolean;
}>();

const emit = defineEmits<{
  favorite: [];
}>();

// Cover floats over artwork and reveals on card hover; inline is a static
// toolbar control sized to match its row neighbours.
const containerClass = computed(() => {
  if (variant === "inline") {
    return "relative size-10";
  }

  return "absolute top-2 right-2 z-10 h-8 w-8 translate-y-1 opacity-0 group-hover:translate-y-0 group-hover:opacity-100 focus-visible:translate-y-0 focus-visible:opacity-100 data-[starred=true]:translate-y-0 data-[starred=true]:opacity-100";
});

const iconBoxClass = computed(() => (variant === "inline" ? "h-5 w-5" : "h-4 w-4"));
</script>

<style scoped>
.fav-ring,
.fav-dot {
  opacity: 0;
}

.fav-enter .fav-icon {
  animation: fav-pop 500ms cubic-bezier(0.34, 1.56, 0.64, 1) both;
}

.fav-enter .fav-ring {
  animation: fav-ring 500ms ease-out both;
}

.fav-enter .fav-dot {
  animation: fav-dot 450ms ease-out both;
}

@keyframes fav-pop {
  0% {
    transform: scale(0.3) rotate(-24deg);
  }
  45% {
    transform: scale(1.4) rotate(8deg);
  }
  70% {
    transform: scale(0.9) rotate(-3deg);
  }
  100% {
    transform: scale(1) rotate(0);
  }
}

@keyframes fav-ring {
  0% {
    transform: scale(0.4);
    opacity: 0.7;
  }
  100% {
    transform: scale(2.2);
    opacity: 0;
  }
}

@keyframes fav-dot {
  0% {
    transform: rotate(var(--angle)) translateY(-6px) scale(1);
    opacity: 1;
  }
  100% {
    transform: rotate(var(--angle)) translateY(-22px) scale(0);
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .fav-enter .fav-icon,
  .fav-enter .fav-ring,
  .fav-enter .fav-dot {
    animation: none;
  }
}
</style>
