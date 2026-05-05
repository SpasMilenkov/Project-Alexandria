FROM alpine:3.21 AS builder
ARG NGINX_VERSION=1.28.3
ARG NGX_BROTLI_COMMIT=a71f9312c2deb28875acc7bacfdd5695a111aa53

RUN apk add --no-cache \
    build-base git cmake curl pax-utils \
    openssl-dev pcre2-dev zlib-dev \
    linux-headers gnupg gettext

# Clone ngx_brotli at pinned commit
RUN git clone --recurse-submodules https://github.com/google/ngx_brotli /usr/src/ngx_brotli \
    && git -C /usr/src/ngx_brotli checkout ${NGX_BROTLI_COMMIT}

RUN cd /usr/src/ngx_brotli/deps/brotli \
    && mkdir -p out && cd out \
    && cmake -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=OFF \
             -DCMAKE_C_FLAGS="-fPIC" .. \
    && make -j$(nproc) brotlienc brotlidec brotlicommon

# Download + verify nginx source
COPY gpg/arut.key /tmp/arut.key

RUN gpg --import /tmp/arut.key \
    && curl -fSL https://nginx.org/download/nginx-${NGINX_VERSION}.tar.gz -o /tmp/nginx.tar.gz \
    && curl -fSL https://nginx.org/download/nginx-${NGINX_VERSION}.tar.gz.asc -o /tmp/nginx.tar.gz.asc \
    && gpg --verify /tmp/nginx.tar.gz.asc /tmp/nginx.tar.gz \
    && tar -C /usr/src -xzf /tmp/nginx.tar.gz \
    && rm -f /tmp/nginx.tar.gz /tmp/nginx.tar.gz.asc

RUN cd /usr/src/nginx-${NGINX_VERSION} && ./configure \
        --prefix=/etc/nginx \
        --sbin-path=/usr/sbin/nginx \
        --modules-path=/usr/lib/nginx/modules \
        --conf-path=/etc/nginx/nginx.conf \
        --error-log-path=/var/log/nginx/error.log \
        --http-log-path=/var/log/nginx/access.log \
        --pid-path=/var/run/nginx.pid \
        --lock-path=/var/run/nginx.lock \
        --http-client-body-temp-path=/var/cache/nginx/client_temp \
        --http-proxy-temp-path=/var/cache/nginx/proxy_temp \
        --http-fastcgi-temp-path=/var/cache/nginx/fastcgi_temp \
        --http-uwsgi-temp-path=/var/cache/nginx/uwsgi_temp \
        --http-scgi-temp-path=/var/cache/nginx/scgi_temp \
        --user=nginx \
        --group=nginx \
        --with-http_ssl_module \
        --with-http_realip_module \
        --with-http_addition_module \
        --with-http_sub_module \
        --with-http_gunzip_module \
        --with-http_gzip_static_module \
        --with-http_secure_link_module \
        --with-http_stub_status_module \
        --with-http_auth_request_module \
        --with-threads \
        --with-stream \
        --with-stream_ssl_module \
        --with-stream_ssl_preread_module \
        --with-stream_realip_module \
        --with-http_slice_module \
        --with-compat \
        --with-file-aio \
        --with-http_v2_module \
        --add-module=/usr/src/ngx_brotli \
    && make -j$(nproc) \
    && make install \
    && strip /usr/sbin/nginx \
    && if [ -n "$(ls /usr/lib/nginx/modules/*.so 2>/dev/null)" ]; then strip /usr/lib/nginx/modules/*.so; fi

# Detect runtime deps from the compiled binary and modules
RUN scanelf --needed --nobanner /usr/sbin/nginx /usr/bin/envsubst $(ls /usr/lib/nginx/modules/*.so 2>/dev/null) \
        | awk '{ gsub(/,/, "\nso:", $2); print "so:" $2 }' \
        | sort -u \
        | xargs -r apk info --installed \
        | sort -u > /tmp/runDeps.txt

# ---- Runtime image ----
FROM alpine:3.21
ARG NGINX_VERSION

LABEL org.opencontainers.image.version="${NGINX_VERSION}"

COPY --from=builder /tmp/runDeps.txt /tmp/runDeps.txt

RUN apk add --no-cache tini tzdata \
    && apk add --no-cache --virtual .nginx-rundeps $(cat /tmp/runDeps.txt) \
    && rm /tmp/runDeps.txt \
    && addgroup -S nginx \
    && adduser -D -S -h /var/cache/nginx -s /sbin/nologin -G nginx nginx \
    && mkdir -p /var/log/nginx /var/cache/nginx /usr/lib/nginx/modules /etc/nginx /etc/nginx/certs \
    && touch /var/log/nginx/access.log /var/log/nginx/error.log \
    && ln -sf /dev/stdout /var/log/nginx/access.log \
    && ln -sf /dev/stderr /var/log/nginx/error.log \
    && ln -s /usr/lib/nginx/modules /etc/nginx/modules

COPY --from=builder /usr/sbin/nginx                 /usr/sbin/nginx
# Uncomment if I add dynamic modules to nginx later
# COPY --from=builder /usr/lib/nginx/modules          /usr/lib/nginx/modules
COPY --from=builder /etc/nginx                      /etc/nginx
COPY --from=builder /usr/bin/envsubst               /usr/local/bin/envsubst
COPY nginx.conf                                     /etc/nginx/nginx.conf

EXPOSE 80 443

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1/health || exit 1

STOPSIGNAL SIGQUIT

ENTRYPOINT ["/sbin/tini", "--"]
CMD ["nginx", "-g", "daemon off;"]
