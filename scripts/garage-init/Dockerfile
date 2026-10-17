FROM dxflrs/garage:v2.2.0 AS garage-bin
FROM alpine:3.21@sha256:ce64758a109eb420d874a118f87920e625e12d3634e03b4a5573fd9f6e5d3507 AS final
COPY --from=garage-bin /garage /garage
COPY scripts/garage-init/init.sh /scripts/init.sh
RUN chmod +x /scripts/init.sh
ENTRYPOINT ["/bin/sh", "/scripts/init.sh"]