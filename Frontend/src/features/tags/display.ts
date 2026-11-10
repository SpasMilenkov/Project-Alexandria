import { FACET_LABELS, type TagDto } from "@/api/tag";
import { formatTimeAgo } from "@/utils/date-formatters";

export const tagBreadcrumb = (tag: TagDto): string | null => {
  if (!tag.isSystem) return null;

  const facet = tag.facet === null || tag.facet === undefined ? null : FACET_LABELS[tag.facet];
  const parts = [facet, tag.parentName].filter(Boolean);

  if (parts.length === 0) return null;

  return parts.join(" · ");
};

// The breadcrumb prefix is only worth showing when a description follows it;
// otherwise describeTag already falls back to the breadcrumb itself.
export const tagBreadcrumbPrefix = (tag: TagDto): string | null => {
  if (!tag.description) return null;

  return tagBreadcrumb(tag);
};

export const describeTag = (tag: TagDto): string => {
  if (tag.description) return tag.description;

  if (tag.isSystem) return tagBreadcrumb(tag) ?? "System vocabulary";

  return `Created ${formatTimeAgo(tag.createdAt)}`;
};
