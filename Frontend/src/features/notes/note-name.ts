// The editor shows and edits a note's name without its extension, which the server keeps, so a
// rename can't turn a note into a different kind of file.
const MARKDOWN_EXTENSION = /\.(?:md|markdown)$/iu;

export const noteTitleOf = (fileName: string): string => fileName.replace(MARKDOWN_EXTENSION, "");
