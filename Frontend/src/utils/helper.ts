import type { AxiosError } from "axios";

export const handleError = (err: unknown, defaultMessage: string): string => {
  let message = defaultMessage;
  if (err instanceof Error) {
    message = err.message;
  }
  if ((err as AxiosError)?.response?.data) {
    message =
      (err as AxiosError<{ message: string }>).response?.data?.message ??
      message;
  }
  return message;
};
