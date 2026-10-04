import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";

import { useSettingsStore } from "@/stores/settings";

export type SettingsSectionId = "appearance" | "behavior" | "auto-tagging" | "auto-playlists";

export interface SettingsSearchEntry {
  section: SettingsSectionId;
  title: string;
  desc: string;
  keywords: string;
  value: () => string;
}

const setSectionOpen: Record<SettingsSectionId, (open: boolean) => void> = {
  appearance: (open) => useSettingsStore().setAppearanceSectionOpen(open),
  "auto-playlists": (open) => useSettingsStore().setAutoPlaylistsSectionOpen(open),
  "auto-tagging": (open) => useSettingsStore().setAutoTaggingSectionOpen(open),
  behavior: (open) => useSettingsStore().setBehaviorSectionOpen(open),
};

const flashSection = (id: string) => {
  const el = document.getElementById(id);
  if (!el) {
    return;
  }
  el.classList.remove("settings-flash");
  void el.offsetWidth;
  el.classList.add("settings-flash");
  window.setTimeout(() => el.classList.remove("settings-flash"), 1800);
};

const buildSearchIndex = (
  settingsStore: ReturnType<typeof useSettingsStore>,
): SettingsSearchEntry[] => [
  {
    desc: "Colors, glass effects and file tiles",
    keywords: "theme look colors glass display",
    section: "appearance",
    title: "Appearance",
    value: () => "",
  },
  {
    desc: "Accent color, background and backdrop image",
    keywords: "color theme primary highlight swatch",
    section: "appearance",
    title: "Accent color",
    value: () =>
      settingsStore.accentColor.charAt(0).toUpperCase() + settingsStore.accentColor.slice(1),
  },
  {
    desc: "Preset backgrounds for light and dark mode",
    keywords: "theme dark light parchment midnight charcoal ink cream linen newsprint",
    section: "appearance",
    title: "Background color",
    value: () => {
      if (settingsStore.backgroundImageKey) {
        return "Image";
      }
      const found = settingsStore.AVAILABLE_BACKGROUNDS.find(
        (bg) => bg.name === settingsStore.backgroundColor,
      );
      return found ? found.label : "";
    },
  },
  {
    desc: "Custom image behind the app",
    keywords: "wallpaper picture photo upload",
    section: "appearance",
    title: "Background image",
    value: () => (settingsStore.backgroundImageKey ? "Set" : "None"),
  },
  {
    desc: "Soften what shows through cards, bars and popovers",
    keywords: "frost frosted glass blur",
    section: "appearance",
    title: "Background blur",
    value: () => (settingsStore.frostEnabled ? "On" : "Off"),
  },
  {
    desc: "Let the background show through panels",
    keywords: "transparency translucent glass opacity",
    section: "appearance",
    title: "See-through panels",
    value: () => (settingsStore.transparencyEnabled ? "On" : "Off"),
  },
  {
    desc: "Image previews in the explorer grid",
    keywords: "preview images thumbnails grid",
    section: "appearance",
    title: "File thumbnails",
    value: () => (settingsStore.thumbnailsEnabled ? "On" : "Off"),
  },
  {
    desc: "Typeface used across the whole app",
    keywords: "font typeface text serif mono",
    section: "appearance",
    title: "Interface font",
    value: () => {
      const found = settingsStore.AVAILABLE_FONTS.find((f) => f.name === settingsStore.fontFamily);
      return found ? found.label : "";
    },
  },
  {
    desc: "How rounded buttons, inputs, cards and dialogs are",
    keywords: "radius corners rounded sharp soft full",
    section: "appearance",
    title: "Corner roundness",
    value: () => {
      if (settingsStore.cornerRadius <= 0) {
        return "Sharp";
      }
      if (settingsStore.cornerRadius <= 0.25) {
        return "Soft";
      }
      if (settingsStore.cornerRadius <= 0.5) {
        return "Round";
      }
      return "Full";
    },
  },
  {
    desc: "Confirmation prompts and notifications",
    keywords: "preferences prompts alerts",
    section: "behavior",
    title: "Behavior",
    value: () => "",
  },
  {
    desc: "Delete non-empty directories without prompting",
    keywords: "trash folder remove prompt warning",
    section: "behavior",
    title: "Skip delete confirmation",
    value: () => (settingsStore.skipDeleteConfirmation ? "On" : "Off"),
  },
  {
    desc: "Success, errors and info toasts",
    keywords: "toasts alerts zen quiet silent mute",
    section: "behavior",
    title: "Notification chatter",
    value: () => {
      if (settingsStore.toastLevel === "errors-only") {
        return "Errors only";
      }
      if (settingsStore.toastLevel === "silent") {
        return "Silent";
      }
      return "All";
    },
  },
  {
    desc: "What happens when tagging runs again",
    keywords: "tags genres moods",
    section: "auto-tagging",
    title: "Auto-tagging",
    value: () => "",
  },
  {
    desc: "Let a re-run lower tag confidence",
    keywords: "autotag verdict lower confidence",
    section: "auto-tagging",
    title: "Allow confidence regression",
    value: () => (settingsStore.allowAutoTagRegression ? "On" : "Off"),
  },
  {
    desc: "Let automatic processing replace existing metadata",
    keywords: "title artist album year genre corrections overwrite",
    section: "auto-tagging",
    title: "Allow automatic metadata overwrite",
    value: () => (settingsStore.allowAutomaticMetadataOverwrite ? "On" : "Off"),
  },
  {
    desc: "When tag and genre playlists get created",
    keywords: "playlist threshold count",
    section: "auto-playlists",
    title: "Auto-playlists",
    value: () => "",
  },
  {
    desc: "Groupings below this never become playlists",
    keywords: "threshold minimum tracks count",
    section: "auto-playlists",
    title: "Minimum tracks per tag or genre playlist",
    value: () => `${settingsStore.autoPlaylistMinTracks} tracks`,
  },
];

export const useSettingsSearch = () => {
  const settingsStore = useSettingsStore();

  const searchIndex = buildSearchIndex(settingsStore);
  const searchQuery = ref("");
  const searchOpen = ref(false);
  const searchActive = ref(0);
  const searchInput = ref<{ inputRef?: HTMLInputElement | null } | null>(null);
  const resultsAnchor = ref<{ top: number; left: number; width: number } | null>(null);

  const updateAnchor = () => {
    const input = searchInput.value?.inputRef;
    if (!input) {
      resultsAnchor.value = null;
      return;
    }
    const rect = input.getBoundingClientRect();
    resultsAnchor.value = {
      left: rect.left,
      top: rect.bottom + 8,
      width: rect.width,
    };
  };

  const resultsStyle = computed(() => {
    if (!resultsAnchor.value) {
      return { visibility: "hidden" as const };
    }
    return {
      left: `${resultsAnchor.value.left}px`,
      top: `${resultsAnchor.value.top}px`,
      width: `${resultsAnchor.value.width}px`,
    };
  });

  const searchTokens = computed(() =>
    searchQuery.value.trim().toLowerCase().split(/\s+/u).filter(Boolean),
  );

  const searchResults = computed(() => {
    const tokens = searchTokens.value;
    if (tokens.length === 0) {
      return [];
    }
    const scored = searchIndex
      .map((entry) => {
        const title = entry.title.toLowerCase();
        const hay = `${title} ${entry.keywords} ${entry.desc.toLowerCase()}`;
        let score = 0;
        for (const token of tokens) {
          if (!hay.includes(token)) {
            return { entry, score: 0 };
          }
          if (title.startsWith(token)) {
            score += 4;
          } else if (title.includes(` ${token}`)) {
            score += 3;
          } else if (title.includes(token)) {
            score += 2;
          } else {
            score += 1;
          }
        }
        return { entry, score };
      })
      .filter((hit) => hit.score > 0)
      .sort((a, b) => b.score - a.score)
      .slice(0, 8);
    return scored.map((hit) => hit.entry);
  });

  const escapeHtml = (text: string) =>
    text.replace(/&/gu, "&amp;").replace(/</gu, "&lt;").replace(/>/gu, "&gt;");

  const highlight = (text: string) => {
    const tokens = searchTokens.value;
    const lower = text.toLowerCase();
    const flags = new Array<boolean>(text.length).fill(false);
    tokens.forEach((token) => {
      let i = lower.indexOf(token);
      while (i !== -1) {
        for (let k = i; k < i + token.length; k += 1) {
          flags[k] = true;
        }
        i = lower.indexOf(token, i + token.length);
      }
    });
    let out = "";
    let inMark = false;
    for (let k = 0; k < text.length; k += 1) {
      if (flags[k] && !inMark) {
        out += "<mark>";
        inMark = true;
      }
      if (!flags[k] && inMark) {
        out += "</mark>";
        inMark = false;
      }
      out += escapeHtml(text[k]);
    }
    if (inMark) {
      out += "</mark>";
    }
    return out;
  };

  const goToEntry = (entry: SettingsSearchEntry | undefined) => {
    if (!entry) {
      return;
    }
    searchOpen.value = false;
    searchQuery.value = "";
    searchInput.value?.inputRef?.blur();
    setSectionOpen[entry.section](true);
    nextTick().then(() => {
      document.getElementById(entry.section)?.scrollIntoView({ behavior: "smooth" });
      flashSection(entry.section);
    });
  };

  const onSearchKeydown = (event: KeyboardEvent) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (searchResults.value.length === 0) {
        return;
      }
      const step = event.key === "ArrowDown" ? 1 : -1;
      searchActive.value =
        (searchActive.value + step + searchResults.value.length) % searchResults.value.length;
    } else if (event.key === "Enter") {
      event.preventDefault();
      goToEntry(searchResults.value[searchActive.value]);
    } else if (event.key === "Escape") {
      if (searchOpen.value) {
        searchOpen.value = false;
      } else {
        searchQuery.value = "";
      }
    }
  };

  // Capture phase so this wins over the global shortcuts-modal "/" handler:
  // on the settings page the local search owns the key.
  const onSlashShortcut = (event: KeyboardEvent) => {
    if (event.key !== "/" || event.metaKey || event.ctrlKey || event.altKey) {
      return;
    }
    const target = event.target as HTMLElement | null;
    const tag = target ? target.tagName : "";
    if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    searchInput.value?.inputRef?.focus();
  };

  const repositionOnScroll = () => {
    if (searchOpen.value) {
      updateAnchor();
    }
  };

  onMounted(() => {
    window.addEventListener("keydown", onSlashShortcut, true);
  });

  onBeforeUnmount(() => {
    window.removeEventListener("keydown", onSlashShortcut, true);
    window.removeEventListener("scroll", repositionOnScroll, true);
    window.removeEventListener("resize", repositionOnScroll);
  });

  watch(searchQuery, () => {
    searchOpen.value = true;
    searchActive.value = 0;
  });

  watch(searchOpen, (open) => {
    if (open) {
      updateAnchor();
      window.addEventListener("scroll", repositionOnScroll, true);
      window.addEventListener("resize", repositionOnScroll);
    } else {
      window.removeEventListener("scroll", repositionOnScroll, true);
      window.removeEventListener("resize", repositionOnScroll);
    }
  });

  return {
    goToEntry,
    highlight,
    onSearchKeydown,
    resultsStyle,
    searchActive,
    searchInput,
    searchOpen,
    searchQuery,
    searchResults,
  };
};
