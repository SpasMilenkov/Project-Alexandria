import type { ColorName } from "@/stores/settings";

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
}

export interface BehaviorSettings {
  skipDeleteConfirmation: boolean;
  toastLevel: ToastLevel;
  allowAutoTagRegression: boolean;
}

export interface RequestUploadResponse {
  uploadUrl: string;
  objectKey: string;
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
});

export const settingsApi = {
  confirmBackgroundImageUpload: async (objectKey: string): Promise<AppearanceSettings> => {
    const result = await apiClient.put<AppearanceSettings>(
      "/settings/appearance/background-image/confirm",
      { objectKey },
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

  requestBackgroundImageUpload: async (): Promise<RequestUploadResponse> => {
    const result = await apiClient.post<RequestUploadResponse>(
      "/settings/appearance/background-image",
    );
    return result.data;
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

  uploadToS3: async (uploadUrl: string, file: File): Promise<void> => {
    await fetch(uploadUrl, {
      body: file,
      headers: { "Content-Type": file.type },
      method: "PUT",
    });
  },
};
