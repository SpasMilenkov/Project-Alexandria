import type { WrappedDeckResponse } from "@/api/stats";

import { HomeRecapStyle } from "@/enums/home-widget";
import { WrappedCardType } from "@/enums/wrapped-card-type";
import { formatWrappedDuration } from "@/utils/wrapped-art.utils";

export const HOME_RECAP_STYLES = [
  { value: HomeRecapStyle.Poster, label: "Poster", description: "Your month as a colorful cover." },
  {
    value: HomeRecapStyle.Numbers,
    label: "Numbers",
    description: "Time listened and the tracks that led it.",
  },
  {
    value: HomeRecapStyle.Stories,
    label: "Stories",
    description: "Swipe through your actual recap stories.",
  },
];

export const homeRecapFacts = (response: WrappedDeckResponse) => {
  const artists = response.deck.cards.find((card) => card.type === WrappedCardType.TopArtists);
  const songs = response.deck.cards.find((card) => card.type === WrappedCardType.TopSongs);

  return [
    { label: "Listened", value: formatWrappedDuration(response.summary?.seconds ?? 0) },
    { label: "Tracks", value: String(response.summary?.tracks ?? 0) },
    { label: "Top artist", value: artists?.entries[0]?.title ?? "Unknown" },
    { label: "Top song", value: songs?.entries[0]?.title ?? "Unknown" },
  ];
};
