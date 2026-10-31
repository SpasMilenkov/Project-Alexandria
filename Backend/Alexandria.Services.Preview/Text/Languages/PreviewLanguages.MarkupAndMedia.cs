using Alexandria.Services.Preview.Text.Languages;

namespace Alexandria.Services.Preview.Text;

public static partial class PreviewLanguages
{
    public static readonly LanguageSpec Diff = new() { Name = "diff", Mode = HighlightMode.Diff };

    public static readonly LanguageSpec Srt = new() { Name = "srt", Mode = HighlightMode.Subtitle };
    public static readonly LanguageSpec Vtt = new() { Name = "vtt", Mode = HighlightMode.Subtitle };

    public static readonly LanguageSpec Ass = new()
    {
        Name = "ass",
        LineComments = [";"],
        KeyTerminators = ":",
        HyphenIdents = true,
        Sections = true,
        Quotes = "",
        FnHeuristic = false
    };

    public static readonly LanguageSpec M3u = new()
    {
        Name = "m3u",
        LineComments = ["#"],
        HashNeedsBoundary = true,
        Quotes = "",
        FnHeuristic = false
    };

    public static readonly LanguageSpec Cue = new()
    {
        Name = "cue",
        Keywords = Words(
            "CATALOG CDTEXTFILE FILE FLAGS INDEX ISRC PERFORMER POSTGAP PREGAP SONGWRITER TITLE TRACK MODE",
            ignoreCase: true),
        Literals = Words("AUDIO AIFF BINARY MOTOROLA MP3 WAVE", ignoreCase: true),
        LineComments = ["REM"],
        Quotes = "\"",
        FnHeuristic = false
    };

    public static readonly LanguageSpec Lrc = new()
    {
        Name = "lrc",
        KeyTerminators = ":",
        HyphenIdents = true,
        Sections = true,
        Quotes = "",
        FnHeuristic = false
    };

    public static readonly LanguageSpec Nfo = new() { Name = "nfo", Mode = HighlightMode.Plain };

    public static readonly LanguageSpec Ignore = new()
    {
        Name = "ignore",
        LineComments = ["#"],
        HashNeedsBoundary = true,
        Quotes = "",
        FnHeuristic = false
    };

    public static readonly LanguageSpec Htaccess = new()
    {
        Name = "htaccess",
        Keywords = Words(
            "AcceptPathInfo AccessFileName Action AddDefaultCharset AddHandler AddType Alias Allow AllowOverride " +
            "AuthBasicProvider AuthGroupFile AuthName AuthType AuthUserFile AutoIndex BrowserMatch CGIMapExtension " +
            "ContentDigest CookieDomain CookieName CookieSecure DefaultLanguage DefaultType Deny DirectoryIndex " +
            "DocumentRoot ErrorDocument ErrorLog ExpiresActive ExpiresByType ExpiresDefault FileETag Files FilesMatch " +
            "ForceType Header HeaderName Include IndexIgnore IndexOptions KeepAlive LanguagePriority Limit " +
            "LimitExcept LogLevel MaxRequestWorkers Options Order PassEnv php_flag php_value Redirect RedirectMatch " +
            "RedirectPermanent RedirectTemp RemoveHandler RemoveType Require RewriteBase RewriteCond RewriteEngine " +
            "RewriteMap RewriteOptions RewriteRule Satisfy ScriptAlias ServerName ServerSignature SetEnv SetEnvIf " +
            "SetHandler SetInputFilter SetOutputFilter SSLCertificateFile SSLCertificateKeyFile SSLCipherSuite " +
            "SSLEngine SSLProtocol Timeout User UserDir", ignoreCase: true),
        Literals = Words("On Off None All", ignoreCase: true),
        LineComments = ["#"],
        HashNeedsBoundary = true,
        Quotes = "\"",
        Variables = true,
        PercentVariables = true,
        HyphenIdents = true,
        FnHeuristic = false
    };

    public static readonly LanguageSpec Nginx = new()
    {
        Name = "nginx",
        Keywords = Words(
            "accept_mimetypes access_log add_header allow auth_basic auth_basic_user_file autoindex break charset " +
            "client_body_buffer_size client_header_buffer_size client_max_body_size daemon default_type deny " +
            "error_log error_page events fastcgi_param fastcgi_pass geo gzip gzip_types http if include index " +
            "keepalive_timeout listen location log_format map open_file_cache pcre_jit pid proxy_connect_timeout " +
            "proxy_pass proxy_read_timeout proxy_redirect proxy_set_header return rewrite root sendfile server " +
            "server_name server_tokens set ssl_certificate ssl_certificate_key ssl_ciphers ssl_protocols " +
            "tcp_nodelay tcp_nopush types upstream user worker_connections worker_processes"),
        LineComments = ["#"],
        HashNeedsBoundary = true,
        Quotes = "\"",
        Variables = true,
        HyphenIdents = true,
        FnHeuristic = false
    };

    public static readonly LanguageSpec Xml = new() { Name = "xml", Mode = HighlightMode.Markup };
    public static readonly LanguageSpec Markdown = new() { Name = "markdown", Mode = HighlightMode.Markdown };
}
