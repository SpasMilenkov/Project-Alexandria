import type { EditorToolbarItem } from "@nuxt/ui";

// The editor's own handler for this kind types "[[", which opens the link picker.
export const NOTE_LINK_TOOL = "noteLink";

// Only formatting with a markdown syntax is offered, so whatever the toolbar produces survives
// being saved as plain markdown. Links go through the picker, so they always carry an ID.
export const NOTE_TOOLBAR_ITEMS: EditorToolbarItem[][] = [
  [
    {
      "aria-label": "Undo",
      icon: "i-lucide-undo-2",
      kind: "undo",
      tooltip: { text: "Undo" },
    },
    {
      "aria-label": "Redo",
      icon: "i-lucide-redo-2",
      kind: "redo",
      tooltip: { text: "Redo" },
    },
  ],
  [
    {
      "aria-label": "Text style",
      content: { align: "start" },
      icon: "i-lucide-heading",
      items: [
        { icon: "i-lucide-pilcrow", kind: "paragraph", label: "Paragraph" },
        { icon: "i-lucide-heading-1", kind: "heading", label: "Heading 1", level: 1 },
        { icon: "i-lucide-heading-2", kind: "heading", label: "Heading 2", level: 2 },
        { icon: "i-lucide-heading-3", kind: "heading", label: "Heading 3", level: 3 },
      ],
      tooltip: { text: "Text style" },
    },
  ],
  [
    {
      "aria-label": "Bold",
      icon: "i-lucide-bold",
      kind: "mark",
      mark: "bold",
      tooltip: { text: "Bold" },
    },
    {
      "aria-label": "Italic",
      icon: "i-lucide-italic",
      kind: "mark",
      mark: "italic",
      tooltip: { text: "Italic" },
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
  [
    {
      "aria-label": "Link to a note or file",
      icon: "i-lucide-link",
      kind: NOTE_LINK_TOOL,
      tooltip: { text: "Link to a note or file ([[)" },
    },
  ],
  [
    {
      "aria-label": "Bullet list",
      icon: "i-lucide-list",
      kind: "bulletList",
      tooltip: { text: "Bullet list" },
    },
    {
      "aria-label": "Numbered list",
      icon: "i-lucide-list-ordered",
      kind: "orderedList",
      tooltip: { text: "Numbered list" },
    },
    {
      "aria-label": "Quote",
      icon: "i-lucide-text-quote",
      kind: "blockquote",
      tooltip: { text: "Quote" },
    },
    {
      "aria-label": "Code block",
      icon: "i-lucide-square-code",
      kind: "codeBlock",
      tooltip: { text: "Code block" },
    },
    {
      "aria-label": "Divider",
      icon: "i-lucide-separator-horizontal",
      kind: "horizontalRule",
      tooltip: { text: "Divider" },
    },
  ],
];
