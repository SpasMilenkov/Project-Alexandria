//oxlint-disable
//prettier-ignore
export const formatDate = (dateString: string) => {
  const diffInDays = Math.floor((Date.now() - new Date(dateString).getTime()) / 86400000);
  const [value, unit] = diffInDays < 7   ? [diffInDays, "day"]
                      : diffInDays < 30  ? [Math.floor(diffInDays / 7), "week"]
                      : diffInDays < 365 ? [Math.floor(diffInDays / 30), "month"]
                      :                    [Math.floor(diffInDays / 365), "year"];
  return diffInDays === 0 ? "Today"
       : diffInDays === 1 ? "Yesterday"
       : `${value} ${unit}${value !== 1 ? "s" : ""} ago`;
};

export const formatDuration = (seconds: number): string => {
  const s = Math.floor(seconds);
  const d = Math.floor(s / 86400);
  const h = Math.floor((s % 86400) / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;

  const pad = (n: number) => String(n).padStart(2, "0");

  if (d > 0) return `${d}:${pad(h)}:${pad(m)}:${pad(sec)}`;
  if (h > 0) return `${h}:${pad(m)}:${pad(sec)}`;
  return `${m}:${pad(sec)}`;
};
