#!/usr/bin/env python3
"""
worker.py - long-lived RabbitMQ consumer. This process never imports
Essentia or TensorFlow: the models live in a child process (see
model_host.py and model_child.py) that is started when a batch arrives,
reused while batches keep coming, and stopped after
ESSENTIA_IDLE_UNLOAD_SECONDS without work (default 300). Stopping the child
is what hands the model memory back to the OS. With the setting at 0 the
models load at startup and stay loaded, as before.

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

The original message is acked once every file in the batch has an output
JSON (success or per-file failure both count; each file's JSON records
which). That includes files the model process could not attempt because it
failed to load or died mid-batch: those get a failure JSON written here. A
malformed message body is nacked without requeue, to avoid a poison-message
loop.

The broker connection is serviced while a batch runs, so heartbeats keep
flowing during long MAEST batches.
"""

import collections
import json
import os
import sys
import time
import traceback

import pika
from model_assets import genre_model_name, mood_model_name
from model_host import ModelHost

RABBITMQ_URL = os.environ.get("RABBITMQ_URL", "amqp://guest:guest@localhost:5672/")
QUEUE_NAME = os.environ.get("QUEUE_NAME")
EXCHANGE_NAME = os.environ.get("EXCHANGE_NAME", "content-exchange")
RESULT_ROUTING_KEY = os.environ.get("RESULT_ROUTING_KEY", "media-metadata.completed")
BACKBONE = os.environ.get("BACKBONE")
PREFETCH_COUNT = int(os.environ.get("PREFETCH_COUNT", "1"))
IDLE_UNLOAD_SETTING = os.environ.get("ESSENTIA_IDLE_UNLOAD_SECONDS", "300")
RECONNECT_DELAY_SECONDS = 5
POLL_SECONDS = 1

if not QUEUE_NAME:
    print("FATAL: QUEUE_NAME environment variable is required.", file=sys.stderr)
    sys.exit(1)
if BACKBONE not in ("effnet", "maest"):
    print("FATAL: BACKBONE must be 'effnet' or 'maest'.", file=sys.stderr)
    sys.exit(1)
if not IDLE_UNLOAD_SETTING.isdigit():
    print("FATAL: ESSENTIA_IDLE_UNLOAD_SECONDS must be a whole number of seconds (0 keeps models loaded).",
          file=sys.stderr)
    sys.exit(1)
IDLE_UNLOAD_SECONDS = int(IDLE_UNLOAD_SETTING)


def log(msg):
    print(msg, flush=True)


def build_connection():
    params = pika.URLParameters(RABBITMQ_URL)
    return pika.BlockingConnection(params)


def parse_batch(body):
    """Returns (batch_id, output_dir, files), or None for a malformed body."""
    try:
        message = json.loads(body)
        batch_id = message["batch_id"]
        output_dir = message["output_dir"]
        files = message["files"]
    except Exception as e:
        log(f"REJECTING malformed message: {e}")
        return None

    if not isinstance(files, list):
        log(f"REJECTING malformed message: 'files' must be a list, got {type(files).__name__}")
        return None

    return batch_id, output_dir, files


def handle_delivery(connection, channel, host, delivery_tag, body):
    parsed = parse_batch(body)
    if parsed is None:
        # Malformed message: drop it, don't requeue (would loop forever).
        channel.basic_nack(delivery_tag=delivery_tag, requeue=False)
        return

    batch_id, output_dir, files = parsed
    log(f"[{batch_id}] starting batch of {len(files)} file(s)")
    os.makedirs(output_dir, exist_ok=True)

    start = time.perf_counter()
    ok_count, fail_count = host.run_batch(
        files=files, output_dir=output_dir,
        keep_alive=lambda: connection.process_data_events(time_limit=0))
    elapsed = time.perf_counter() - start
    log(f"[{batch_id}] done: {ok_count} ok, {fail_count} failed, {elapsed:.1f}s total")

    result = {
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
    channel.basic_publish(
        exchange=EXCHANGE_NAME,
        routing_key=RESULT_ROUTING_KEY,
        body=json.dumps(result),
        properties=pika.BasicProperties(delivery_mode=2),  # persistent
    )
    channel.basic_ack(delivery_tag=delivery_tag)


def consume(host):
    connection = build_connection()
    try:
        channel = connection.channel()
        channel.queue_declare(queue=QUEUE_NAME, durable=True)
        channel.basic_qos(prefetch_count=PREFETCH_COUNT)

        # Deliveries are only queued in the callback and handled from the
        # loop below, so a batch never runs inside pika's dispatch and the
        # connection can be serviced while the model process works.
        deliveries = collections.deque()
        channel.basic_consume(
            queue=QUEUE_NAME,
            on_message_callback=lambda ch, method, properties, body: deliveries.append((method.delivery_tag, body)))
        log(f"Waiting for batches on '{QUEUE_NAME}'...")

        while True:
            connection.process_data_events(time_limit=POLL_SECONDS)
            while deliveries:
                delivery_tag, body = deliveries.popleft()
                handle_delivery(connection, channel, host, delivery_tag, body)
            host.unload_if_idle()
    finally:
        if connection.is_open:
            try:
                connection.close()
            except pika.exceptions.AMQPError as e:
                log(f"Could not close the RabbitMQ connection cleanly: {e}")


def run():
    host = ModelHost(backbone=BACKBONE, idle_unload_seconds=IDLE_UNLOAD_SECONDS, log=log)

    if IDLE_UNLOAD_SECONDS == 0:
        log("ESSENTIA_IDLE_UNLOAD_SECONDS=0: models load now and stay loaded.")
        error = host.load(keep_alive=lambda: None)
        if error is not None:
            log("Continuing without models; the next batch will retry the load.")
    else:
        log(f"Models load on the first batch and unload after {IDLE_UNLOAD_SECONDS}s without work.")

    try:
        while True:
            try:
                consume(host)
            except pika.exceptions.AMQPConnectionError as e:
                log(f"RabbitMQ connection lost/unavailable: {e}. Retrying in {RECONNECT_DELAY_SECONDS}s...")
                time.sleep(RECONNECT_DELAY_SECONDS)
                host.unload_if_idle()
            except Exception as e:
                # Unexpected error in the consume loop itself (not a per-file or
                # per-batch error, those are already handled by the model host).
                log(f"Unexpected error in consumer loop: {e}")
                traceback.print_exc(file=sys.stderr)
                time.sleep(RECONNECT_DELAY_SECONDS)
                host.unload_if_idle()
    except KeyboardInterrupt:
        log("Shutting down.")
    finally:
        host.close()


if __name__ == "__main__":
    run()
