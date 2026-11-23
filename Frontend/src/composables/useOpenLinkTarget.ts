import { useRouter } from "vue-router";

import type { LinkTargetDto } from "@/api/links";

import { streamingApi } from "@/api/streaming";
import { useAppToast } from "@/composables/useAppToast";
import { type LinkDestination, linkDestination } from "@/features/notes/link-targets";
import { usePlayerStore } from "@/stores/stream-player";
import { useTabStore } from "@/stores/tab";

/** Follows a note's link to its target, wherever that target lives in the app. */
export const useOpenLinkTarget = () => {
  const router = useRouter();
  const tabStore = useTabStore();
  const playerStore = usePlayerStore();
  const toast = useAppToast("files");

  const hrefOf = (destination: LinkDestination): string => {
    if (destination.type === "route") return router.resolve(destination.to).href;
    if (destination.type === "play") return hrefOf(destination.fallback);

    if (destination.directoryId) {
      return router.resolve({ name: "dashboard", params: { dirId: destination.directoryId } }).href;
    }

    return router.resolve({ name: "dashboard" }).href;
  };

  const hrefFor = (target: LinkTargetDto | undefined, suffix = ""): string | undefined => {
    if (!target) return undefined;

    const destination = linkDestination(target, suffix);

    if (!destination) return undefined;

    return hrefOf(destination);
  };

  const go = (destination: LinkDestination) => {
    if (destination.type === "route") {
      void router.push(destination.to);
      return;
    }

    if (destination.type === "play") {
      void play(destination);
      return;
    }

    // A new tab, so the explorer tabs already open stay where the user left them.
    tabStore.createTab(destination.directoryId, destination.fileId, destination.page);
    void router.push({ name: "dashboard" });
  };

  // Only files converted for streaming can play; anything else opens where it lives.
  const play = async (destination: Extract<LinkDestination, { type: "play" }>) => {
    try {
      const file = await streamingApi.getStreamingFile(destination.fileId);

      if (file) {
        await playerStore.playNow([file], { startSeconds: destination.startSeconds });
        return;
      }

      toast.info("Not ready to play yet", "This file is still being prepared for streaming.");
    } catch (error) {
      toast.error("Couldn't start playback", error);
    }

    go(destination.fallback);
  };

  const open = (target: LinkTargetDto | undefined, suffix = "") => {
    if (!target) return;

    const destination = linkDestination(target, suffix);

    if (destination) go(destination);
  };

  return { hrefFor, open };
};
