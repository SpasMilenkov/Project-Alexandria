import { defineQueryOptions } from "@pinia/colada";

import { activityApi } from "@/api/activity";
import { publicStatusApi } from "@/api/publicStatus";
import { SortDirection } from "@/enums/SortDirection";

interface HomeWidgetSession {
  userId: string;
  epoch: number;
}

export const HOME_STATUS_ACTIVITY_QUERY_KEYS = {
  statusOwner: (userId: string) => ["home", "public-status", userId],
  activityOwner: (userId: string) => ["activity", "user", "home", userId],
};

export const homeSystemStatus = defineQueryOptions(
  (params: HomeWidgetSession & { history: boolean }) => ({
    key: [
      ...HOME_STATUS_ACTIVITY_QUERY_KEYS.statusOwner(params.userId),
      params.epoch,
      params.history,
    ],
    enabled: Boolean(params.userId),
    staleTime: 30_000,
    query: async () => {
      if (params.history) {
        return {
          ...params,
          current: null,
          historyDays: await publicStatusApi.getStatusHistory(30),
        };
      }

      return { ...params, current: await publicStatusApi.getCurrentStatus(), historyDays: null };
    },
  }),
);

export const homeRecentActivity = defineQueryOptions((session: HomeWidgetSession) => ({
  key: [...HOME_STATUS_ACTIVITY_QUERY_KEYS.activityOwner(session.userId), session.epoch],
  enabled: Boolean(session.userId),
  staleTime: 30_000,
  query: async () => ({
    ...session,
    value: await activityApi.getUserActivity({
      userId: session.userId,
      page: 1,
      pageSize: 20,
      sortBy: "timestamp",
      sortDirection: SortDirection.Desc,
    }),
  }),
}));
