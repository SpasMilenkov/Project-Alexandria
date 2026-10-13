interface HomeTimeZoneChoice {
  label: string;
  value: string;
  disabled?: boolean;
}

export const homeTimeZoneLabel = (identifier: unknown): string => {
  if (identifier === "local") return "Your device's time zone";
  if (typeof identifier !== "string" || !identifier) return "Saved time zone";

  return identifier.split("_").join(" ").split("/").join(" / ");
};

export const resolveHomeTimeZone = (identifier: unknown) => {
  if (identifier === "local") return { timeZone: undefined, unavailable: false };
  if (typeof identifier !== "string" || !identifier)
    return { timeZone: undefined, unavailable: true };

  try {
    const formatter = new Intl.DateTimeFormat(undefined, { timeZone: identifier });

    return { timeZone: formatter.resolvedOptions().timeZone, unavailable: false };
  } catch {
    return { timeZone: undefined, unavailable: true };
  }
};

export const homeTimeZoneChoices = (identifiers: string[], saved: string): HomeTimeZoneChoice[] => {
  const choices: HomeTimeZoneChoice[] = [...new Set(identifiers)]
    .filter((identifier) => !resolveHomeTimeZone(identifier).unavailable)
    .map((identifier) => ({ label: homeTimeZoneLabel(identifier), value: identifier }));

  if (!choices.some((choice) => choice.value === saved)) {
    choices.unshift({ label: `${homeTimeZoneLabel(saved)} (saved)`, value: saved, disabled: true });
  }

  return choices;
};
