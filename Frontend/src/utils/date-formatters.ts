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
