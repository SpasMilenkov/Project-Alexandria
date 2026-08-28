import { useRouter } from "vue-router";

import type { ServiceType } from "@/enums";
import type { DeepLinkParams } from "@/utils/serviceDashboardRouting";

import {
  buildDeepLink,
  FALLBACK_ROUTE,
  SERVICE_DASHBOARD_ROUTES,
} from "@/utils/serviceDashboardRouting";

export const useServiceDashboardRouting = () => {
  const router = useRouter();

  // Routes for dashboards that are not built yet resolve to zero matched
  // records — those degrade to the generic filtered timeline (D12: never a
  // dead link).
  const goToServiceDetail = (service: ServiceType, params: DeepLinkParams = {}): void => {
    const targetPath = SERVICE_DASHBOARD_ROUTES[service] ?? FALLBACK_ROUTE;
    const resolved = router.resolve(targetPath);

    const isFallback = resolved.matched.length === 0 || targetPath === FALLBACK_ROUTE;
    const path = isFallback ? FALLBACK_ROUTE : targetPath;

    const query: Record<string, string> = {};
    if (isFallback) query.serviceType = String(service);
    // Shared dashboards (previews) use this to pick their half; single-service
    // dashboards simply ignore it per the deep-link contract.
    query.service = String(service);
    if (params.from) query.from = params.from;
    if (params.to) query.to = params.to;
    if (params.severity !== undefined) query.severity = String(params.severity);
    if (params.eventId) query.eventId = params.eventId;

    void router.push({ path, query });
  };

  return { goToServiceDetail };
};
