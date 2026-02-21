import { watchEffect } from "vue";
import { useDark } from "@vueuse/core";
import {
  useSettingsStore,
  AVAILABLE_COLORS,
  AVAILABLE_BACKGROUNDS,
} from "@/stores/settings";

/** Convert a 6-digit hex color string to "r,g,b" */
function hexToRgbChannels(hex: string): string {
  const h = hex.replace("#", "");
  const r = parseInt(h.substring(0, 2), 16);
  const g = parseInt(h.substring(2, 4), 16);
  const b = parseInt(h.substring(4, 6), 16);
  return `${r},${g},${b}`;
}

function resolveAccentRGB(colorName: string): string {
  const match = AVAILABLE_COLORS.find((c) => c.name === colorName);
  const fallback = AVAILABLE_COLORS.find((c) => c.name === "amber")!;
  return match?.value ?? fallback.value;
}

/**
 * Call once at app root (e.g. app.vue or a layout).
 *
 * Uses VueUse's `useDark` as a first-class reactive source so that every
 * theme-affecting value — isDark, accentColor, backgroundColor,
 * backgroundImage, backgroundImageOpacity — is tracked inside a single
 * `watchEffect`. The effect re-runs synchronously whenever any of them
 * change, with no DOM sniffing or manual watcher wiring needed.
 */
export function useTheme() {
  const store = useSettingsStore();
  const isDark = useDark();

  watchEffect(() => {
    if (typeof document === "undefined") return;

    const root = document.documentElement;

    // ── Accent color ─────────────────────────────────────────────────────
    root.style.setProperty(
      "--ui-primary",
      `rgb(${resolveAccentRGB(store.accentColor)})`,
    );

    // ── Background ───────────────────────────────────────────────────────
    const preset =
      AVAILABLE_BACKGROUNDS.find((b) => b.name === store.backgroundColor) ??
      AVAILABLE_BACKGROUNDS[0];

    if (store.backgroundImage) {
      // Image mode — overlay the preset color on top of the image to keep
      // text readable. Opacity controls image show-through; the overlay
      // uses (1 - opacity) as its alpha so they always sum to 1.
      const alpha = Math.min(0.65, Math.max(0.1, store.backgroundImageOpacity));
      const overlayAlpha = parseFloat((1 - alpha).toFixed(2));
      const overlayHex = isDark.value
        ? preset.darkValue || "#09090b"
        : preset.lightValue || "#ffffff";
      const rgb = hexToRgbChannels(overlayHex);
      const overlay = `rgba(${rgb},${overlayAlpha})`;

      document.body.style.backgroundImage = `linear-gradient(${overlay}, ${overlay}), url(${store.backgroundImage})`;
      document.body.style.backgroundSize = "cover";
      document.body.style.backgroundAttachment = "fixed";
      document.body.style.backgroundRepeat = "no-repeat";
      document.body.style.backgroundPosition = "center";
      document.body.style.removeProperty("background-color");
      root.style.removeProperty("--ui-bg");
    } else {
      // Flat color mode — clear any leftover image styles first
      document.body.style.backgroundImage = "";
      document.body.style.backgroundSize = "";
      document.body.style.backgroundAttachment = "";
      document.body.style.backgroundRepeat = "";
      document.body.style.backgroundPosition = "";

      if (preset.name === "system" || !preset.lightValue) {
        document.body.style.removeProperty("background-color");
        root.style.removeProperty("--ui-bg");
      } else {
        const bgColor = isDark.value ? preset.darkValue : preset.lightValue;
        document.body.style.backgroundColor = bgColor;
        root.style.setProperty("--ui-bg", bgColor);
      }
    }
  });

  return { isDark };
}
