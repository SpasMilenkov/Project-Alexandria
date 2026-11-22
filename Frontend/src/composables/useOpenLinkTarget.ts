import { useRouter } from "vue-router";

import type { LinkTargetDto } from "@/api/links";

import { linkDestination } from "@/features/notes/link-targets";
import { useTabStore } from "@/stores/tab";

/** Follows a note's link to its target, wherever that target lives in the app. */
export const useOpenLinkTarget = () => {
  const router = useRouter();
  const tabStore = useTabStore();

  const hrefFor = (target: LinkTargetDto | undefined): string | undefined => {
    if (!target) return undefined;

    const destination = linkDestination(target);

    if (!destination) return undefined;
    if (destination.type === "route") return router.resolve(destination.to).href;

    if (destination.directoryId) {
      return router.resolve({ name: "dashboard", params: { dirId: destination.directoryId } }).href;
    }

    return router.resolve({ name: "dashboard" }).href;
  };

  const open = (target: LinkTargetDto | undefined) => {
    if (!target) return;

    const destination = linkDestination(target);

    if (!destination) return;

    if (destination.type === "route") {
      void router.push(destination.to);
      return;
    }

    // A new tab, so the explorer tabs already open stay where the user left them.
    tabStore.createTab(destination.directoryId, destination.fileId);
    void router.push({ name: "dashboard" });
  };

  return { hrefFor, open };
};
