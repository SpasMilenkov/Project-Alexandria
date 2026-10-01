import { ref } from "vue";

export interface VariantTrack {
  id: number;
  height: number | null;
  width: number | null;
  bandwidth: number;
  videoCodec: string | null;
  audioCodec: string | null;
  active: boolean;
}

export const createTransport = () => {
  const currentTime = ref(0);
  const duration = ref(0);
  const isPlaying = ref(false);
  const variantTracks = ref<VariantTrack[]>([]);
  const activeVariantId = ref<number | null>(null);
  const abrEnabled = ref(true);
  const playbackRate = ref(1);
  const volume = ref(0.5);

  return {
    currentTime,
    duration,
    isPlaying,
    variantTracks,
    activeVariantId,
    abrEnabled,
    playbackRate,
    volume,
    resetPosition: () => {
      currentTime.value = 0;
      duration.value = 0;
      isPlaying.value = false;
    },
  };
};

export const PREVIOUS_RESTART_SECONDS = 3;
