import type { WrappedCardResponse } from "@/api/stats";

import { WrappedCardType } from "@/enums/wrapped-card-type";

interface WrappedSections {
  hero?: WrappedCardResponse;
  artists?: WrappedCardResponse;
  songs?: WrappedCardResponse;
  spotlight: WrappedCardResponse[];
}

// Splits the curated deck into the hero (listening time), the two
// rankings, and remaining moment cards in deck order. The page and
// exported recap share this ordering.
export const buildWrappedSections = (cards: WrappedCardResponse[]): WrappedSections => {
  const sections: WrappedSections = { spotlight: [] };

  for (const card of cards) {
    if (card.type === WrappedCardType.ListeningTime && !sections.hero) {
      sections.hero = card;
    } else if (card.type === WrappedCardType.TopArtists && !sections.artists) {
      sections.artists = card;
    } else if (card.type === WrappedCardType.TopSongs && !sections.songs) {
      sections.songs = card;
    } else {
      sections.spotlight.push(card);
    }
  }

  return sections;
};
