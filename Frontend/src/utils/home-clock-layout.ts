import type { HomeClockOptions } from "@/types/home";

import { HomeVerticalAlignment } from "@/enums/home-widget";

const verticalTracks = {
  [HomeVerticalAlignment.Top]: 1,
  [HomeVerticalAlignment.Middle]: 2,
  [HomeVerticalAlignment.Bottom]: 3,
};

export const homeClockLayout = (options: HomeClockOptions, faceOnly: boolean) => {
  const faceRow = verticalTracks[options.verticalAlignment];
  const textRow = verticalTracks[options.textVerticalAlignment];
  const faceColumn = options.horizontalAlignment + 1;
  const textColumn = options.textAlignment + 1;
  const shared = !faceOnly && faceRow === textRow && faceColumn === textColumn;
  const layout = {
    shared,
    gridTemplateRows: "minmax(0, 1fr)",
    gridTemplateColumns: "minmax(0, 1fr)",
    faceArea: "1 / 1 / -1 / -1",
    textArea: "1 / 1 / -1 / -1",
  };

  if (faceOnly || shared) return layout;

  if (faceRow !== textRow) {
    const rows = ["minmax(0, 1fr)", "minmax(0, 1fr)", "minmax(0, 1fr)"];
    rows[textRow - 1] = "auto";
    layout.gridTemplateRows = rows.join(" ");
    layout.textArea = `${textRow} / 1 / ${textRow + 1} / -1`;

    if (faceRow < textRow) layout.faceArea = `1 / 1 / ${textRow} / -1`;
    else layout.faceArea = `${textRow + 1} / 1 / 4 / -1`;

    return layout;
  }

  const columns = ["minmax(0, 1fr)", "minmax(0, 1fr)", "minmax(0, 1fr)"];
  columns[textColumn - 1] = "fit-content(40%)";
  layout.gridTemplateColumns = columns.join(" ");
  layout.textArea = `1 / ${textColumn} / -1 / ${textColumn + 1}`;

  if (faceColumn < textColumn) layout.faceArea = `1 / 1 / -1 / ${textColumn}`;
  else layout.faceArea = `1 / ${textColumn + 1} / -1 / 4`;

  return layout;
};
