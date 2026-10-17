import type { ColorName, FontName } from "@/stores/settings";

import { apiClient } from "./client";

export type ToastLevel = "all" | "errors-only" | "silent";

export interface AppearanceSettings {
  accentColor: ColorName;
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
  getHomeTimeZones: async (): Promise<string[]> => {
    const result = await apiClient.get<string[]>("/settings/home/time-zones");

    return result.data;
  },

  uploadBackgroundImage: async (file: File): Promise<AppearanceSettings> => {
    const form = new FormData();
    form.append("file", file);

    // Drop the instance JSON content-type so the browser sets multipart
    // with its own boundary; the default transform would stringify the form.
    const result = await apiClient.post<AppearanceSettings>(
      "/settings/appearance/background-image",
      form,
      {
        transformRequest: [
          (payload, headers) => {
            headers?.delete("Content-Type");
            return payload;
          },
        ],
      },
    );

    return result.data;
  },

  deleteBackgroundImage: async (): Promise<void> => {
    await apiClient.delete("/settings/appearance/background-image");
  },

  getAppearance: async (): Promise<AppearanceSettings> => {
    const result = await apiClient.get<AppearanceSettings>("/settings/appearance");
    return result.data;
  },

  getBackgroundImageUrl: async (): Promise<string> => {
    const result = await apiClient.get<{ url: string }>(
      "/settings/appearance/background-image-url",
    );
    return result.data.url;
  },

  getBehavior: async (): Promise<BehaviorSettings> => {
    const result = await apiClient.get("/settings/behavior");
    return mapBehaviorFromServer(result.data);
  },

  updateAppearance: async (payload: AppearanceSettings): Promise<AppearanceSettings> => {
    const result = await apiClient.put<AppearanceSettings>("/settings/appearance", payload);
    return result.data;
  },

  updateBehavior: async (payload: BehaviorSettings): Promise<BehaviorSettings> => {
    const result = await apiClient.put("/settings/behavior", {
      ...payload,
      toastLevel: TOAST_LEVEL_TO_VALUE[payload.toastLevel],
    });
    return mapBehaviorFromServer(result.data);
  },
};
