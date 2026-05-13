#!/bin/sh
set -e
BASE="http://localhost:15672/api"
AUTH="-u ${RABBITMQ_ADMIN_USER}:${RABBITMQ_ADMIN_PASS}"
until curl -sf $AUTH "$BASE/overview" > /dev/null 2>&1; do
  echo "Waiting for RabbitMQ management API..."
  sleep 3
done
echo "RabbitMQ is up"
# Vhost
curl -sf -XPUT $AUTH "$BASE/vhosts/${RABBITMQ_VHOST}" \
  && echo "Vhost ready: ${RABBITMQ_VHOST}"
# Users
curl -sf -XPUT $AUTH "$BASE/users/document_worker_user" \
  -H "Content-Type: application/json" \
  -d "{\"password\":\"${DOCUMENT_WORKER_PASS}\",\"tags\":\"\"}" \
  && echo "User ready: document_worker_user"
curl -sf -XPUT $AUTH "$BASE/users/media_worker_user" \
  -H "Content-Type: application/json" \
  -d "{\"password\":\"${MEDIA_WORKER_PASS}\",\"tags\":\"\"}" \
  && echo "User ready: media_worker_user"
curl -sf -XPUT $AUTH "$BASE/users/media_transpilation_worker" \
  -H "Content-Type: application/json" \
  -d "{\"password\":\"${TRANSPILATION_WORKER_PASS}\",\"tags\":\"\"}" \
  && echo "User ready: media_transpilation_worker"
# Permissions
# TODO: remove amq\.gen-.* once workers are updated to use named queues
curl -sf -XPUT $AUTH "$BASE/permissions/${RABBITMQ_VHOST}/document_worker_user" \
  -H "Content-Type: application/json" \
  -d '{
    "configure": "^(content-exchange|document-queue|amq\\.gen-.*)$",
    "write":     "^(content-exchange|document-queue|amq\\.gen-.*)$",
    "read":      "^(content-exchange|document-queue|amq\\.gen-.*)$"
  }' \
  && echo "Permissions set: document_worker_user"
# TODO: remove amq\.gen-.* once workers are updated to use named queues
curl -sf -XPUT $AUTH "$BASE/permissions/${RABBITMQ_VHOST}/media_worker_user" \
  -H "Content-Type: application/json" \
  -d '{
    "configure": "^(content-exchange|content-queue|amq\\.gen-.*)$",
    "write":     "^(content-exchange|content-queue|amq\\.gen-.*)$",
    "read":      "^(content-exchange|content-queue|amq\\.gen-.*)$"
  }' \
  && echo "Permissions set: media_worker_user"
# TODO: remove amq\.gen-.* once workers are updated to use named queues
curl -sf -XPUT $AUTH "$BASE/permissions/${RABBITMQ_VHOST}/media_transpilation_worker" \
  -H "Content-Type: application/json" \
  -d '{
    "configure": "^(content-exchange|transpilation-queue|amq\\.gen-.*)$",
    "write":     "^(content-exchange|transpilation-queue|amq\\.gen-.*)$",
    "read":      "^(content-exchange|transpilation-queue|amq\\.gen-.*)$"
  }' \
  && echo "Permissions set: media_transpilation_worker"
# Exchange
curl -sf -XPUT $AUTH "$BASE/exchanges/${RABBITMQ_VHOST}/content-exchange" \
  -H "Content-Type: application/json" \
  -d '{"type":"topic","durable":true,"auto_delete":false,"arguments":{}}' \
  && echo "Exchange ready: content-exchange"
# Queues
curl -sf -XPUT $AUTH "$BASE/queues/${RABBITMQ_VHOST}/document-queue" \
  -H "Content-Type: application/json" \
  -d '{"durable":true,"auto_delete":false,"arguments":{}}' \
  && echo "Queue ready: document-queue"
curl -sf -XPUT $AUTH "$BASE/queues/${RABBITMQ_VHOST}/content-queue" \
  -H "Content-Type: application/json" \
  -d '{"durable":true,"auto_delete":false,"arguments":{}}' \
  && echo "Queue ready: content-queue"
curl -sf -XPUT $AUTH "$BASE/queues/${RABBITMQ_VHOST}/transpilation-queue" \
  -H "Content-Type: application/json" \
  -d '{"durable":true,"auto_delete":false,"arguments":{}}' \
  && echo "Queue ready: transpilation-queue"
# Bindings
# TODO: remove these once workers declare their own bindings against named queues
curl -sf -XPOST $AUTH "$BASE/bindings/${RABBITMQ_VHOST}/e/content-exchange/q/document-queue" \
  -H "Content-Type: application/json" \
  -d '{"routing_key":"document.#","arguments":{}}' \
  && echo "Binding ready: content-exchange -> document-queue (document.#)"
curl -sf -XPOST $AUTH "$BASE/bindings/${RABBITMQ_VHOST}/e/content-exchange/q/content-queue" \
  -H "Content-Type: application/json" \
  -d '{"routing_key":"media.#","arguments":{}}' \
  && echo "Binding ready: content-exchange -> content-queue (media.#)"
curl -sf -XPOST $AUTH "$BASE/bindings/${RABBITMQ_VHOST}/e/content-exchange/q/content-queue" \
  -H "Content-Type: application/json" \
  -d '{"routing_key":"image.#","arguments":{}}' \
  && echo "Binding ready: content-exchange -> content-queue (image.#)"
curl -sf -XPOST $AUTH "$BASE/bindings/${RABBITMQ_VHOST}/e/content-exchange/q/transpilation-queue" \
  -H "Content-Type: application/json" \
  -d '{"routing_key":"transpilation.#","arguments":{}}' \
  && echo "Binding ready: content-exchange -> transpilation-queue (transpilation.#)"
echo "=== RabbitMQ init complete ==="
