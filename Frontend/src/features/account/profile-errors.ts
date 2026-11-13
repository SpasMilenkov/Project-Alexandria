import { isApiError } from "@/api/errors";

// Turns a rejected rename into the message shown under the field, or null when it is not
// something the user can fix by typing a different name.
export const usernameFieldError = (err: unknown): string | null => {
  if (!isApiError(err)) return null;
  if (err.status === 409) return "That username is already taken.";
  if (err.status !== 400) return null;

  const errors = (err.data as { errors?: Record<string, string[]> } | undefined)?.errors;
  const [first] = Object.values(errors ?? {}).flat();

  return first ?? null;
};
