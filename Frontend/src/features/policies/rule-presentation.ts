import { PolicyActionType, PolicyTriggerType } from "@/api/policy";

interface ActionPresentation {
  icon: string;
  colorClass: string;
  label: string;
}

const FALLBACK: ActionPresentation = {
  colorClass: "bg-neutral-200 dark:bg-neutral-700 text-neutral-600 dark:text-neutral-400",
  icon: "i-lucide-settings",
  label: "Rule",
};

const ACTIONS: Record<PolicyActionType, ActionPresentation> = {
  [PolicyActionType.Transcode]: {
    colorClass: "bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400",
    icon: "i-lucide-film",
    label: "Transcode media",
  },
  [PolicyActionType.AutoTag]: {
    colorClass: "bg-amber-100 dark:bg-amber-900/30 text-amber-600 dark:text-amber-400",
    icon: "i-lucide-tag",
    label: "Auto-tag",
  },
  [PolicyActionType.Backup]: {
    colorClass: "bg-green-100 dark:bg-green-900/30 text-green-600 dark:text-green-400",
    icon: "i-lucide-cloud-upload",
    label: "Backup",
  },
};

export const policyActionPresentation = (actionType: PolicyActionType): ActionPresentation =>
  ACTIONS[actionType] ?? FALLBACK;

export const policyTriggerLabel = (
  triggerType: PolicyTriggerType,
  triggerValue: string,
): string => {
  if (triggerType === PolicyTriggerType.AnyFile) return "All eligible files";
  if (triggerType === PolicyTriggerType.FileGroup) {
    return triggerValue.split(";").filter(Boolean).join(", ");
  }

  return triggerValue;
};
