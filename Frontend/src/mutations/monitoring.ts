import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import type { ReportProblemPayload } from "@/api/monitoring";

import { monitoringApi } from "@/api/monitoring";

export const useReportProblem = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: (payload: ReportProblemPayload) => monitoringApi.reportProblem(payload),
    // The new report should surface in the admin log + health strip without a
    // manual refresh.
    onSettled() {
      queryCache.invalidateQueries({ key: ["monitoring", "events"] });
      queryCache.invalidateQueries({ key: ["monitoring", "status-history"] });
    },
  });
});

export const useResolveIncident = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: ({ id, note }: { id: string; note?: string }) =>
      monitoringApi.resolveIncident(id, note),
    onSettled() {
      // Resolved timestamps shift the uptime windows too, so the health strip
      // must recalculate.
      queryCache.invalidateQueries({ key: ["monitoring", "events"] });
      queryCache.invalidateQueries({ key: ["monitoring", "status-history"] });
      queryCache.invalidateQueries({ key: ["monitoring", "uptimes"] });
    },
  });
});
