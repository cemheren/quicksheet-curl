# quicksheet-curl

A [QuickSheet](https://github.com/cemheren/QuickSheet) extension that turns cells into a cURL-style HTTP client. Think Postman, but in your spreadsheet wallpaper.

## Install

Type in any cell:

```
ext: github:cemheren/quicksheet-curl
```

## Usage

```
curl: METHOD URL [options] [body]
```

### Examples

| Cell contents | What it does |
|---|---|
| `curl: GET https://api.example.com/users` | GET request, show status + response preview |
| `curl: POST https://api.example.com/users {"name":"Alice"}` | POST with JSON body |
| `curl: PUT https://api.example.com/users/1 {"name":"Bob"}` | PUT to update a resource |
| `curl: DELETE https://api.example.com/users/1` | DELETE a resource |
| `curl: GET https://api.example.com -H "Authorization: Bearer token123"` | GET with auth header |
| `curl: POST https://api.example.com -H "Content-Type: text/xml" --body <data/>` | POST with custom content type |

### Options

| Flag | Description |
|---|---|
| `-H "Header: Value"` | Add request header (can use multiple) |
| `--body DATA` | Explicit request body |
| `--lines N` | Show N lines of response body (default: 3, max: 50) |
| `--headers` | Show response headers |
| `--json` | Pretty-print JSON response |
| `--status-only` | Only show status line (no body) |
| `--timeout Ns` | Request timeout in seconds (default: 10s, max: 60s) |
| `-k` / `--insecure` | Skip TLS certificate verification |

### Output format

```
+ 200 OK | 142ms | application/json
{
  "id": 1,
  "name": "Alice"
... (3 more lines)
```

Status icons: `+` success (2xx), `->` redirect (3xx), `x` client error (4xx), `!` server error (5xx)

### Error handling

```
ERR timeout after 10s
ERR DNS: https://invalid.example
ERR TLS error (try -k flag): certificate not trusted
```

## Supported methods

GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS

## Notes

- URLs without `http://` or `https://` prefix default to `https://`
- JSON bodies are auto-detected (start with `{` or `[`)
- Response bodies are truncated at 100KB
- Sensitive headers (Set-Cookie, Authorization) are redacted in `--headers` output
- Line length capped at 120 chars for clean display

## Requirements

- .NET 9 SDK
- QuickSheet with extension support

## License

MIT
