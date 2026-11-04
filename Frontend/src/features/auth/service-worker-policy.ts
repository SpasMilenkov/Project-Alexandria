const DEFAULT_API_BASE_URL = "http://localhost:5000";

const PROTECTED_PATH_PREFIXES = [
  "/stream",
  "/preview",
  "/covers",
  "/images",
  "/alexandria-files",
  "/alexandria-temp",
];

const pathMatchesPrefix = (pathname: string, prefix: string): boolean =>
  pathname === prefix || pathname.startsWith(`${prefix}/`);

export const resolveApiBase = (baseUrl: string | undefined, origin: string): URL => {
  const url = new URL(baseUrl?.trim() || DEFAULT_API_BASE_URL, origin);

  if (url.pathname.length > 1) {
    url.pathname = url.pathname.replace(/\/+$/u, "");
  }

  return url;
};

export const resolveConfiguredApiBase = (origin: string): URL => {
  const configuredBase =
    typeof __ALEXANDRIA_API_BASE_URL__ === "string" ? __ALEXANDRIA_API_BASE_URL__ : "";

  return resolveApiBase(configuredBase || undefined, origin);
};

const apiBasePath = (apiBase: URL): string => {
  if (apiBase.pathname === "/") {
    return "";
  }

  return apiBase.pathname;
};

export const isApiRequest = (url: URL, apiBase: URL): boolean => {
  if (url.origin !== apiBase.origin) {
    return false;
  }

  const basePath = apiBasePath(apiBase);

  if (!basePath) {
    return true;
  }

  return url.pathname === basePath || url.pathname.startsWith(`${basePath}/`);
};

export const isProtectedRequest = (
  url: URL,
  apiBase: URL,
  pageOrigin: string,
): boolean => {
  if (isApiRequest(url, apiBase)) {
    return true;
  }

  if (url.origin !== pageOrigin) {
    return false;
  }

  return PROTECTED_PATH_PREFIXES.some((prefix) => pathMatchesPrefix(url.pathname, prefix));
};
