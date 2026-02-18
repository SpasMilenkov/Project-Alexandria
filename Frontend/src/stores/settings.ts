import { defineStore, acceptHMRUpdate } from "pinia";
import { computed, ref, watch } from "vue";

export interface UserSettings {
  accentColor: string;
  gridIconSize: number;
  listIconSize: number;
  skipDeleteConfirmation: boolean;
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
const DEFAULT_SKIP_DELETE_CONFIRMATION = false;
const DEFAULT_UI_STATE = {
  isAppearanceSectionOpen: true,
  isBehaviorSectionOpen: true,
};

export interface UserSettings {
  accentColor: string;
  gridIconSize: number;
  listIconSize: number;
  skipDeleteConfirmation: boolean;
  toastLevel: ToastLevel;
}

export type ToastLevel = "all" | "errors-only" | "silent";

export const TOAST_LEVELS = [
  {
    value: "all",
    label: "Tell me everything",
    description: "Success, errors, info — the full play-by-play",
    icon: "mdi:bell-ring",
  },
  {
    value: "errors-only",
    label: "Only when it hurts",
    description: "Stay quiet unless something actually goes wrong",
    icon: "mdi:bell-alert",
  },
  {
    value: "silent",
    label: "Zen mode",
    description:
      "No notifications at all. You didn't see anything (by design this will not disable notifications for critical operations such as login, uploads and file restoration)",
    icon: "mdi:bell-off",
  },
] as const;

export const useSettingsStore = defineStore(
  "settings",
  () => {
    // State
    const accentColor = ref<string>(DEFAULT_ACCENT_COLOR);
    const gridIconSize = ref(DEFAULT_GRID_ICON_SIZE);
    const listIconSize = ref(DEFAULT_LIST_ICON_SIZE);
    const skipDeleteConfirmation = ref(DEFAULT_SKIP_DELETE_CONFIRMATION);

    // UI State (for collapsible sections)
    const isAppearanceSectionOpen = ref(
      DEFAULT_UI_STATE.isAppearanceSectionOpen,
    );
    const isBehaviorSectionOpen = ref(DEFAULT_UI_STATE.isBehaviorSectionOpen);
    const DEFAULT_TOAST_LEVEL: ToastLevel = "all";
    const toastLevel = ref<ToastLevel>(DEFAULT_TOAST_LEVEL);

    // Get RGB value for a color name
    const getColorRGB = (colorName: string): string => {
      const color = AVAILABLE_COLORS.find((c) => c.name === colorName);
      return (
        color?.value ||
        AVAILABLE_COLORS.find((c) => c.name === DEFAULT_ACCENT_COLOR)!.value
      );
    };
    const setToastLevel = (level: ToastLevel) => {
      toastLevel.value = level;
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
      { immediate: true },
    );

    // Getters
    const getSettings = computed(
      (): UserSettings => ({
        accentColor: accentColor.value,
        gridIconSize: gridIconSize.value,
        listIconSize: listIconSize.value,
        skipDeleteConfirmation: skipDeleteConfirmation.value,
      }),
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

    const setSkipDeleteConfirmation = (value: boolean) => {
      skipDeleteConfirmation.value = value;
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
      if (settings.skipDeleteConfirmation !== undefined) {
        setSkipDeleteConfirmation(settings.skipDeleteConfirmation);
      }
      if (settings.toastLevel !== undefined) {
        setToastLevel(settings.toastLevel);
      }
    };

    const resetSettings = () => {
      accentColor.value = DEFAULT_ACCENT_COLOR;
      gridIconSize.value = DEFAULT_GRID_ICON_SIZE;
      listIconSize.value = DEFAULT_LIST_ICON_SIZE;
      skipDeleteConfirmation.value = DEFAULT_SKIP_DELETE_CONFIRMATION;
    };

    const resetAppearanceSettings = () => {
      accentColor.value = DEFAULT_ACCENT_COLOR;
      gridIconSize.value = DEFAULT_GRID_ICON_SIZE;
      listIconSize.value = DEFAULT_LIST_ICON_SIZE;
    };

    const resetBehaviorSettings = () => {
      skipDeleteConfirmation.value = DEFAULT_SKIP_DELETE_CONFIRMATION;
      toastLevel.value = DEFAULT_TOAST_LEVEL; // 👈
    };

    const setAppearanceSectionOpen = (value: boolean) => {
      isAppearanceSectionOpen.value = value;
    };

    const setBehaviorSectionOpen = (value: boolean) => {
      isBehaviorSectionOpen.value = value;
    };

    const resetUIState = () => {
      isAppearanceSectionOpen.value = DEFAULT_UI_STATE.isAppearanceSectionOpen;
      isBehaviorSectionOpen.value = DEFAULT_UI_STATE.isBehaviorSectionOpen;
    };

    return {
      // State
      accentColor,
      gridIconSize,
      listIconSize,
      skipDeleteConfirmation,
      toastLevel,
      // UI State
      isAppearanceSectionOpen,
      isBehaviorSectionOpen,
      // Getters
      getSettings,
      getColorRGBValue,
      // Constants
      AVAILABLE_COLORS,
      // Actions
      setAccentColor,
      setGridIconSize,
      setListIconSize,
      setSkipDeleteConfirmation,
      updateSettings,
      resetSettings,
      resetAppearanceSettings,
      resetBehaviorSettings,
      setToastLevel,
      // UI Actions
      setAppearanceSectionOpen,
      setBehaviorSectionOpen,
      resetUIState,
    };
  },
  { persist: true },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useSettingsStore, import.meta.hot));
}
