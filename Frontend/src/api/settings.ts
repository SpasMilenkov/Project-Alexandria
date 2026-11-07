import type { ColorName, FontName, TabIconColor } from "@/stores/settings";

import { api } from "./client";

export type ToastLevel = "all" | "errors-only" | "silent";

export interface AppearanceSettings {
  accentColor: ColorName;
  tabIconColor: TabIconColor;
  backgroundColor: string;
  backgroundImageKey: string | null;
  // ISO 8601
  backgroundImageUpdatedAt: string | null;
  backgroundImageOpacity: number;
  gridIconSize: number;
  listIconSize: number;
  backgroundBlurEnabled: boolean;
  backgroundBlurAmount: number;
  disableBlurOnMobile: boolean;
  transparencyEnabled: boolean;
  surfaceOpacity: number;
  thumbnailsEnabled: boolean;
  fontFamily: FontName;
  cornerRadius: number;
}

export interface BehaviorSettings {
  skipDeleteConfirmation: boolean;
  toastLevel: ToastLevel;
  allowAutoTagRegression: boolean;
  allowAutomaticMetadataOverwrite: boolean;
  autoPlaylistMinTracks: number;
}

// Backend `ToastLevel` is a numeric enum (All=0, ErrorsOnly=1, Silent=2) serialized without a
// JsonStringEnumConverter, so it travels as a number in both directions.
const TOAST_LEVEL_TO_VALUE: Record<ToastLevel, number> = {
  all: 0,
  "errors-only": 1,
  silent: 2,
};

const toastLevelFromServer: Record<number, ToastLevel> = {
  0: "all",
  1: "errors-only",
  2: "silent",
};

const mapBehaviorFromServer = (raw: any): BehaviorSettings => ({
  skipDeleteConfirmation: raw.skipDeleteConfirmation,
  toastLevel: toastLevelFromServer[raw.toastLevel] ?? "all",
  allowAutoTagRegression: raw.allowAutoTagRegression,
  allowAutomaticMetadataOverwrite: raw.allowAutomaticMetadataOverwrite ?? false,
  autoPlaylistMinTracks: raw.autoPlaylistMinTracks ?? 10,
});

export const settingsApi = {
  getHomeTimeZones: (): Promise<string[]> => api.get<string[]>("/settings/home/time-zones"),

  uploadBackgroundImage: (file: File): Promise<AppearanceSettings> => {
    const form = new FormData();
    form.append("file", file);

    return api.post<AppearanceSettings>("/settings/appearance/background-image", form);
  },

  deleteBackgroundImage: async (): Promise<void> => {
    await api.delete("/settings/appearance/background-image");
  },

  getAppearance: (): Promise<AppearanceSettings> =>
    api.get<AppearanceSettings>("/settings/appearance"),

  getBackgroundImageUrl: async (): Promise<string> => {
    const result = await api.get<{ url: string }>("/settings/appearance/background-image-url");
    return result.url;
  },

  getBehavior: async (): Promise<BehaviorSettings> => {
    const result = await api.get("/settings/behavior");
    return mapBehaviorFromServer(result);
  },

  updateAppearance: (payload: AppearanceSettings): Promise<AppearanceSettings> =>
    api.put<AppearanceSettings>("/settings/appearance", payload),

  updateBehavior: async (payload: BehaviorSettings): Promise<BehaviorSettings> => {
    const result = await api.put("/settings/behavior", {
      ...payload,
      toastLevel: TOAST_LEVEL_TO_VALUE[payload.toastLevel],
    });
    return mapBehaviorFromServer(result);
  },
};
