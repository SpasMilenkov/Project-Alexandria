import { useLocalStorage } from "@vueuse/core";
import { type InjectionKey, type Ref, computed } from "vue";

import { useTheme } from "@/composables/useTheme";
import {
  type WrappedPalette,
  parseWrappedPalette,
  wrappedCssColors,
} from "@/utils/wrapped-palette.utils";

export const wrappedPaletteKey: InjectionKey<Readonly<Ref<WrappedPalette | null>>> =
  Symbol("wrapped-palette");

export const useWrappedPalette = () => {
  const stored = useLocalStorage("alexandria:wrapped-palette:v1", "");
  const { isDark } = useTheme();

  const palette = computed<WrappedPalette | null>({
    get: () => {
      try {
        return parseWrappedPalette(JSON.parse(stored.value));
      } catch {
        return null;
      }
    },

    set: (value) => {
      stored.value = value ? JSON.stringify(value) : "";
    },
  });

  const paletteStyle = computed(() => wrappedCssColors(isDark.value, palette.value));

  return { palette, paletteStyle };
};
