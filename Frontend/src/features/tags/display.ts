import { FACET_LABELS, type TagDto } from "@/api/tag";
import { formatDate } from "@/utils/date-formatters";

export const describeTag = (tag: TagDto): string => {
  if (tag.isSystem) {
    const facet = tag.facet === null || tag.facet === undefined ? null : FACET_LABELS[tag.facet];
    const parts = [facet, tag.parentName].filter(Boolean);

    if (parts.length > 0) return parts.join(" · ");

    return "System vocabulary";
  }

  if (tag.description) return tag.description;

  return `Created ${formatDate(tag.createdAt).toLowerCase()}`;
};
