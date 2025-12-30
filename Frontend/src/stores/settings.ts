import { defineStore, acceptHMRUpdate } from "pinia";
import { computed, ref, watch } from "vue";

export interface UserSettings {
  accentColor: string;
  gridIconSize: number;
  listIconSize: number;
}

// Available Tailwind/Nuxt UI colors
export const AVAILABLE_COLORS = [
  { name: "red", value: "239 68 68" },
  { name: "orange", value: "249 115 22" },
  { name: "amber", value: "245 158 11" },
  { name: "yellow", value: "234 179 8" },
  { name: "lime", value: "132 204 22" },
  { name: "green", value: "34 197 94" },
  { name: "emerald", value: "16 185 129" },
  { name: "teal", value: "20 184 166" },
  { name: "cyan", value: "6 182 212" },
  { name: "sky", value: "14 165 233" },
  { name: "blue", value: "59 130 246" },
  { name: "indigo", value: "99 102 241" },
  { name: "violet", value: "139 92 246" },
  { name: "purple", value: "168 85 247" },
  { name: "fuchsia", value: "217 70 239" },
  { name: "pink", value: "236 72 153" },
  { name: "rose", value: "244 63 94" },
] as const;

export type ColorName = (typeof AVAILABLE_COLORS)[number]["name"];

const DEFAULT_ACCENT_COLOR = "orange";
const DEFAULT_GRID_ICON_SIZE = 48;
const DEFAULT_LIST_ICON_SIZE = 20;

export const useSettingsStore = defineStore(
  "settings",
  () => {

    // State
    const accentColor = ref<string>(DEFAULT_ACCENT_COLOR);
    const gridIconSize = ref(DEFAULT_GRID_ICON_SIZE);
    const listIconSize = ref(DEFAULT_LIST_ICON_SIZE);

    // Get RGB value for a color name
    const getColorRGB = (colorName: string): string => {
      const color = AVAILABLE_COLORS.find((c) => c.name === colorName);
      return (
        color?.value ||
        AVAILABLE_COLORS.find((c) => c.name === DEFAULT_ACCENT_COLOR)!.value
      );
    };

    // Apply theme by updating CSS variables
    const applyTheme = () => {
      if (typeof document === "undefined") return;

      const root = document.documentElement;
      const rgb = getColorRGB(accentColor.value);

      // Nuxt UI uses these CSS variables for the primary color
      // Format: RGB values without 'rgb()' wrapper (e.g., "59 130 246")
      root.style.setProperty("--ui-primary", `rgb(${rgb})`);
    };
    // Watch for changes and apply/save
    watch(
      accentColor,
      () => {
        applyTheme();
      },
      { immediate: true }
    );

    // Getters
    const getSettings = computed(
      (): UserSettings => ({
        accentColor: accentColor.value,
        gridIconSize: gridIconSize.value,
        listIconSize: listIconSize.value,
      })
    );

    const getColorRGBValue = computed(() => getColorRGB(accentColor.value));

    // Actions
    const setAccentColor = (newColor: string) => {
      if (AVAILABLE_COLORS.some((c) => c.name === newColor)) {
        accentColor.value = newColor;
      }
    };

    const setGridIconSize = (newSize: number) => {
      gridIconSize.value = Math.max(12, Math.min(64, newSize));
    };

    const setListIconSize = (newSize: number) => {
      listIconSize.value = Math.max(12, Math.min(64, newSize));
    };

    const updateSettings = (settings: Partial<UserSettings>) => {
      if (settings.accentColor !== undefined) {
        setAccentColor(settings.accentColor);
      }
      if (settings.gridIconSize !== undefined) {
        setGridIconSize(settings.gridIconSize);
      }
      if (settings.listIconSize !== undefined) {
        setGridIconSize(settings.listIconSize);
      }
    };

    const resetSettings = () => {
      accentColor.value = DEFAULT_ACCENT_COLOR;
      gridIconSize.value = DEFAULT_GRID_ICON_SIZE;
      listIconSize.value = DEFAULT_LIST_ICON_SIZE;
    };

    return {
      // State
      accentColor,
      gridIconSize,
      listIconSize,
      // Getters
      getSettings,
      getColorRGBValue,
      // Constants
      AVAILABLE_COLORS,
      // Actions
      setAccentColor,
      setGridIconSize,
      setListIconSize,
      updateSettings,
      resetSettings,
    };
  },
  { persist: true }
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useSettingsStore, import.meta.hot));
}
