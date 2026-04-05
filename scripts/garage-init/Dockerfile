FROM dxflrs/garage:v2.2.0 AS garage-bin
FROM amazon/aws-cli:2.34.19 AS final
COPY --from=garage-bin /garage /garage
COPY scripts/garage-init/init.sh /scripts/init.sh
RUN chmod +x /scripts/init.sh
ENTRYPOINT ["/bin/sh", "/scripts/init.sh"]