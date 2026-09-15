export const MEDIA_GRID_GAP = 16;
export const MEDIA_CARD_METADATA_HEIGHT = 112;
export const MEDIA_CARD_BORDER_WIDTH = 1;

export const getMediaGridLayout = (width: number, mediaType: "audio" | "video") => {
  const availableWidth = Math.max(1, width);
  const minCardWidth = mediaType === "audio" ? 192 : 256;
  const columns = Math.max(
    1,
    Math.floor((availableWidth + MEDIA_GRID_GAP) / (minCardWidth + MEDIA_GRID_GAP)) - 1,
  );
  const cardWidth = (availableWidth - (columns - 1) * MEDIA_GRID_GAP) / columns;
  const artworkWidth = Math.max(0, cardWidth - MEDIA_CARD_BORDER_WIDTH * 2);
  const artworkHeight = artworkWidth * (mediaType === "video" ? 9 / 16 : 1);
  // Match the complete card, with rounding room for fractional CSS pixels.
  const rowHeight = Math.ceil(
    artworkHeight + MEDIA_CARD_METADATA_HEIGHT + MEDIA_CARD_BORDER_WIDTH * 2 + MEDIA_GRID_GAP,
  );
  return { columns, rowHeight };
};
