import type { WrappedCardResponse } from "@/api/stats";

import { WrappedCardType } from "@/enums/wrapped-card-type";
import { WrappedRhythmEvidence, WrappedTimeScene } from "@/enums/wrapped-story";
import { formatWrappedDuration } from "@/utils/wrapped-art.utils";
import { dateLabel } from "@/utils/wrapped-story.utils";

type StoryCard = Pick<WrappedCardResponse, "type" | "facts">;

const storyNotes: Partial<Record<number, string>> = {
  [WrappedCardType.TopArtists]: "The artists you spent the most time with, including every repeat.",
  [WrappedCardType.TopSongs]: "The songs you spent the most time with, including every repeat.",
  [WrappedCardType.ListeningTime]:
    "Every listen adds up, from a quick song to a long evening of favorites. The milestone puts all that time into perspective.",
  [WrappedCardType.Persona]:
    "A glimpse of when music most often found its way into your day. The scene brings that rhythm to life.",
  [WrappedCardType.Bookends]:
    "The songs at the edges of this chapter: your first play and the latest one in this recap.",
  [WrappedCardType.MostReplayed]:
    "Each mark is another listen to this song. A quick listen counts too; you didn't have to finish it every time.",
  [WrappedCardType.MostMinutes]: "The song you spent the most time with, across all your listens.",
  [WrappedCardType.Streak]:
    "Your longest stretch of days with music. The outlined dates belong to that streak, and deeper colors show days with more listening.",
  [WrappedCardType.BusiestDay]:
    "Your biggest day with music, shown alongside the quieter days around it. Choose a day to revisit your listening.",
  [WrappedCardType.LongestSitting]: "The time you settled in for your longest listen.",
  [WrappedCardType.Discoveries]:
    "Songs you played in Alexandria for the first time during this chapter.",
  [WrappedCardType.NewArtists]:
    "New voices that joined your Alexandria rotation during this chapter.",
  [WrappedCardType.LoyalListener]: "The familiar favorites you kept making time for.",
  [WrappedCardType.Exploration]:
    "A mix of fresh finds and familiar favorites. A song is a fresh find when you play it in Alexandria for the first time.",
  [WrappedCardType.RetainedDiscovery]:
    "A fresh find you kept coming back to on different days, until it became part of your rotation.",
  [WrappedCardType.Chapters]:
    "Different artists took the lead as your year unfolded. These chapters highlight the months you spent enough time listening for a favorite to stand out.",
  [WrappedCardType.ReturningFavorite]:
    "You gave this song a break, then welcomed it back on more than one day. The quiet stretch shows the time between those listens in Alexandria.",
} satisfies Record<WrappedCardType, string>;

export const wrappedStoryNote = (card: StoryCard): string | null => {
  if (card.type === WrappedCardType.BusiestDay) {
    const seconds = card.facts?.seconds ?? 0;
    const typicalDay = card.facts?.baselineSeconds ?? 0;

    if (Number.isFinite(seconds) && seconds > 0 && Number.isFinite(typicalDay) && typicalDay > 0) {
      return `You spent ${formatWrappedDuration(seconds)} with music that day, compared with ${formatWrappedDuration(typicalDay)} on a typical listening day.`;
    }
  }

  if (card.type === WrappedCardType.RetainedDiscovery) {
    const firstPlay = card.facts?.from;

    if (firstPlay && Number.isFinite(Date.parse(firstPlay))) {
      return `First played in Alexandria on ${dateLabel(firstPlay)}. You kept finding your way back to it on different days.`;
    }
  }

  return storyNotes[card.type] ?? null;
};

const listeningWindows: Record<WrappedTimeScene, string> = {
  [WrappedTimeScene.Night]: "the late-night hours",
  [WrappedTimeScene.Morning]: "the morning",
  [WrappedTimeScene.Midday]: "the middle of the day",
  [WrappedTimeScene.Afternoon]: "the afternoon",
  [WrappedTimeScene.Evening]: "the evening",
};

export const wrappedCardSubline = (card: WrappedCardResponse): string | null => {
  if (card.type !== WrappedCardType.Persona) return card.subline ?? null;

  const rhythm = card.facts?.rhythm;

  if (rhythm?.evidence === WrappedRhythmEvidence.Sparse) {
    return "A few more listening days will reveal the rhythm of your music.";
  }

  if (rhythm?.evidence === WrappedRhythmEvidence.Balanced) {
    return "A little morning, a little midnight. You made room for music throughout the day.";
  }

  if (
    !rhythm ||
    rhythm.scene === null ||
    !listeningWindows[rhythm.scene] ||
    !Number.isFinite(rhythm.share) ||
    rhythm.share < 0 ||
    rhythm.share > 1
  ) {
    return "A glimpse of the times you made room for music.";
  }

  const share = new Intl.NumberFormat(undefined, {
    style: "percent",
    maximumFractionDigits: 0,
  }).format(rhythm.share);

  let description = `${share} of your listening found its home in ${listeningWindows[rhythm.scene]}.`;

  if (card.subline?.includes("Mostly on weekends.")) description += " Mostly on weekends.";
  else if (card.subline?.includes("Mostly on weekdays.")) description += " Mostly on weekdays.";

  return description;
};
