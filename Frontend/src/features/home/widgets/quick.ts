import { HomeQuickAccessStyle, HomeQuickUploadStyle } from "@/enums/home-widget";

export const HOME_QUICK_ACCESS_STYLES = [
  {
    value: HomeQuickAccessStyle.Tiles,
    label: "Tiles",
    description: "Simple folder cards in a grid.",
  },
  {
    value: HomeQuickAccessStyle.List,
    label: "List",
    description: "Familiar rows for your folders.",
  },
  {
    value: HomeQuickAccessStyle.Chips,
    label: "Chips",
    description: "Compact bookmarks that wrap.",
  },
];

export const HOME_QUICK_UPLOAD_STYLES = [
  {
    value: HomeQuickUploadStyle.DropZone,
    label: "Drop zone",
    description: "A quiet target for files and folders.",
  },
  {
    value: HomeQuickUploadStyle.Actions,
    label: "Actions",
    description: "Files, folders and archives within reach.",
  },
  {
    value: HomeQuickUploadStyle.Pulse,
    label: "Pulse",
    description: "A round target that responds to dragging.",
  },
];
