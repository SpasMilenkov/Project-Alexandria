FROM python:3.12-slim@sha256:646fb0bca3dd3ea1bcc6feb72c17ed16eed6e10cffc732fcc1478bd3e7f02d7b

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

# Shared /data volume ownership: local.yml runs this image as uid 1001 (the
# same uid the .NET media-metadata worker uses) so both sides can clean up each
# other's files. The named volume inherits this ownership on first mount.
RUN groupadd --gid 1001 appgroup && useradd --uid 1001 --gid appgroup --no-create-home appuser \
    && mkdir -p /data && chown appuser:appgroup /data
USER appuser
ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["python", "worker.py"]
