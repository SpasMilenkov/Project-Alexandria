import type { RouteRecordRaw } from "vue-router";

/** The Notes page and the note editor. */
export const NOTES_ROUTES: RouteRecordRaw[] = [
  {
    component: () => import("@/views/dashboard/NotesView.vue"),
    meta: { layout: "dashboard", requiresAuth: true, siteIcon: "notes", title: "Notes" },
    name: "notes",
    path: "/notes",
  },
  {
    component: () => import("@/views/dashboard/NoteEditorView.vue"),
    meta: { layout: "dashboard", requiresAuth: true, siteIcon: "note", title: "Note" },
    name: "note-editor",
    path: "/notes/:fileId",
  },
];
