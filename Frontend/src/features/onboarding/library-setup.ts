import type { FinishTourRequest } from "@/api/users";

import { AudioRung, VideoRung } from "@/api/policy";
import { LibraryFolderKind } from "@/enums";

// Mirrors the backend presets. The server re-checks what the installation can run, so this
// list only decides what the tour offers.
export type LibraryTranscodeTarget = "audio" | "video";

export interface LibraryFolderPreset {
  kind: LibraryFolderKind;
  defaultName: string;
  icon: string;
  purpose: string;
  transcode: LibraryTranscodeTarget | null;
  autoTag: boolean;
  // System folders keep their default name; the server ignores any other.
  fixedName?: boolean;
}

export interface LibraryCapabilities {
  streaming: boolean;
  autoTagging: boolean;
}

export interface LibraryFolderDraft {
  kind: LibraryFolderKind;
  name: string;
  automationEnabled: boolean;
  audioRungs: AudioRung[];
  videoRungs: VideoRung[];
  autoTag: boolean;
}

export type LibraryErrors = Partial<Record<LibraryFolderKind, string>>;

export const LIBRARY_FOLDER_PRESETS: LibraryFolderPreset[] = [
  {
    autoTag: true,
    defaultName: "Music",
    icon: "i-lucide-music",
    kind: LibraryFolderKind.Music,
    purpose: "Songs and albums, ready to stream",
    transcode: "audio",
  },
  {
    autoTag: false,
    defaultName: "Movies",
    icon: "i-lucide-film",
    kind: LibraryFolderKind.Movies,
    purpose: "Films and series, ready to stream",
    transcode: "video",
  },
  {
    autoTag: false,
    defaultName: "Pictures",
    icon: "i-lucide-image",
    kind: LibraryFolderKind.Pictures,
    purpose: "Photos from your camera and phone",
    transcode: null,
  },
  {
    autoTag: false,
    defaultName: "Videos",
    icon: "i-lucide-video",
    kind: LibraryFolderKind.Videos,
    purpose: "Home videos and clips",
    transcode: null,
  },
  {
    autoTag: false,
    defaultName: "Documents",
    icon: "i-lucide-file-text",
    kind: LibraryFolderKind.Documents,
    purpose: "PDFs and office files",
    transcode: null,
  },
  {
    autoTag: false,
    defaultName: "Notes",
    fixedName: true,
    icon: "i-lucide-notebook",
    kind: LibraryFolderKind.Notes,
    purpose: "Markdown notes you write in the app",
    transcode: null,
  },
];

export const AUDIO_RUNG_OPTIONS = [
  { label: "96 kbps", value: AudioRung.Kbps96 },
  { label: "128 kbps", value: AudioRung.Kbps128 },
  { label: "192 kbps", value: AudioRung.Kbps192 },
  { label: "256 kbps", value: AudioRung.Kbps256 },
  { label: "320 kbps", value: AudioRung.Kbps320 },
];

export const VIDEO_RUNG_OPTIONS = [
  { label: "360p", value: VideoRung.P360 },
  { label: "480p", value: VideoRung.P480 },
  { label: "720p", value: VideoRung.P720 },
  { label: "1080p", value: VideoRung.P1080 },
  { label: "1440p", value: VideoRung.P1440 },
  { label: "2160p", value: VideoRung.P2160 },
];

// Same starting qualities as the rule editor, so both entry points agree.
const DEFAULT_AUDIO_RUNGS = [AudioRung.Kbps192, AudioRung.Kbps320];
const DEFAULT_VIDEO_RUNGS = [VideoRung.P720, VideoRung.P1080];

const MAX_NAME_LENGTH = 255;
const INVALID_NAME_CHARACTERS = '/\\:*?"<>|';

export const readLibraryCapabilities = (): LibraryCapabilities => ({
  autoTagging: import.meta.env.VITE_AUTOTAGGING_ENABLED === "true",
  streaming: import.meta.env.VITE_STREAMING_ENABLED === "true",
});

export const presetFor = (kind: LibraryFolderKind): LibraryFolderPreset => {
  const preset = LIBRARY_FOLDER_PRESETS.find((p) => p.kind === kind);
  if (!preset) throw new Error(`Unknown library folder kind ${kind}`);

  return preset;
};

export const canTranscode = (preset: LibraryFolderPreset, capabilities: LibraryCapabilities) =>
  preset.transcode !== null && capabilities.streaming;

export const canAutoTag = (preset: LibraryFolderPreset, capabilities: LibraryCapabilities) =>
  preset.autoTag && capabilities.autoTagging;

export const isAutomatable = (preset: LibraryFolderPreset, capabilities: LibraryCapabilities) =>
  canTranscode(preset, capabilities) || canAutoTag(preset, capabilities);

export const hasAnyAutomation = (capabilities: LibraryCapabilities) =>
  LIBRARY_FOLDER_PRESETS.some((preset) => isAutomatable(preset, capabilities));

export const createLibraryDrafts = (capabilities: LibraryCapabilities): LibraryFolderDraft[] =>
  LIBRARY_FOLDER_PRESETS.map((preset) => ({
    audioRungs: preset.transcode === "audio" ? [...DEFAULT_AUDIO_RUNGS] : [],
    autoTag: canAutoTag(preset, capabilities),
    automationEnabled: isAutomatable(preset, capabilities),
    kind: preset.kind,
    name: preset.defaultName,
    videoRungs: preset.transcode === "video" ? [...DEFAULT_VIDEO_RUNGS] : [],
  }));

const selectedRungs = (draft: LibraryFolderDraft): readonly number[] => {
  if (presetFor(draft.kind).transcode === "audio") return draft.audioRungs;

  return draft.videoRungs;
};

// Mirrors LibraryPolicyRules on the server: a transcode rule only exists with at least one quality.
export const willTranscode = (draft: LibraryFolderDraft, capabilities: LibraryCapabilities) =>
  draft.automationEnabled &&
  canTranscode(presetFor(draft.kind), capabilities) &&
  selectedRungs(draft).length > 0;

export const willAutoTag = (draft: LibraryFolderDraft, capabilities: LibraryCapabilities) =>
  draft.automationEnabled && canAutoTag(presetFor(draft.kind), capabilities) && draft.autoTag;

// Only true when the server will actually create a rule, so the tour never promises more.
export const isDraftAutomated = (draft: LibraryFolderDraft, capabilities: LibraryCapabilities) =>
  willTranscode(draft, capabilities) || willAutoTag(draft, capabilities);

const nameError = (name: string): string | null => {
  const trimmed = name.trim();

  if (trimmed.length === 0) return "Give this folder a name.";
  if (trimmed.length > MAX_NAME_LENGTH) return `Keep the name under ${MAX_NAME_LENGTH} characters.`;

  const invalid = [...trimmed].find((char) => INVALID_NAME_CHARACTERS.includes(char));
  if (invalid) return `Folder names can't contain ${invalid}`;

  return null;
};

export const validateFolderNames = (drafts: LibraryFolderDraft[]): LibraryErrors => {
  const errors: LibraryErrors = {};
  const seen = new Set<string>();

  // Fixed names are claimed first, so a clash is reported on the folder that can still be renamed.
  const fixedFirst = [
    ...drafts.filter((draft) => presetFor(draft.kind).fixedName),
    ...drafts.filter((draft) => !presetFor(draft.kind).fixedName),
  ];

  for (const draft of fixedFirst) {
    const error = nameError(draft.name);
    const key = draft.name.trim().toLowerCase();

    if (error) {
      errors[draft.kind] = error;
    } else if (seen.has(key)) {
      errors[draft.kind] = "Another folder already uses this name.";
    }

    seen.add(key);
  }

  return errors;
};

const isMissingQualities = (draft: LibraryFolderDraft, capabilities: LibraryCapabilities) => {
  const transcodeEnabled =
    draft.automationEnabled && canTranscode(presetFor(draft.kind), capabilities);
  if (!transcodeEnabled || selectedRungs(draft).length > 0) return false;

  // Tagging still gives the folder a rule, so skipping every quality is a valid choice.
  return !willAutoTag(draft, capabilities);
};

export const validateQualities = (
  drafts: LibraryFolderDraft[],
  capabilities: LibraryCapabilities,
): LibraryErrors => {
  const errors: LibraryErrors = {};

  // TODO: transcoding and tagging share one automation switch per folder, which is why an empty
  // quality list has to be judged against the tag choice here. Give each action its own toggle
  // in the tour and in the request so neither depends on the other.
  for (const draft of drafts) {
    if (isMissingQualities(draft, capabilities)) {
      errors[draft.kind] = "Pick at least one quality, or turn this off.";
    }
  }

  return errors;
};

export const buildFinishTourRequest = (
  drafts: LibraryFolderDraft[],
  capabilities: LibraryCapabilities,
): FinishTourRequest => ({
  folders: drafts.map((draft) => {
    const preset = presetFor(draft.kind);
    const transcodes = willTranscode(draft, capabilities);
    const tags = willAutoTag(draft, capabilities);

    return {
      audioRungs: transcodes && preset.transcode === "audio" ? [...draft.audioRungs] : [],
      autoTag: tags,
      automationEnabled: transcodes || tags,
      kind: draft.kind,
      name: draft.name.trim(),
      videoRungs: transcodes && preset.transcode === "video" ? [...draft.videoRungs] : [],
    };
  }),
});
