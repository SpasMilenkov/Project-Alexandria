import { useSettingsStore } from "@/stores/settings";

const extractMessage = (err: any): string => {
  const data = err?.response?.data;
  if (!data) return err?.message ?? "Unknown error";
  if (data.errors && typeof data.errors === "object") {
    const messages = Object.values(data.errors as Record<string, string[]>)
      .flat()
      .filter(Boolean);
    if (messages.length > 0) return messages.join(" · ");
  }
  return data.message ?? data.error ?? err?.message ?? "Unknown error";
};

const extractErrorDescription = (descriptionOrErr: unknown) => {
  if (typeof descriptionOrErr === "string") return descriptionOrErr;
  if (descriptionOrErr !== null) return extractMessage(descriptionOrErr);
  return undefined;
};

export const useAppToast = () => {
  const toast = useToast();
  const settings = useSettingsStore();

  const level = () => settings.toastLevel;

  return {
    error(title: string, descriptionOrErr?: string | unknown) {
      if (level() === "silent") return;
      const description = extractErrorDescription(descriptionOrErr);
      toast.add({ color: "error", description, title });
    },

    info(title: string, description?: string) {
      if (level() !== "all") return;
      toast.add({ color: "info", description, title });
    },

    success(title: string, description?: string) {
      if (level() !== "all") return;
      toast.add({ color: "success", description, title });
    },

    warning(title: string, description?: string) {
      // warnings surface on both "all" and "errors-only"
      if (level() === "silent") return;
      toast.add({ color: "warning", description, title });
    },
  };
};
