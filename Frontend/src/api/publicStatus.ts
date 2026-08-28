import type { ServiceType } from "@/enums";

import { apiClient } from "./client";

export type PublicServiceState = "Healthy" | "Degraded" | "Down";

export interface DailyStatusPoint {
  date: string; // "yyyy-MM-dd"
  status: PublicServiceState;
}

export interface DailyServiceStatus {
  serviceType: ServiceType;
  days: DailyStatusPoint[];
}

export interface PublicServiceCurrent {
  serviceType: ServiceType;
  status: PublicServiceState;
}

export const publicStatusApi = {
  // Anonymous endpoints — no auth required by design
  getCurrentStatus: async (): Promise<PublicServiceCurrent[]> => {
    const result = await apiClient.get<PublicServiceCurrent[]>("/status");
    return result.data;
  },

  getStatusHistory: async (days = 90): Promise<DailyServiceStatus[]> => {
    const result = await apiClient.get<DailyServiceStatus[]>("/status/history", {
      params: { days },
    });
    return result.data;
  },
};
