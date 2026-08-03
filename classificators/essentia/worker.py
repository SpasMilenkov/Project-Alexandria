#!/usr/bin/env python3
"""
worker.py - long-lived RabbitMQ consumer. Loads models ONCE at startup and
reuses them across every batch for the lifetime of the process, this is
what actually amortizes the model load cost you benchmarked earlier.

Run as two separate containers/deployments, one per backbone:
    BACKBONE=effnet QUEUE_NAME=classify.effnet   python worker.py
    BACKBONE=maest  QUEUE_NAME=classify.maest    python worker.py

Expected input message (JSON body), one message per batch:
    {
      "batch_id": "b47f...",
      "output_dir": "/data/output/b47f...",
      "files": ["/data/input/song1.mp3", "/data/input/song2.mp3", ...]
    }

.NET decides output_dir per batch (e.g. a per-batch subfolder to avoid
collisions between concurrently-running batches), and is responsible for
creating/mounting the corresponding volume paths.

After a batch is processed, a completion message is published to
EXCHANGE_NAME (content-exchange) with routing key RESULT_ROUTING_KEY,
which routes to the results queue:
    {
      "batch_id": "b47f...",
      "backbone": "effnet",
      "ok_count": 9,
      "fail_count": 1,
      "output_dir": "/data/output/b47f...",
      "seconds": 38.2,
      "model_versions": { "genre": "...", "mood": "..." }
    }

The original message is acked once every file in the batch has been
attempted (success or per-file failure both count as "attempted", each
file's own JSON already records which). It is only nacked (without
requeue, to avoid a poison-message loop) if something fails before or
outside the per-file processing itself, e.g. malformed message body,
inability to create the output directory.
"""

import json
import os
import sys
import time
import traceback

import pika
from essentia_core import Models, genre_model_name, mood_model_name, process_file

RABBITMQ_URL = os.environ.get("RABBITMQ_URL", "amqp://guest:guest@localhost:5672/")
QUEUE_NAME = os.environ.get("QUEUE_NAME")
EXCHANGE_NAME = os.environ.get("EXCHANGE_NAME", "content-exchange")
RESULT_ROUTING_KEY = os.environ.get("RESULT_ROUTING_KEY", "media-metadata.completed")
BACKBONE = os.environ.get("BACKBONE")
PREFETCH_COUNT = int(os.environ.get("PREFETCH_COUNT", "1"))
RECONNECT_DELAY_SECONDS = 5

if not QUEUE_NAME:
    print("FATAL: QUEUE_NAME environment variable is required.", file=sys.stderr)
    sys.exit(1)
if BACKBONE not in ("effnet", "maest"):
    print("FATAL: BACKBONE must be 'effnet' or 'maest'.", file=sys.stderr)
    sys.exit(1)


def log(msg):
    print(msg, flush=True)


def build_connection():
    params = pika.URLParameters(RABBITMQ_URL)
    return pika.BlockingConnection(params)


def handle_batch(channel, models, body):
    try:
        message = json.loads(body)
        batch_id = message["batch_id"]
        output_dir = message["output_dir"]
        files = message["files"]
    except Exception as e:
        # Malformed message, nothing we can do with it. Log and give up on
        # this specific message rather than crash the whole consumer loop.
        log(f"REJECTING malformed message: {e}")
        return None  # signals caller to nack without requeue

    log(f"[{batch_id}] starting batch of {len(files)} file(s)")
    os.makedirs(output_dir, exist_ok=True)

    ok_count = 0
    fail_count = 0
    start = time.perf_counter()

    for input_path in files:
        succeeded = process_file(models, input_path, output_dir)
        if succeeded:
            ok_count += 1
        else:
            fail_count += 1

    elapsed = time.perf_counter() - start
    log(f"[{batch_id}] done: {ok_count} ok, {fail_count} failed, {elapsed:.1f}s total")

    return {
        "batch_id": batch_id,
        "backbone": BACKBONE,
        "ok_count": ok_count,
        "fail_count": fail_count,
        "output_dir": output_dir,
        "seconds": elapsed,
        "model_versions": {
            "genre": genre_model_name(BACKBONE),
            "mood": mood_model_name(),
        },
    }


def run():
    log(f"Loading models for backbone={BACKBONE}...")
    load_start = time.perf_counter()
    models = Models(BACKBONE)
    log(f"Models loaded in {time.perf_counter() - load_start:.1f}s. Ready to consume from '{QUEUE_NAME}'.")

    while True:
        try:
            connection = build_connection()
            channel = connection.channel()
            channel.queue_declare(queue=QUEUE_NAME, durable=True)
            channel.basic_qos(prefetch_count=PREFETCH_COUNT)

            def on_message(ch, method, properties, body):
                result = handle_batch(ch, models, body)
                if result is None:
                    # Malformed message: drop it, don't requeue (would loop forever).
                    ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)
                    return
                ch.basic_publish(
                    exchange=EXCHANGE_NAME,
                    routing_key=RESULT_ROUTING_KEY,
                    body=json.dumps(result),
                    properties=pika.BasicProperties(delivery_mode=2),  # persistent
                )
                ch.basic_ack(delivery_tag=method.delivery_tag)

            channel.basic_consume(queue=QUEUE_NAME, on_message_callback=on_message)
            log("Waiting for batches...")
            channel.start_consuming()

        except pika.exceptions.AMQPConnectionError as e:
            log(f"RabbitMQ connection lost/unavailable: {e}. Retrying in {RECONNECT_DELAY_SECONDS}s...")
            time.sleep(RECONNECT_DELAY_SECONDS)
        except KeyboardInterrupt:
            log("Shutting down.")
            break
        except Exception as e:
            # Unexpected error in the consume loop itself (not a per-file or
            # per-batch error, those are already handled inside handle_batch).
            log(f"Unexpected error in consumer loop: {e}")
            traceback.print_exc(file=sys.stderr)
            time.sleep(RECONNECT_DELAY_SECONDS)


if __name__ == "__main__":
    run()
