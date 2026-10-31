export interface RenderedSegment {
  text: string;
  kind: string | null;
}

export interface RenderedLine {
  number: number;
  segments: RenderedSegment[];
}
