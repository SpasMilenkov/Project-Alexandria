export type ApiErrorKind = "http" | "network" | "timeout";

interface ApiErrorDetails<T> {
  cause?: unknown;
  data?: T;
  kind: ApiErrorKind;
  status?: number;
}

export class ApiError<T = unknown> extends Error {
  readonly data?: T;

  readonly kind: ApiErrorKind;

  readonly status?: number;

  constructor(message: string, { cause, data, kind, status }: ApiErrorDetails<T>) {
    super(message, { cause });

    this.name = "ApiError";
    this.data = data;
    this.kind = kind;
    this.status = status;
  }
}

export const isApiError = (err: unknown): err is ApiError => err instanceof ApiError;
