FROM dxflrs/garage:v2.2.0@sha256:45a61ce3f7c9c24fc23d9ed2b09b27ed560ab87b34605d175d5c588f539c24e4 AS garage-bin
FROM amazon/aws-cli:2.34.19@sha256:2a42ab831c97444db6294a2e9c87d09aee6c41830acae8445804a5d4d0b78c20 AS final
COPY --from=garage-bin /garage /garage
COPY scripts/garage-init/init.sh /scripts/init.sh
RUN chmod +x /scripts/init.sh
ENTRYPOINT ["/bin/sh", "/scripts/init.sh"]
