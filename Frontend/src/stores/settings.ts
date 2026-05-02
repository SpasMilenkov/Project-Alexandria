// oxlint-disable max-statements
import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";

import type { AppearanceSettings, BehaviorSettings } from "@/api/settings";

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
    darkSwatch: "#09090b",
    darkValue: "",
    description: "Your theme's default background",
    label: "Default",
    lightSwatch: "#ffffff",
    lightValue: "",
    mode: "both",
    name: "system",
  },
  {
    darkSwatch: "#1c1814",
    darkValue: "#1c1814",
    description: "Warm aged paper",
    label: "Parchment",
    lightSwatch: "#faf7f0",
    lightValue: "#faf7f0",
    mode: "light",
    name: "parchment",
  },
  {
    darkSwatch: "#1a1a14",
    darkValue: "#1a1a14",
    description: "Soft off-white",
    label: "Cream",
    lightSwatch: "#fdfcf8",
    lightValue: "#fdfcf8",
    mode: "light",
    name: "cream",
  },
  {
    darkSwatch: "#141210",
    darkValue: "#141210",
    description: "Classic grey newsprint",
    label: "Newsprint",
    lightSwatch: "#efece4",
    lightValue: "#efece4",
    mode: "light",
    name: "newsprint",
  },
  {
    darkSwatch: "#1e1b16",
    darkValue: "#1e1b16",
    description: "Natural fabric tone",
    label: "Linen",
    lightSwatch: "#f8f4ed",
    lightValue: "#f8f4ed",
    mode: "light",
    name: "linen",
  },
  {
    darkSwatch: "#08090d",
    darkValue: "#08090d",
    description: "Deep cool black",
    label: "Midnight",
    lightSwatch: "#f0f2f5",
    lightValue: "#f0f2f5",
    mode: "dark",
    name: "midnight",
  },
  {
    darkSwatch: "#111110",
    darkValue: "#111110",
    description: "Warm dark grey",
    label: "Charcoal",
    lightSwatch: "#f2f2f0",
    lightValue: "#f2f2f0",
    mode: "dark",
    name: "charcoal",
  },
  {
    darkSwatch: "#0b0d14",
    darkValue: "#0b0d14",
    description: "Desaturated navy — like quality book covers",
    label: "Ink",
    lightSwatch: "#f0f2f6",
    lightValue: "#f0f2f6",
    mode: "dark",
    name: "ink",
  },
  {
    darkSwatch: "#0f1117",
    darkValue: "#0f1117",
    description: "Clean slate",
    label: "Cool",
    lightSwatch: "#f4f6f9",
    lightValue: "#f4f6f9",
    mode: "both",
    name: "cool",
  },
];

export type BackgroundName = string;

export type ToastLevel = "all" | "errors-only" | "silent";

export const TOAST_LEVELS = [
  {
    description: "Success, errors, info — the full play-by-play",
    icon: "mdi:bell-ring",
    label: "Tell me everything",
    value: "all",
  },
  {
    description: "Stay quiet unless something actually goes wrong",
    icon: "mdi:bell-alert",
    label: "Only when it hurts",
    value: "errors-only",
  },
  {
    description:
      "No notifications at all. (Critical operations such as login, uploads and file restoration are always shown.)",
    icon: "mdi:bell-off",
    label: "Zen mode",
    value: "silent",
  },
] as const;

export interface UserSettings {
  accentColor: ColorName;
  backgroundColor: string;
  backgroundImageKey: string | null;
  backgroundImageUpdatedAt: string | null;
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

export const MAX_BACKGROUND_IMAGE_BYTES = 2 * 1024 * 1024;

export const useSettingsStore = defineStore(
  "settings",
  // oxlint-disable-next-line max-lines-per-function
  () => {
    const accentColor = ref<ColorName>(DEFAULT_ACCENT_COLOR);
    const backgroundColor = ref<string>(DEFAULT_BACKGROUND);
    const backgroundImageKey = ref<string | null>(null);
    const backgroundImageUpdatedAt = ref<string | null>(null);
    const backgroundImage = ref<string | null>(null); // Runtime only — SW URL
    const backgroundImageOpacity = ref<number>(DEFAULT_BACKGROUND_IMAGE_OPACITY);
    const gridIconSize = ref(DEFAULT_GRID_ICON_SIZE);
    const listIconSize = ref(DEFAULT_LIST_ICON_SIZE);
    const skipDeleteConfirmation = ref(DEFAULT_SKIP_DELETE_CONFIRMATION);
    const toastLevel = ref<ToastLevel>(DEFAULT_TOAST_LEVEL);
    const isAppearanceSectionOpen = ref(DEFAULT_UI_STATE.isAppearanceSectionOpen);
    const isBehaviorSectionOpen = ref(DEFAULT_UI_STATE.isBehaviorSectionOpen);

    // Getters
    const getSettings = computed(
      (): UserSettings => ({
        accentColor: accentColor.value,
        backgroundColor: backgroundColor.value,
        backgroundImageKey: backgroundImageKey.value,
        backgroundImageOpacity: backgroundImageOpacity.value,
        backgroundImageUpdatedAt: backgroundImageUpdatedAt.value,
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
    const hasBackgroundImage = computed(() => Boolean(backgroundImage.value));

    // Actions
    const setAccentColor = (v: ColorName) => {
      accentColor.value = v;
    };
    const setBackgroundColor = (v: string) => {
      if (AVAILABLE_BACKGROUNDS.some((b) => b.name === v)) {
        backgroundColor.value = v;
      }
    };
    const setBackgroundImage = (v: string | null) => {
      backgroundImage.value = v;
    };
    const setBackgroundImageOpacity = (v: number) => {
      backgroundImageOpacity.value = Math.min(0.65, Math.max(0.1, v));
    };
    const clearBackgroundImage = () => {
      backgroundImage.value = null;
      backgroundImageKey.value = null;
      backgroundImageUpdatedAt.value = null;
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

    const syncAppearanceFromServer = (appearance: AppearanceSettings) => {
      setAccentColor(appearance.accentColor);
      setBackgroundColor(appearance.backgroundColor);
      backgroundImageKey.value = appearance.backgroundImageKey;
      backgroundImageUpdatedAt.value = appearance.backgroundImageUpdatedAt;
      setBackgroundImageOpacity(appearance.backgroundImageOpacity);
      setGridIconSize(appearance.gridIconSize);
      setListIconSize(appearance.listIconSize);
    };

    const syncBehaviorFromServer = (behavior: BehaviorSettings) => {
      setSkipDeleteConfirmation(behavior.skipDeleteConfirmation);
      setToastLevel(behavior.toastLevel);
    };

    const syncFromServer = (appearance: AppearanceSettings, behavior: BehaviorSettings) => {
      syncAppearanceFromServer(appearance);
      syncBehaviorFromServer(behavior);
    };
    const updateSettings = (settings: Partial<UserSettings>) => {
      if (settings.accentColor !== undefined) {
        setAccentColor(settings.accentColor);
      }
      if (settings.backgroundColor !== undefined) {
        setBackgroundColor(settings.backgroundColor);
      }
      if (settings.backgroundImageOpacity !== undefined) {
        setBackgroundImageOpacity(settings.backgroundImageOpacity);
      }
      if (settings.gridIconSize !== undefined) {
        setGridIconSize(settings.gridIconSize);
      }
      if (settings.listIconSize !== undefined) {
        setListIconSize(settings.listIconSize);
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
      AVAILABLE_BACKGROUNDS,
      AVAILABLE_COLORS,
      accentColor,
      backgroundColor,
      backgroundImage,
      backgroundImageKey,
      backgroundImageOpacity,
      backgroundImageUpdatedAt,
      clearBackgroundImage,
      getCurrentBackgroundPreset,
      getSettings,
      gridIconSize,
      hasBackgroundImage,
      isAppearanceSectionOpen,
      isBehaviorSectionOpen,
      listIconSize,
      resetAppearanceSettings,
      resetBehaviorSettings,
      resetSettings,
      resetUIState,
      setAccentColor,
      setAppearanceSectionOpen,
      setBackgroundColor,
      setBackgroundImage,
      setBackgroundImageOpacity,
      setBehaviorSectionOpen,
      setGridIconSize,
      setListIconSize,
      setSkipDeleteConfirmation,
      setToastLevel,
      skipDeleteConfirmation,
      syncAppearanceFromServer,
      syncBehaviorFromServer,
      syncFromServer,
      toastLevel,
      updateSettings,
    };
  },
  {
    persist: {
      pick: [
        "accentColor",
        "backgroundColor",
        "backgroundImageKey",
        "backgroundImageUpdatedAt",
        "backgroundImageOpacity",
        "gridIconSize",
        "listIconSize",
        "skipDeleteConfirmation",
        "toastLevel",
        "isAppearanceSectionOpen",
        "isBehaviorSectionOpen",
      ],
    },
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useSettingsStore, import.meta.hot));
}
