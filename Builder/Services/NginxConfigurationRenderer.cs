namespace Builder.Services;

public static class NginxConfigurationRenderer
{
    public static string Render(string publicOrigin, bool monitoring)
    {
        if (string.IsNullOrWhiteSpace(publicOrigin)
            || publicOrigin.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            || !Uri.TryCreate(publicOrigin, UriKind.Absolute, out var origin)
            || origin.Scheme != Uri.UriSchemeHttps
            || origin.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4)
            || origin.Port < 1
            || origin.IdnHost.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-'))
            || !string.IsNullOrEmpty(origin.UserInfo)
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment))
        {
            throw new ArgumentException("The proxy requires an HTTPS origin with a DNS name or IPv4 address and no path, credentials, query, or fragment.", nameof(publicOrigin));
        }

        var authority = origin.IdnHost;

        if (!origin.IsDefaultPort)
        {
            authority += $":{origin.Port}";
        }

        var canonicalOrigin = $"https://{authority}";
        var monitoringConfiguration = monitoring
            ? MonitoringConfiguration.Replace("{{PUBLIC_ORIGIN}}", canonicalOrigin, StringComparison.Ordinal)
                .Replace("{{MONITORING_SECURITY_HEADERS}}", Indent(CommonSecurityHeaders, 12), StringComparison.Ordinal)
            : string.Empty;

        return Configuration
            .Replace("{{PUBLIC_HOST}}", origin.IdnHost, StringComparison.Ordinal)
            .Replace("{{SERVER_SECURITY_HEADERS}}", Indent(SecurityHeaders, 8), StringComparison.Ordinal)
            .Replace("{{LOCATION_SECURITY_HEADERS}}", Indent(SecurityHeaders, 12), StringComparison.Ordinal)
            .Replace("{{MONITORING_MAP}}", monitoring ? MonitoringMap : string.Empty, StringComparison.Ordinal)
            .Replace("{{MONITORING_CONFIGURATION}}", monitoringConfiguration, StringComparison.Ordinal)
            + "\n";
    }

    private static string Indent(string text, int count)
    {
        var indentation = new string(' ', count);

        return string.Join('\n', text.Split('\n').Select(line => indentation + line));
    }

    private const string CommonSecurityHeaders = """
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header Strict-Transport-Security "max-age=63072000; includeSubDomains" always;
        add_header Referrer-Policy "strict-origin-when-cross-origin" always;
        """;

    private const string SecurityHeaders = CommonSecurityHeaders + "\n" + """
        add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'sha256-tYCcUbFfjZ9QESuTWESGWrFg2SmiEdyD2MYUfRWUgK0='; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; media-src 'self' blob:; worker-src 'self' blob:; font-src 'self' https://fonts.gstatic.com; connect-src 'self';" always;
        """;

    private const string MonitoringMap = """
            map $http_upgrade $connection_upgrade {
                default upgrade;
                '' close;
            }
        """;

    private const string MonitoringConfiguration = """
                location = /monitoring {
                    return 308 {{PUBLIC_ORIGIN}}/monitoring/$is_args$args;
                }

                location /monitoring/ {
                    proxy_pass http://grafana:3000;
                    proxy_http_version 1.1;
                    proxy_set_header Host $http_host;
                    proxy_set_header X-Real-IP $remote_addr;
                    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
                    proxy_set_header X-Forwarded-Host $http_host;
                    proxy_set_header X-Forwarded-Proto $scheme;
                    proxy_set_header Upgrade $http_upgrade;
                    proxy_set_header Connection $connection_upgrade;
                    proxy_redirect http://grafana:3000/ {{PUBLIC_ORIGIN}}/;
                    proxy_read_timeout 300s;
                    proxy_send_timeout 300s;
                    proxy_buffering off;

        {{MONITORING_SECURITY_HEADERS}}
                }
        """;

    private const string Configuration = """
        user nginx;
        worker_processes auto;
        error_log /var/log/nginx/error.log warn;
        pid /tmp/nginx.pid;

        events {
            worker_connections 1024;
        }

        http {
            server_tokens off;
            include /etc/nginx/mime.types;
            default_type application/octet-stream;

            log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                            '$status $body_bytes_sent "$http_referer" '
                            '"$http_user_agent"';
            access_log /var/log/nginx/access.log main;

            limit_req_zone $binary_remote_addr zone=s3:10m rate=6000r/m;
            limit_conn_zone $binary_remote_addr zone=s3_conn:10m;
            limit_req_zone $binary_remote_addr zone=api:10m rate=200r/m;
            limit_req_zone $binary_remote_addr zone=thumb:10m rate=3000r/m;

            sendfile on;
            tcp_nopush on;
            tcp_nodelay on;
            keepalive_timeout 65;

            brotli on;
            brotli_comp_level 6;
            brotli_types text/plain text/css application/javascript application/json image/svg+xml;

            gzip on;
            gzip_vary on;
            gzip_proxied any;
            gzip_comp_level 6;
            gzip_min_length 256;
            gzip_types
                text/plain
                text/css
                text/xml
                text/javascript
                application/javascript
                application/json
                application/xml
                image/svg+xml;

            client_max_body_size 500M;
            http2 on;
            resolver 127.0.0.11 valid=30s ipv6=off;

            proxy_cache_path /var/cache/nginx/previews levels=1:2 keys_zone=previews_cache:50m max_size=5g inactive=30d use_temp_path=off;

        {{MONITORING_MAP}}

            server {
                listen 127.0.0.1:8081;
                server_name localhost;

                location = /health {
                    access_log off;
                    default_type text/plain;
                    return 200 "ok\n";
                }

                location / {
                    return 404;
                }
            }

            server {
                listen 443 ssl;
                server_name {{PUBLIC_HOST}};
                absolute_redirect off;

                ssl_certificate     /etc/nginx/certs/server.crt;
                ssl_certificate_key /etc/nginx/certs/server.key;
                ssl_protocols TLSv1.2 TLSv1.3;
                ssl_ciphers 'ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305';
                ssl_prefer_server_ciphers off;
                ssl_session_cache shared:SSL:10m;
                ssl_session_timeout 1d;
                ssl_session_tickets off;

        {{SERVER_SECURITY_HEADERS}}

                location = /health {
                    access_log off;
                    default_type text/plain;
                    return 200 "ok\n";
                }

                location /api/ {
                    limit_req zone=api burst=100 nodelay;
                    proxy_pass http://api:8080/api/;
                    proxy_set_header Host $http_host;
                    proxy_set_header X-Real-IP $remote_addr;
                    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
                    proxy_set_header X-Forwarded-Host $http_host;
                    proxy_set_header X-Forwarded-Proto $scheme;
                    proxy_read_timeout 300s;
                    proxy_send_timeout 300s;
                    client_max_body_size 500M;
                }

                location ~ ^/api/files/[0-9a-fA-F-]+(/versions/[0-9a-fA-F-]+)?/(preview|thumbnail)$ {
                    limit_req zone=thumb burst=300 nodelay;
                    proxy_pass http://api:8080$request_uri;
                    proxy_set_header Host $http_host;
                    proxy_set_header X-Real-IP $remote_addr;
                    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
                    proxy_set_header X-Forwarded-Host $http_host;
                    proxy_set_header X-Forwarded-Proto $scheme;
                }

                location = /api/auth/stream-check {
                    internal;
                    proxy_pass http://api:8080/api/auth/stream-check;
                    proxy_pass_request_body off;
                    proxy_set_header Content-Length "";
                    proxy_set_header X-Original-URI $request_uri;
                    proxy_set_header Cookie $http_cookie;
                    proxy_set_header Host $http_host;
                    proxy_set_header X-Forwarded-Proto $scheme;
                }

                location = /api/auth/preview-check {
                    internal;
                    proxy_pass http://api:8080/api/auth/preview-check;
                    proxy_pass_request_body off;
                    proxy_set_header Content-Length "";
                    proxy_set_header X-Original-URI $request_uri;
                    proxy_set_header Cookie $http_cookie;
                    proxy_set_header Host $http_host;
                    proxy_set_header X-Forwarded-Proto $scheme;
                }

                location = /api/auth/cover-check {
                    internal;
                    proxy_pass http://api:8080/api/auth/cover-check;
                    proxy_pass_request_body off;
                    proxy_set_header Content-Length "";
                    proxy_set_header X-Original-URI $request_uri;
                    proxy_set_header Cookie $http_cookie;
                    proxy_set_header Host $http_host;
                    proxy_set_header X-Forwarded-Proto $scheme;
                }

                location /stream/ {
                    auth_request /api/auth/stream-check;

                    rewrite ^/stream/(.*)$ /$1 break;

                    proxy_pass http://garage:3902;
                    proxy_set_header Host alexandria-streaming.web.garage.localhost;

                    proxy_set_header Range $http_range;
                    proxy_set_header If-Range $http_if_range;
                    proxy_pass_header Content-Range;

                    proxy_buffering off;
                    proxy_request_buffering off;
                    proxy_read_timeout 120s;
                }

                location ~ ^/preview/(?<version_id>[0-9a-fA-F-]+)/(?<kind>preview|thumbnail)$ {
                    auth_request /api/auth/preview-check;
                    auth_request_set $content_path $upstream_http_x_content_path;

                    limit_conn s3_conn 300;

                    proxy_cache previews_cache;
                    proxy_cache_key "$scheme$host$content_path";
                    proxy_cache_valid 200 30d;
                    proxy_cache_valid 404 1m;
                    proxy_cache_valid 500 502 503 504 5s;

                    proxy_cache_lock on;
                    proxy_cache_lock_timeout 10s;
                    proxy_cache_lock_age 10s;

                    proxy_cache_use_stale error timeout updating http_500 http_502 http_503 http_504;
                    proxy_cache_background_update on;

        {{LOCATION_SECURITY_HEADERS}}
                    add_header Cache-Control "public, max-age=604800, immutable" always;
                    add_header X-Cache-Status $upstream_cache_status;

                    proxy_pass http://garage:3902/$content_path;
                    proxy_set_header Host alexandria-previews.web.garage.localhost;
                    client_max_body_size 500M;
                    proxy_read_timeout 300s;
                }

                location ~ ^/covers/(?<playlist_id>[0-9a-fA-F-]+)$ {
                    auth_request /api/auth/cover-check;
                    auth_request_set $content_path $upstream_http_x_content_path;

                    limit_conn s3_conn 300;

                    proxy_cache previews_cache;
                    proxy_cache_key "$scheme$host$content_path$arg_v";
                    proxy_cache_valid 200 30d;
                    proxy_cache_valid 404 1m;
                    proxy_cache_valid 500 502 503 504 5s;

                    proxy_cache_lock on;
                    proxy_cache_lock_timeout 10s;
                    proxy_cache_lock_age 10s;

                    proxy_cache_use_stale error timeout updating http_500 http_502 http_503 http_504;
                    proxy_cache_background_update on;

        {{LOCATION_SECURITY_HEADERS}}
                    add_header Cache-Control "public, max-age=31536000" always;
                    add_header X-Cache-Status $upstream_cache_status;

                    proxy_pass http://garage:3902/$content_path;
                    proxy_set_header Host alexandria-previews.web.garage.localhost;
                    client_max_body_size 500M;
                    proxy_read_timeout 300s;
                }

                location ~ ^/alexandria-previews/covers/ {
                    limit_except PUT {
                        deny all;
                    }

                    limit_conn s3_conn 300;
                    proxy_pass http://garage:3900;
                    proxy_set_header Host $http_host;
                    client_max_body_size 500M;
                    proxy_read_timeout 300s;
                }

                location ~ ^/alexandria-images/ {
                    limit_conn s3_conn 300;
                    proxy_pass http://garage:3900;
                    proxy_set_header Host $http_host;
                    client_max_body_size 500M;
                    proxy_read_timeout 300s;
                }

                location ~ ^/(alexandria-files|alexandria-temp)/ {
                    limit_conn s3_conn 300;
                    proxy_pass http://garage:3900;
                    proxy_set_header Host $http_host;
                    client_max_body_size 500M;
                    proxy_read_timeout 300s;
                }

        {{MONITORING_CONFIGURATION}}

                location / {
                    proxy_pass http://frontend:8080;
                    proxy_set_header Host $http_host;
                    proxy_set_header X-Real-IP $remote_addr;
                    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
                    proxy_set_header X-Forwarded-Host $http_host;
                    proxy_set_header X-Forwarded-Proto $scheme;
                }
            }
        }
        """;
}
