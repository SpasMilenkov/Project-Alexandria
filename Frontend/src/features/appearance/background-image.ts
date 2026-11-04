export interface BackgroundImageLoad {
  controller: AbortController;
  generation: number;
}

interface ActiveBackgroundImage {
  key: string;
  updatedAt: string;
  url: string;
}

let activeLoad: BackgroundImageLoad | null = null;
let activeObjectUrl: ActiveBackgroundImage | null = null;
let generation = 0;

export const startBackgroundImageLoad = (): BackgroundImageLoad => {
  generation += 1;
  activeLoad?.controller.abort();

  const load = {
    controller: new AbortController(),
    generation,
  };

  activeLoad = load;

  return load;
};

export const getBackgroundImageLoadGeneration = (): number => generation;

export const isCurrentBackgroundImageLoad = (load: BackgroundImageLoad): boolean =>
  load.generation === generation;

export const finishBackgroundImageLoad = (load: BackgroundImageLoad): void => {
  if (load.generation === generation) {
    activeLoad = null;
  }
};

export const invalidateBackgroundImageLoads = (): void => {
  generation += 1;
  activeLoad?.controller.abort();
  activeLoad = null;
};

export const cancelBackgroundImageLoad = (): void => {
  invalidateBackgroundImageLoads();
  releaseBackgroundImageObjectUrl();
};

export const hasBackgroundImageObjectUrl = (key: string, updatedAt: string): boolean =>
  activeObjectUrl?.key === key && activeObjectUrl.updatedAt === updatedAt;

export const setBackgroundImageObjectUrl = (
  url: string,
  key: string,
  updatedAt: string,
): void => {
  if (activeObjectUrl?.url === url) {
    return;
  }

  releaseBackgroundImageObjectUrl();
  activeObjectUrl = { key, updatedAt, url };
};

export const releaseBackgroundImageObjectUrl = (): void => {
  if (!activeObjectUrl) {
    return;
  }

  URL.revokeObjectURL(activeObjectUrl.url);
  activeObjectUrl = null;
};
