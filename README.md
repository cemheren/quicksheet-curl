# quicksheet-curl

A [QuickSheet](https://github.com/cemheren/QuickSheet) extension that turns cells into a cURL-style HTTP client. Think Postman, but in your spreadsheet wallpaper.

## Install

Type in any cell:

```
ext: github:Deskworks/quicksheet-curl
```

## Usage

```
curl: METHOD URL [options] [body]
```

### Basic examples

| Cell contents | What it does |
|---|---|
| `curl: GET https://api.example.com/users` | GET request, show status + response preview |
| `curl: POST https://api.example.com/users {"name":"Alice"}` | POST with JSON body |
| `curl: PUT https://api.example.com/users/1 {"name":"Bob"}` | PUT to update a resource |
| `curl: DELETE https://api.example.com/users/1` | DELETE a resource |
| `curl: GET https://api.example.com -H "Authorization: Bearer token123"` | GET with auth header |
| `curl: POST https://api.example.com -H "Content-Type: text/xml" --body <data/>` | POST with custom content type |

### Using cell references

QuickSheet supports `{CellRef}` syntax to reference other cells. This lets you build dynamic requests where URLs, tokens, and bodies come from other cells in your spreadsheet.

**Example layout — API testing dashboard:**

| | A | B |
|---|---|---|
| 1 | `https://api.example.com` | *(base URL)* |
| 2 | `Bearer sk-abc123...` | *(auth token)* |
| 3 | `{"name":"Alice","role":"admin"}` | *(request body)* |
| 4 | `curl: GET {A1}/users -H "Authorization: {A2}"` | → fetches user list |
| 5 | `curl: POST {A1}/users --body {A3} -H "Authorization: {A2}"` | → creates user with body from A3 |
| 6 | `curl: GET {A1}/users/1 --headers --json` | → detailed single-user view |

**Why this is useful:**
- Change the base URL in A1 to switch between staging/production
- Rotate the auth token in A2 without editing every request
- Modify the POST body in A3 and re-run to test different payloads
- Build a full API test suite across rows, all sharing the same config

**More cell reference patterns:**

| Cell | Contents | Notes |
|---|---|---|
| A1 | `https://httpbin.org` | Base URL |
| A2 | `my-api-key-123` | API key |
| B1 | `curl: GET {A1}/get` | Simple GET |
| B2 | `curl: GET {A1}/headers -H "X-Api-Key: {A2}"` | GET with key from A2 |
| B3 | `curl: POST {A1}/post {"key":"{A2}"}` | POST embedding the key in JSON body |
| B4 | `curl: GET {A1}/status/404 --status-only` | Test error handling |

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

## Use cases

- **API development** — test endpoints without leaving your desktop
- **Service monitoring** — pin health checks to your wallpaper with `--status-only`
- **Webhook debugging** — POST test payloads to webhook receivers
- **Auth testing** — quickly cycle through tokens with cell references
- **CI/CD checks** — monitor deployment endpoints alongside your other dashboards

## Notes

- URLs without `http://` or `https://` prefix default to `https://`
- JSON bodies are auto-detected (start with `{` or `[`)
- Response bodies are truncated at 100KB
- Sensitive headers (Set-Cookie, Authorization) are redacted in `--headers` output
- Line length capped at 120 chars for clean display
- Cell references (`{A1}`, `{B2}`) are resolved by QuickSheet before the extension sees them

## Requirements

- .NET 9 SDK
- QuickSheet with extension support

## License

MIT
