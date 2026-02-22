import { defineStore, acceptHMRUpdate } from "pinia";
import { computed, ref } from "vue";

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

export interface BackgroundPreset {
  name: string;
  label: string;
  description: string;
  mode: "light" | "dark" | "both";
  lightValue: string;
  darkValue: string;
  lightSwatch: string;
  darkSwatch: string;
}

export const AVAILABLE_BACKGROUNDS: BackgroundPreset[] = [
  {
    name: "system",
    label: "Default",
    description: "Your theme's default background",
    mode: "both",
    lightValue: "",
    darkValue: "",
    lightSwatch: "#ffffff",
    darkSwatch: "#09090b",
  },
  {
    name: "parchment",
    label: "Parchment",
    description: "Warm aged paper",
    mode: "light",
    lightValue: "#faf7f0",
    darkValue: "#1c1814",
    lightSwatch: "#faf7f0",
    darkSwatch: "#1c1814",
  },
  {
    name: "cream",
    label: "Cream",
    description: "Soft off-white",
    mode: "light",
    lightValue: "#fdfcf8",
    darkValue: "#1a1a14",
    lightSwatch: "#fdfcf8",
    darkSwatch: "#1a1a14",
  },
  {
    name: "newsprint",
    label: "Newsprint",
    description: "Classic grey newsprint",
    mode: "light",
    lightValue: "#efece4",
    darkValue: "#141210",
    lightSwatch: "#efece4",
    darkSwatch: "#141210",
  },
  {
    name: "linen",
    label: "Linen",
    description: "Natural fabric tone",
    mode: "light",
    lightValue: "#f8f4ed",
    darkValue: "#1e1b16",
    lightSwatch: "#f8f4ed",
    darkSwatch: "#1e1b16",
  },
  {
    name: "midnight",
    label: "Midnight",
    description: "Deep cool black",
    mode: "dark",
    lightValue: "#f0f2f5",
    darkValue: "#08090d",
    lightSwatch: "#f0f2f5",
    darkSwatch: "#08090d",
  },
  {
    name: "charcoal",
    label: "Charcoal",
    description: "Warm dark grey",
    mode: "dark",
    lightValue: "#f2f2f0",
    darkValue: "#111110",
    lightSwatch: "#f2f2f0",
    darkSwatch: "#111110",
  },
  {
    name: "ink",
    label: "Ink",
    description: "Desaturated navy — like quality book covers",
    mode: "dark",
    lightValue: "#f0f2f6",
    darkValue: "#0b0d14",
    lightSwatch: "#f0f2f6",
    darkSwatch: "#0b0d14",
  },
  {
    name: "cool",
    label: "Cool",
    description: "Clean slate",
    mode: "both",
    lightValue: "#f4f6f9",
    darkValue: "#0f1117",
    lightSwatch: "#f4f6f9",
    darkSwatch: "#0f1117",
  },
];

export type BackgroundName = string;

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
      "No notifications at all. (Critical operations such as login, uploads and file restoration are always shown.)",
    icon: "mdi:bell-off",
  },
] as const;

export interface UserSettings {
  accentColor: string;
  backgroundColor: string;
  backgroundImage: string | null;
  backgroundImageOpacity: number;
  gridIconSize: number;
  listIconSize: number;
  skipDeleteConfirmation: boolean;
  toastLevel: ToastLevel;
}

const DEFAULT_ACCENT_COLOR = "amber";
const DEFAULT_BACKGROUND = "parchment";
const DEFAULT_BACKGROUND_IMAGE = null;
const DEFAULT_BACKGROUND_IMAGE_OPACITY = 0.35;
const DEFAULT_GRID_ICON_SIZE = 48;
const DEFAULT_LIST_ICON_SIZE = 20;
const DEFAULT_SKIP_DELETE_CONFIRMATION = false;
const DEFAULT_TOAST_LEVEL: ToastLevel = "all";
const DEFAULT_UI_STATE = {
  isAppearanceSectionOpen: true,
  isBehaviorSectionOpen: true,
};

/** Maximum allowed file size for a background image (2 MB in bytes) */
export const MAX_BACKGROUND_IMAGE_BYTES = 2 * 1024 * 1024;

export const useSettingsStore = defineStore(
  "settings",
  () => {
    const accentColor = ref<string>(DEFAULT_ACCENT_COLOR);
    const backgroundColor = ref<string>(DEFAULT_BACKGROUND);
    const backgroundImage = ref<string | null>(DEFAULT_BACKGROUND_IMAGE);
    const backgroundImageOpacity = ref<number>(
      DEFAULT_BACKGROUND_IMAGE_OPACITY,
    );
    const gridIconSize = ref(DEFAULT_GRID_ICON_SIZE);
    const listIconSize = ref(DEFAULT_LIST_ICON_SIZE);
    const skipDeleteConfirmation = ref(DEFAULT_SKIP_DELETE_CONFIRMATION);
    const toastLevel = ref<ToastLevel>(DEFAULT_TOAST_LEVEL);
    const isAppearanceSectionOpen = ref(
      DEFAULT_UI_STATE.isAppearanceSectionOpen,
    );
    const isBehaviorSectionOpen = ref(DEFAULT_UI_STATE.isBehaviorSectionOpen);

    // Getters
    const getSettings = computed(
      (): UserSettings => ({
        accentColor: accentColor.value,
        backgroundColor: backgroundColor.value,
        backgroundImage: backgroundImage.value,
        backgroundImageOpacity: backgroundImageOpacity.value,
        gridIconSize: gridIconSize.value,
        listIconSize: listIconSize.value,
        skipDeleteConfirmation: skipDeleteConfirmation.value,
        toastLevel: toastLevel.value,
      }),
    );

    const getCurrentBackgroundPreset = computed(
      () =>
        AVAILABLE_BACKGROUNDS.find((b) => b.name === backgroundColor.value) ??
        AVAILABLE_BACKGROUNDS[0],
    );
    const hasBackgroundImage = computed(() => !!backgroundImage.value);

    // Actions
    const setAccentColor = (v: string) => {
      if (AVAILABLE_COLORS.some((c) => c.name === v)) accentColor.value = v;
    };
    const setBackgroundColor = (v: string) => {
      if (AVAILABLE_BACKGROUNDS.some((b) => b.name === v))
        backgroundColor.value = v;
    };
    const setBackgroundImage = (v: string | null) => {
      backgroundImage.value = v;
    };
    const setBackgroundImageOpacity = (v: number) => {
      backgroundImageOpacity.value = Math.min(0.65, Math.max(0.1, v));
    };
    const clearBackgroundImage = () => {
      backgroundImage.value = null;
    };
    const setGridIconSize = (v: number) => {
      gridIconSize.value = Math.max(12, Math.min(64, v));
    };
    const setListIconSize = (v: number) => {
      listIconSize.value = Math.max(12, Math.min(64, v));
    };
    const setSkipDeleteConfirmation = (v: boolean) => {
      skipDeleteConfirmation.value = v;
    };
    const setToastLevel = (v: ToastLevel) => {
      toastLevel.value = v;
    };
    const setAppearanceSectionOpen = (v: boolean) => {
      isAppearanceSectionOpen.value = v;
    };
    const setBehaviorSectionOpen = (v: boolean) => {
      isBehaviorSectionOpen.value = v;
    };

    const updateSettings = (settings: Partial<UserSettings>) => {
      if (settings.accentColor !== undefined)
        setAccentColor(settings.accentColor);
      if (settings.backgroundColor !== undefined)
        setBackgroundColor(settings.backgroundColor);
      if (settings.backgroundImage !== undefined)
        setBackgroundImage(settings.backgroundImage);
      if (settings.backgroundImageOpacity !== undefined)
        setBackgroundImageOpacity(settings.backgroundImageOpacity);
      if (settings.gridIconSize !== undefined)
        setGridIconSize(settings.gridIconSize);
      if (settings.listIconSize !== undefined)
        setListIconSize(settings.listIconSize);
      if (settings.skipDeleteConfirmation !== undefined)
        setSkipDeleteConfirmation(settings.skipDeleteConfirmation);
      if (settings.toastLevel !== undefined) setToastLevel(settings.toastLevel);
    };

    const resetSettings = () => {
      accentColor.value = DEFAULT_ACCENT_COLOR;
      backgroundColor.value = DEFAULT_BACKGROUND;
      backgroundImage.value = DEFAULT_BACKGROUND_IMAGE;
      backgroundImageOpacity.value = DEFAULT_BACKGROUND_IMAGE_OPACITY;
      gridIconSize.value = DEFAULT_GRID_ICON_SIZE;
      listIconSize.value = DEFAULT_LIST_ICON_SIZE;
      skipDeleteConfirmation.value = DEFAULT_SKIP_DELETE_CONFIRMATION;
      toastLevel.value = DEFAULT_TOAST_LEVEL;
    };

    const resetAppearanceSettings = () => {
      accentColor.value = DEFAULT_ACCENT_COLOR;
      backgroundColor.value = DEFAULT_BACKGROUND;
      backgroundImage.value = DEFAULT_BACKGROUND_IMAGE;
      backgroundImageOpacity.value = DEFAULT_BACKGROUND_IMAGE_OPACITY;
      gridIconSize.value = DEFAULT_GRID_ICON_SIZE;
      listIconSize.value = DEFAULT_LIST_ICON_SIZE;
    };

    const resetBehaviorSettings = () => {
      skipDeleteConfirmation.value = DEFAULT_SKIP_DELETE_CONFIRMATION;
      toastLevel.value = DEFAULT_TOAST_LEVEL;
    };

    const resetUIState = () => {
      isAppearanceSectionOpen.value = DEFAULT_UI_STATE.isAppearanceSectionOpen;
      isBehaviorSectionOpen.value = DEFAULT_UI_STATE.isBehaviorSectionOpen;
    };

    return {
      accentColor,
      backgroundColor,
      backgroundImage,
      backgroundImageOpacity,
      gridIconSize,
      listIconSize,
      skipDeleteConfirmation,
      toastLevel,
      isAppearanceSectionOpen,
      isBehaviorSectionOpen,
      getSettings,
      getCurrentBackgroundPreset,
      hasBackgroundImage,
      AVAILABLE_COLORS,
      AVAILABLE_BACKGROUNDS,
      setAccentColor,
      setBackgroundColor,
      setBackgroundImage,
      setBackgroundImageOpacity,
      clearBackgroundImage,
      setGridIconSize,
      setListIconSize,
      setSkipDeleteConfirmation,
      setToastLevel,
      updateSettings,
      resetSettings,
      resetAppearanceSettings,
      resetBehaviorSettings,
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
