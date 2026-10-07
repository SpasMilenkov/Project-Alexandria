import { HomeWidgetType } from "@/enums/home-widget";

export const HOME_WIDGET_DRAG_TYPE = "application/x-alexandria-home-widget";

export const readHomeWidgetDrag = (transfer: DataTransfer | null): HomeWidgetType | null => {
  const raw = transfer?.getData(HOME_WIDGET_DRAG_TYPE);
  if (!raw) return null;

  const type = Number(raw);
  if (
    String(type) !== raw ||
    !Number.isInteger(type) ||
    !Object.values(HomeWidgetType).includes(type)
  )
    return null;

  return type;
};
