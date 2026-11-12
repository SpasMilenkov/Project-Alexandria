import type { AccountAccessSummary } from "@/api/adminAudit";

import { LoginFailureReason } from "@/enums/login-failure-reason";
import { describeUserAgent } from "@/utils/user-agent";

export const ACCESS_WINDOW_OPTIONS = [
  { label: "30d", value: 30 },
  { label: "60d", value: 60 },
  { label: "90d", value: 90 },
] as const;

export type AccessWindowDays = (typeof ACCESS_WINDOW_OPTIONS)[number]["value"];

export const DEFAULT_ACCESS_WINDOW: AccessWindowDays = 30;

export const WIDEST_ACCESS_WINDOW: AccessWindowDays = 90;

export const FAILURE_REASON_LABELS: Record<LoginFailureReason, string> = {
  [LoginFailureReason.UnknownUser]: "No account with that email",
  [LoginFailureReason.BadPassword]: "Wrong password",
  [LoginFailureReason.LockedOut]: "Account was locked",
  [LoginFailureReason.NotAllowed]: "Sign-in not allowed",
};

export const failureReasonLabel = (reason: LoginFailureReason): string =>
  FAILURE_REASON_LABELS[reason] ?? "Other";

// Two sessions in the same browser read as one device, so labels are deduplicated.
export const sessionDeviceLabels = (userAgents: string[]): string[] => [
  ...new Set(userAgents.map((agent) => describeUserAgent(agent).label)),
];

export const isLockedOut = (account: AccountAccessSummary, now = Date.now()): boolean => {
  if (!account.lockoutEnd) return false;

  return new Date(account.lockoutEnd).getTime() > now;
};

export const isWithinDays = (iso: string, days: number, now = Date.now()): boolean =>
  new Date(iso).getTime() >= now - days * 24 * 60 * 60 * 1000;

// Reads as a sentence under the failed sign-in count.
export const failedTrendCaption = (current: number, previous: number, days: number): string => {
  const period = `the ${days} days before`;
  const difference = current - previous;

  if (difference === 0) return `Same as ${period}`;
  if (difference > 0) return `${difference} more than ${period}`;

  return `${-difference} fewer than ${period}`;
};
