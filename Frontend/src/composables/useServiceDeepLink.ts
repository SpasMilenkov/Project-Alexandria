import { ref, watch } from "vue";
import { useRoute } from "vue-router";

import type { EventCalendarRange } from "@/api/monitoring";
import type { OperationalEventSeverity } from "@/enums";

import { parseDeepLinkQuery } from "@/utils/serviceDashboardRouting";

// Seeds an embedded incident feed from the shared deep-link contract. Every param is
// re-read on each navigation so a link without one clears the previous selection.
export const useServiceDeepLink = () => {
  const route = useRoute();

  const feedDateRange = ref<EventCalendarRange | null>(null);
  const initialSeverity = ref<OperationalEventSeverity | null>(null);
  const highlightId = ref<string | null>(null);

  watch(
    () => route.query,
    () => {
      const params = parseDeepLinkQuery(route.query);

      if (params.from && params.to) {
        feedDateRange.value = { from: params.from, to: params.to };
      } else {
        feedDateRange.value = null;
      }

      initialSeverity.value = params.severity ?? null;
      highlightId.value = params.eventId ?? null;
    },
    { immediate: true },
  );

  return { feedDateRange, highlightId, initialSeverity };
};
