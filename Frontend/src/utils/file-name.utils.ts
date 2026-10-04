// Extension parsing shared by explorer rows and rename flows. Leading-dot
// files (.gitignore, .env) and extensionless names yield an empty string.
export const getFileExtension = (fileName: string): string => {
  const lastDot = fileName.lastIndexOf(".");
  if (lastDot <= 0 || lastDot === fileName.length - 1) {
    return "";
  }
  return fileName.slice(lastDot + 1);
};
