import type { DropdownMenuItem, EditorToolbarItem } from "@nuxt/ui";

/** The toolbar slot the editor renders its Insert menu into. */
export const NOTE_INSERT_SLOT = "insert";

/** What the Insert menu's own entries do; the editor supplies them. */
export interface NoteInsertActions {
  // Types "[[", which opens the link picker.
  link: () => void;
  // Types "![[": the same picker, inserting the item as an embed.
  embed: () => void;
  // Links what the player has open, at its current time.
  timestamp: () => void;
}

/**
 * One quiet row: block style, the four marks, an Insert menu, and undo/redo at the far end. Only
 * formatting with a markdown syntax is offered, so whatever the toolbar produces survives being
 * saved as plain markdown.
 *
 * Insert goes through a slot rather than a dropdown item: a toolbar dropdown takes on the icon and
 * label of whichever child is active, so "Insert" would turn into "Bullet list" inside a list.
 */
export const NOTE_TOOLBAR_ITEMS: EditorToolbarItem[][] = [
  [
    {
      "aria-label": "Text style",
      content: { align: "start" },
      icon: "i-lucide-pilcrow",
      items: [
        { icon: "i-lucide-pilcrow", kind: "paragraph", label: "Paragraph" },
        { icon: "i-lucide-heading-1", kind: "heading", label: "Heading 1", level: 1 },
        { icon: "i-lucide-heading-2", kind: "heading", label: "Heading 2", level: 2 },
        { icon: "i-lucide-heading-3", kind: "heading", label: "Heading 3", level: 3 },
      ],
      // The toolbar swaps in the active style's label and icon.
      label: "Paragraph",
      trailingIcon: "i-lucide-chevron-down",
      ui: { label: "hidden sm:inline", trailingIcon: "size-3.5 text-gray-500 dark:text-gray-500" },
    },
  ],
  [
    {
      "aria-label": "Bold",
      icon: "i-lucide-bold",
      kind: "mark",
      mark: "bold",
      tooltip: { kbds: ["meta", "b"], text: "Bold" },
    },
    {
      "aria-label": "Italic",
      icon: "i-lucide-italic",
      kind: "mark",
      mark: "italic",
      tooltip: { kbds: ["meta", "i"], text: "Italic" },
    },
    {
      "aria-label": "Strikethrough",
      icon: "i-lucide-strikethrough",
      kind: "mark",
      mark: "strike",
      tooltip: { text: "Strikethrough" },
    },
    {
      "aria-label": "Inline code",
      icon: "i-lucide-code",
      kind: "mark",
      mark: "code",
      tooltip: { text: "Inline code" },
    },
  ],
  [{ "aria-label": "Insert", slot: NOTE_INSERT_SLOT }],
  [
    {
      "aria-label": "Undo",
      icon: "i-lucide-undo-2",
      kind: "undo",
      tooltip: { kbds: ["meta", "z"], text: "Undo" },
    },
    {
      "aria-label": "Redo",
      icon: "i-lucide-redo-2",
      kind: "redo",
      tooltip: { kbds: ["meta", "shift", "z"], text: "Redo" },
    },
  ],
];

/** Blocks in the Insert menu, run by the toolbar's own handlers. */
export const NOTE_INSERT_BLOCKS: EditorToolbarItem[] = [
  { icon: "i-lucide-list", kind: "bulletList", label: "Bullet list" },
  { icon: "i-lucide-list-ordered", kind: "orderedList", label: "Numbered list" },
  { icon: "i-lucide-text-quote", kind: "blockquote", label: "Quote" },
  { icon: "i-lucide-square-code", kind: "codeBlock", label: "Code block" },
  { icon: "i-lucide-separator-horizontal", kind: "horizontalRule", label: "Divider" },
];

/**
 * The Insert menu's first group: links and embeds through the picker, and a link to the playing
 * file at `timestampLabel` ("12:04"), or a disabled entry when nothing is playing.
 */
export const noteInsertLinks = (
  actions: NoteInsertActions,
  timestampLabel: string | null,
): DropdownMenuItem[] => [
  { icon: "i-lucide-link", kbds: ["[["], label: "Link…", onSelect: actions.link },
  { icon: "i-lucide-image-plus", kbds: ["![["], label: "Embed…", onSelect: actions.embed },
  {
    disabled: timestampLabel === null,
    icon: "i-lucide-timer",
    label: timestampLabel === null ? "Link to what's playing" : `Link at ${timestampLabel}`,
    onSelect: actions.timestamp,
  },
];
