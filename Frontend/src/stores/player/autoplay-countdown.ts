import { ref } from "vue";

export const VIDEO_AUTOPLAY_SECONDS = 5;

const TICK_MS = 1000;

export const createAutoplayCountdown = (onComplete: () => void) => {
  const seconds = ref<number | null>(null);
  let interval: ReturnType<typeof setInterval> | null = null;

  const clear = () => {
    if (interval !== null) {
      clearInterval(interval);
      interval = null;
    }
    seconds.value = null;
  };

  const start = (from: number = VIDEO_AUTOPLAY_SECONDS) => {
    clear();
    seconds.value = from;
    interval = setInterval(() => {
      const current = seconds.value;
      if (current === null) {
        clear();
        return;
      }
      if (current > 1) {
        seconds.value = current - 1;
        return;
      }
      clear();
      onComplete();
    }, TICK_MS);
  };

  return { seconds, start, clear };
};
