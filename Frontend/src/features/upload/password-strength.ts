const PASSWORD_MIN_LENGTH = 16;

export const PASSWORD_STRENGTH_LEVELS = [
  { barColor: "bg-red-400", label: "Very weak", textColor: "text-red-500" },
  { barColor: "bg-orange-400", label: "Weak", textColor: "text-orange-500" },
  { barColor: "bg-yellow-400", label: "Fair", textColor: "text-yellow-600 dark:text-yellow-400" },
  { barColor: "bg-lime-400", label: "Good", textColor: "text-lime-600 dark:text-lime-400" },
  { barColor: "bg-green-500", label: "Strong", textColor: "text-green-600 dark:text-green-400" },
] as const;

export const STRENGTH_IDLE_TEXT = "text-gray-600 dark:text-gray-400";

export const getPasswordRequirements = (password: string) => [
  { label: "16+ characters", met: password.length >= PASSWORD_MIN_LENGTH },
  { label: "Uppercase letter", met: /[A-Z]/u.test(password) },
  { label: "Lowercase letter", met: /[a-z]/u.test(password) },
  { label: "Number", met: /[0-9]/u.test(password) },
  { label: "Special character", met: /[^a-zA-Z0-9]/u.test(password) },
];

export const getPasswordScore = (password: string): number =>
  getPasswordRequirements(password).filter((requirement) => requirement.met).length;

export const strengthLevel = (password: string) => {
  if (!password) return undefined;

  return PASSWORD_STRENGTH_LEVELS[getPasswordScore(password) - 1];
};

export const strengthBarClass = (password: string, segment: number) => {
  const level = strengthLevel(password);
  if (level && segment <= getPasswordScore(password)) return level.barColor;

  return "bg-gray-200 dark:bg-gray-700";
};
