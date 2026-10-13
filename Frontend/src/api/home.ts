import type { HomeLayoutKind, HomeWidgetType } from "@/enums/home-widget";
import type { HomeWidget, HomeWidgetOptions } from "@/types/home";

import { homeBoardToSettings } from "@/utils/home-widget-persistence";

import { apiClient } from "./client";

export interface HomeWidgetRow {
  id: string;
  layout: HomeLayoutKind;
  type: HomeWidgetType;
  x: number;
  y: number;
  width: number;
  height: number;
  showTitle: boolean | null;
  optionsVersion: number;
  options: Partial<HomeWidgetOptions>;
}

export interface HomeBoard {
  initialized: boolean;
  hasMobileLayout: boolean;
  preferences: {
    useSeparateMobileLayout: boolean;
    showWidgetTitles: boolean;
    showHeaderGreeting?: boolean;
  };
  widgets: HomeWidgetRow[];
}

export type HomeWidgetFieldChanges = Partial<
  Pick<HomeWidget, "x" | "y" | "width" | "height" | "showTitle">
> & { options?: Partial<HomeWidgetOptions> };

export interface HomeBoardChanges {
  creates: HomeWidgetRow[];
  updates: { id: string; changes: HomeWidgetFieldChanges }[];
  deletes: string[];
  preferences?: Partial<HomeBoard["preferences"]>;
  initializeMobileLayout: boolean;
}

export const homeApi = {
  getBoard: async () => {
    const response = await apiClient.get<HomeBoard>("/home/widgets");

    return homeBoardToSettings(response.data);
  },

  saveChanges: async (changes: HomeBoardChanges) => {
    const response = await apiClient.patch<HomeBoard>("/home/widgets", changes);

    return homeBoardToSettings(response.data);
  },

  resetBoard: async () => {
    const response = await apiClient.delete<HomeBoard>("/home/widgets");

    return homeBoardToSettings(response.data);
  },
};
