FROM dxflrs/garage:v2.2.0@sha256:45a61ce3f7c9c24fc23d9ed2b09b27ed560ab87b34605d175d5c588f539c24e4 AS garage-bin
FROM alpine:3.21@sha256:ce64758a109eb420d874a118f87920e625e12d3634e03b4a5573fd9f6e5d3507 AS final
# init.sh validates Garage's JSON layout and credential receipts with python3.
RUN apk add --no-cache python3
COPY --from=garage-bin /garage /garage
COPY scripts/garage-init/init.sh /scripts/init.sh
RUN chmod +x /scripts/init.sh
ENTRYPOINT ["/bin/sh", "/scripts/init.sh"]
