import { getIcon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { useDocumentVisibility } from "@vueuse/core";
import { computed, watch } from "vue";
import { useRoute } from "vue-router";

import type { OperationalEvent } from "@/api/monitoring";

import { useOnlineStatus } from "@/composables/useOnlineStatus";
import { resolveAccentRGB } from "@/composables/useTheme";
import { OperationalEventSeverity } from "@/enums";
import { PLACE_GLYPHS } from "@/features/site-icon/places";
import { buildSiteIconSvg, toDataUri } from "@/features/site-icon/render";
import {
  APP_NAME,
  type PlaybackActivity,
  type ServiceHealthCounts,
  resolveSiteIcon,
} from "@/features/site-icon/state";
import { statusHistory } from "@/queries/monitoring";
import { useAuthStore } from "@/stores/auth";
import { TAB_ICON_MATCH_ACCENT, useSettingsStore } from "@/stores/settings";
import { usePlayerStore } from "@/stores/stream-player";
import { useUploadActivityStore } from "@/stores/upload-activity";

const ICON_LINK_ID = "site-icon";

const countHealth = (events: OperationalEvent[]): ServiceHealthCounts => {
  // The newest event per service is its active incident, matching useServiceStatus.
  const latest = new Map(events.map((event) => [event.serviceType, event]));
  let down = 0;
  let degraded = 0;

  for (const event of latest.values()) {
    if (event.severity === OperationalEventSeverity.Failure) down++;
    else degraded++;
  }

  return { degraded, down };
};

const ensureIconLink = (): HTMLLinkElement => {
  const existing = document.getElementById(ICON_LINK_ID);

  if (existing instanceof HTMLLinkElement) return existing;

  const link = document.createElement("link");
  link.id = ICON_LINK_ID;
  link.rel = "icon";
  link.type = "image/svg+xml";
  document.head.appendChild(link);

  return link;
};

/**
 * Keeps the favicon and document title in step with the current page and whatever the
 * user has running. Call once at the app root.
 */
export const useSiteIcon = () => {
  const route = useRoute();
  const settings = useSettingsStore();
  const player = usePlayerStore();
  const auth = useAuthStore();
  const uploads = useUploadActivityStore();
  const { isOnline } = useOnlineStatus();
  const visibility = useDocumentVisibility();

  // Reads the shared incident cache for admins; the admin pages own the polling.
  const history = useQuery(() => ({
    ...statusHistory(),
    enabled: auth.isAdmin && auth.isSessionVerified,
  }));

  watch(visibility, (state) => {
    if (state === "visible") uploads.clearOutcome();
  });

  const playback = computed((): PlaybackActivity | null => {
    const file = player.activeFile;

    if (!file || player.playerDismissed || !player.isPlaying) return null;

    const name = file.title ?? file.fileName;
    const title = file.artist ? `${name} · ${file.artist}` : name;

    return { title };
  });

  const tabIconColorName = computed(() => {
    if (settings.tabIconColor === TAB_ICON_MATCH_ACCENT) return settings.accentColor;

    return settings.tabIconColor;
  });

  const health = computed((): ServiceHealthCounts | null => {
    if (!auth.isAdmin || !history.data.value) return null;

    return countHealth(history.data.value);
  });

  const icon = computed(() =>
    resolveSiteIcon({
      accentRgb: resolveAccentRGB(tabIconColorName.value),
      health: health.value,
      isOnline: isOnline.value,
      pageTitle: route.meta.title ?? APP_NAME,
      place: route.meta.siteIcon ?? "home",
      playback: playback.value,
      upload: uploads.current,
      uploadOutcome: uploads.outcome,
    }),
  );

  const svg = computed(() => {
    const state = icon.value;
    const glyph = getIcon(state.glyph) ?? getIcon(PLACE_GLYPHS.home);

    if (!glyph) return null;

    return buildSiteIconSvg(state, glyph);
  });

  // Comparing the SVG string means a percentage tick inside the same eighth never swaps the icon.
  watch(
    svg,
    (markup) => {
      if (markup) ensureIconLink().href = toDataUri(markup);
    },
    { immediate: true },
  );

  watch(
    () => icon.value.title,
    (title) => {
      document.title = title;
    },
    { immediate: true },
  );
};
