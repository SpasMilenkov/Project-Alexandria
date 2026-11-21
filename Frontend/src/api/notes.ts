import { ApiError } from "./errors";
import { fileApi } from "./file";

export const notesApi = {
  // Owner-encrypted content needs the password flow, which the editor does not have yet.
  getVersionText: async (versionId: string): Promise<string> => {
    const info = await fileApi.downloadFileVersion(versionId);

    if (info.isEncrypted) {
      throw new Error("Encrypted notes can't be opened in the editor yet.");
    }

    const response = await fetch(info.presignedUrl);

    if (!response.ok) {
      throw new ApiError(`Couldn't read the note (${response.status})`, {
        kind: "http",
        status: response.status,
      });
    }

    return response.text();
  },
};
