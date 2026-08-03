FROM python:3.12-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    libsndfile1 \
    && rm -rf /var/lib/apt/lists/*

RUN pip install --no-cache-dir \
    essentia-tensorflow==2.1b6.dev1389 \
    numpy==2.5.1 \
    pika==1.4.2

WORKDIR /app

COPY essentia_core.py /app/essentia_core.py
COPY classify.py /app/classify.py
COPY worker.py /app/worker.py
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Models are NOT baked into the image. They're mounted at runtime from a
# host directory that is populated once via download_models.sh, so re-running
# `docker build` never re-downloads them and the image stays small. Both
# the effnet and maest worker containers use this SAME image, they just
# get different BACKBONE/QUEUE_NAME env vars at `docker run`/compose time.
ENV ESSENTIA_MODEL_DIR=/app/models
ENV VERIFY_MODELS=true

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["python", "worker.py"]
