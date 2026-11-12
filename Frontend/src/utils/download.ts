// Hands a downloaded body to the browser as a file save.
export const saveBlob = (blob: Blob, fileName: string): void => {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");

  anchor.href = url;
  anchor.download = fileName;
  anchor.rel = "noopener";
  document.body.append(anchor);
  anchor.click();
  anchor.remove();

  // The click starts the save synchronously, so the URL can go on the next tick.
  setTimeout(() => URL.revokeObjectURL(url), 0);
};

// A local timestamp for file names, such as 20261110-153045.
export const fileStamp = (date = new Date()): string => {
  const pad = (value: number) => String(value).padStart(2, "0");
  const day = `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}`;

  return `${day}-${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`;
};
