import type { ServiceType } from "@/enums";

import { api } from "./client";

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
  getCurrentStatus: (): Promise<PublicServiceCurrent[]> =>
    api.get<PublicServiceCurrent[]>("/status"),

  getStatusHistory: (days = 90): Promise<DailyServiceStatus[]> =>
    api.get<DailyServiceStatus[]>("/status/history", {
      params: { days },
    }),
};
