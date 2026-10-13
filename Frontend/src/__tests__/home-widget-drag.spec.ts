import { describe, expect, it } from "vitest";

import { HomeWidgetType } from "@/enums/home-widget";
import { HOME_WIDGET_DRAG_TYPE, readHomeWidgetDrag } from "@/utils/home-widget-drag";

const transfer = (raw: string): DataTransfer =>
  ({
    getData: (type: string) => (type === HOME_WIDGET_DRAG_TYPE ? raw : ""),
  }) as DataTransfer;

describe("Home catalogue drag payloads", () => {
  it.each([HomeWidgetType.Clock, HomeWidgetType.LibraryShortcuts])(
    "accepts registered widget %s, including zero",
    (type) => {
      expect(readHomeWidgetDrag(transfer(String(type)))).toBe(type);
    },
  );

  it.each(["", " ", "unknown", "99", "0.5", "Infinity", "00", "1e0", '{"type":0}'])(
    "ignores unsupported or malformed payload %s",
    (raw) => {
      expect(readHomeWidgetDrag(transfer(raw))).toBeNull();
    },
  );

  it("ignores missing data transfers", () => {
    expect(readHomeWidgetDrag(null)).toBeNull();
  });
});
