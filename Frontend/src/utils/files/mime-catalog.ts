import catalog from "@/generated/mime-catalog.json";

// The JSON is generated from the backend MIME tables by MimeCatalogTests, and the backend unit
// suite fails when it drifts. Read rules from here instead of hand-copying backend lists.

/** A policy file group name, exactly as the backend FileGroups defines it. */
export type PolicyFileGroup = keyof typeof catalog.groups;

interface MimeMatcherData {
  prefixes: readonly string[];
  types: readonly string[];
}

interface MimeMatcher {
  prefixes: readonly string[];
  types: ReadonlySet<string>;
}

const toMatcher = (data: MimeMatcherData): MimeMatcher => ({
  prefixes: data.prefixes,
  types: new Set(data.types),
});

const ALIASES: Readonly<Record<string, string>> = catalog.aliases;

const PREVIEW_WORKER = toMatcher(catalog.previewWorker);
const AUTO_TAG = toMatcher(catalog.autoTag);

/** Every policy group in backend declaration order. */
export const POLICY_FILE_GROUPS = Object.keys(catalog.groups) as PolicyFileGroup[];

const GROUP_MATCHERS = POLICY_FILE_GROUPS.map((group) => ({
  group,
  matcher: toMatcher(catalog.groups[group]),
}));

const matches = (matcher: MimeMatcher, canonical: string): boolean =>
  matcher.types.has(canonical) || matcher.prefixes.some((prefix) => canonical.startsWith(prefix));

/**
 * Mirrors the backend MimeAliases.Canonicalize: drops parameters, trims, lowercases and maps
 * aliases to their canonical form. Returns an empty string for blank input.
 */
export const canonicalizeMime = (mimeType: string | null | undefined): string => {
  if (!mimeType) return "";

  const bare = mimeType.split(";")[0] ?? "";
  const normalized = bare.trim().toLowerCase();

  return ALIASES[normalized] ?? normalized;
};

/**
 * True when a preview worker renders this type. Asking for its thumbnail queues the job, so only
 * these types should ever request one; everything else is guaranteed to come back empty.
 */
export const routesToPreviewWorker = (mimeType: string | null | undefined): boolean => {
  const canonical = canonicalizeMime(mimeType);

  return canonical.length > 0 && matches(PREVIEW_WORKER, canonical);
};

/** True when the auto-tag endpoint accepts this type. */
export const isAutoTagSupportedMime = (mimeType: string | null | undefined): boolean => {
  const canonical = canonicalizeMime(mimeType);

  return canonical.length > 0 && matches(AUTO_TAG, canonical);
};

/** The policy group that owns this type, or null when no group does. */
export const policyGroupOf = (mimeType: string | null | undefined): PolicyFileGroup | null => {
  const canonical = canonicalizeMime(mimeType);
  if (!canonical) return null;

  return GROUP_MATCHERS.find((entry) => matches(entry.matcher, canonical))?.group ?? null;
};
