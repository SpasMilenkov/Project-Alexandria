import { useDark } from "@vueuse/core";
import { watchEffect } from "vue";

import { AVAILABLE_BACKGROUNDS, AVAILABLE_COLORS, useSettingsStore } from "@/stores/settings";

/** Convert a 6-digit hex color string to "r,g,b" */
const hexToRgbChannels = (hex: string): string => {
  const h = hex.replace("#", "");
  const r = parseInt(h.substring(0, 2), 16);
  const g = parseInt(h.substring(2, 4), 16);
  const b = parseInt(h.substring(4, 6), 16);
  return `${r},${g},${b}`;
};

const resolveAccentRGB = (colorName: string): string => {
  const match = AVAILABLE_COLORS.find((c) => c.name === colorName);
  const fallback = AVAILABLE_COLORS.find((c) => c.name === "amber")!;
  return match?.value ?? fallback.value;
};

/**
 * Call once at app root (e.g. app.vue or a layout).
 *
 * Uses VueUse's `useDark` as a first-class reactive source so that every
 * theme-affecting value — isDark, accentColor, backgroundColor,
 * backgroundImage, backgroundImageOpacity — is tracked inside a single
 * `watchEffect`. The effect re-runs synchronously whenever any of them
 * change, with no DOM sniffing or manual watcher wiring needed.
 */

const resolvePreset = (name: string) =>
  AVAILABLE_BACKGROUNDS.find((b) => b.name === name) ?? AVAILABLE_BACKGROUNDS[0];

const applyImageBackground = (
  preset: (typeof AVAILABLE_BACKGROUNDS)[0],
  opts: {
    image: string;
    opacity: number;
    isDark: boolean;
  },
) => {
  const alpha = Math.min(0.65, Math.max(0.1, opts.opacity));
  const overlayAlpha = parseFloat((1 - alpha).toFixed(2));
  const overlayHex = opts.isDark ? preset.darkValue || "#09090b" : preset.lightValue || "#ffffff";
  const overlay = `rgba(${hexToRgbChannels(overlayHex)},${overlayAlpha})`;

  Object.assign(document.body.style, {
    backgroundAttachment: "fixed",
    backgroundImage: `linear-gradient(${overlay}, ${overlay}), url(${opts.image})`,
    backgroundPosition: "center",
    backgroundRepeat: "no-repeat",
    backgroundSize: "cover",
  });

  document.body.style.removeProperty("background-color");
  document.documentElement.style.removeProperty("--ui-bg");
};

const applyFlatBackground = (preset: (typeof AVAILABLE_BACKGROUNDS)[0], isDark: boolean) => {
  Object.assign(document.body.style, {
    backgroundAttachment: "",
    backgroundImage: "",
    backgroundPosition: "",
    backgroundRepeat: "",
    backgroundSize: "",
  });

  if (preset.name === "system" || !preset.lightValue) {
    document.body.style.removeProperty("background-color");
    document.documentElement.style.removeProperty("--ui-bg");
    return;
  }

  const bgColor = isDark ? preset.darkValue : preset.lightValue;
  document.body.style.backgroundColor = bgColor;
  document.documentElement.style.setProperty("--ui-bg", bgColor);
};

export const useTheme = () => {
  const store = useSettingsStore();
  const isDark = useDark();

  watchEffect(() => {
    if (typeof document === "undefined") return;

    document.documentElement.style.setProperty(
      "--ui-primary",
      `rgb(${resolveAccentRGB(store.accentColor)})`,
    );

    const preset = resolvePreset(store.backgroundColor);

    if (store.backgroundImage) {
      applyImageBackground(preset, {
        image: store.backgroundImage,
        isDark: isDark.value,
        opacity: store.backgroundImageOpacity,
      });
    } else {
      applyFlatBackground(preset, isDark.value);
    }
  });

  return { isDark };
};
