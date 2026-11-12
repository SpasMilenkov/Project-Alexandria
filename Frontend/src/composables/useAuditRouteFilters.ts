import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";

import { type AuditFilters, parseAuditQuery, toAuditRouteQuery } from "@/features/audit/query";

// The URL is the single home of a history page's filters, so links and reloads keep them.
export const useAuditRouteFilters = () => {
  const route = useRoute();
  const router = useRouter();

  const filters = computed<AuditFilters>({
    get: () => parseAuditQuery(route.query),
    set: (next) => {
      void router.replace({ query: toAuditRouteQuery(next) });
    },
  });

  const setPage = (page: number) => {
    filters.value = { ...filters.value, page };
  };

  return { filters, setPage };
};
