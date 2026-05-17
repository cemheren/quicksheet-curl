using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

// QuickSheet HTTP Client Extension - Postman-in-a-cell
// Prefix: http:
// Syntax: http: METHOD URL [-H "Header: Value"] [--body JSON] [--lines N] [--headers] [--timeout Ns]

#region Protocol types

class InitMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("version")] public int Version { get; set; }
}

class RegisterMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = "register";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("prefix")] public string Prefix { get; set; } = "";
    [JsonPropertyName("author")] public string Author { get; set; } = "";
}

class ActivateMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("params")] public List<string> Params { get; set; } = new();
}

class CellUpdate
{
    [JsonPropertyName("r")] public int R { get; set; }
    [JsonPropertyName("c")] public int C { get; set; }
    [JsonPropertyName("v")] public string V { get; set; } = "";
}

class ResponseMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = "response";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("cells")] public List<CellUpdate> Cells { get; set; } = new();
}

#endregion

#region Parsed request

class ParsedHttpRequest
{
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public int MaxLines { get; set; } = 3;
    public bool ShowHeaders { get; set; }
    public bool PrettyJson { get; set; }
    public bool StatusOnly { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    public bool SkipTlsVerify { get; set; }
}

#endregion

class Program
{
    static readonly HashSet<string> ValidMethods = new(StringComparer.OrdinalIgnoreCase)
        { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };

    static async Task Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        string? initLine = Console.ReadLine();
        if (initLine == null) return;

        try { JsonSerializer.Deserialize<InitMessage>(initLine); }
        catch { return; }

        var reg = new RegisterMessage
        {
            Name = "curl",
            Version = "1.0.0",
            Description = "cURL-style HTTP client — GET/POST/PUT/DELETE from cells (Postman-in-a-cell)",
            Prefix = "curl:",
            Author = "cemheren"
        };
        Console.WriteLine(JsonSerializer.Serialize(reg));

        while (true)
        {
            string? line = Console.ReadLine();
            if (line == null) break;

            ActivateMessage? msg;
            try { msg = JsonSerializer.Deserialize<ActivateMessage>(line); }
            catch { continue; }
            if (msg == null || msg.Type != "activate") continue;

            var input = msg.Params.Count > 0 ? msg.Params[0] : "";
            var cells = await ProcessRequest(input);

            var resp = new ResponseMessage { Id = msg.Id, Cells = cells };
            Console.WriteLine(JsonSerializer.Serialize(resp));
        }
    }

    static async Task<List<CellUpdate>> ProcessRequest(string input)
    {
        var cells = new List<CellUpdate>();
        input = input.Trim();

        if (string.IsNullOrEmpty(input))
        {
            cells.Add(new CellUpdate { R = 0, C = 0, V = "Usage: curl: METHOD URL [-H \"Header\"] [--body JSON]" });
            cells.Add(new CellUpdate { R = 1, C = 0, V = "Methods: GET POST PUT DELETE PATCH HEAD OPTIONS" });
            cells.Add(new CellUpdate { R = 2, C = 0, V = "Flags: --lines N --headers --json --status-only --timeout Ns -k" });
            return cells;
        }

        ParsedHttpRequest req;
        try { req = ParseInput(input); }
        catch (Exception ex)
        {
            cells.Add(new CellUpdate { R = 0, C = 0, V = "ERR parse: " + ex.Message });
            return cells;
        }

        if (string.IsNullOrEmpty(req.Url))
        {
            cells.Add(new CellUpdate { R = 0, C = 0, V = "ERR: no URL provided" });
            return cells;
        }

        if (!req.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !req.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            req.Url = "https://" + req.Url;
        }

        try
        {
            var result = await ExecuteRequest(req);
            int row = 0;

            // Status line with icon
            var icon = result.StatusCode >= 200 && result.StatusCode < 300 ? "+" :
                       result.StatusCode >= 300 && result.StatusCode < 400 ? "->" :
                       result.StatusCode >= 400 && result.StatusCode < 500 ? "x" : "!";
            cells.Add(new CellUpdate { R = row++, C = 0,
                V = icon + " " + result.StatusCode + " " + result.StatusText + " | " + result.LatencyMs + "ms | " + result.ContentType });

            if (req.StatusOnly) return cells;

            // Response headers
            if (req.ShowHeaders)
            {
                foreach (var h in result.Headers.Take(10))
                {
                    var val = h.Value;
                    if (h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
                        h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                        val = "****";
                    cells.Add(new CellUpdate { R = row++, C = 0, V = h.Key + ": " + val });
                }
                cells.Add(new CellUpdate { R = row++, C = 0, V = "---" });
            }

            // Response body
            if (!string.IsNullOrEmpty(result.Body) && req.Method != "HEAD")
            {
                string display = result.Body;

                if (req.PrettyJson || result.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(result.Body);
                        display = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch { }
                }

                var lines = display.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                bool truncated = lines.Length > req.MaxLines;

                foreach (var bodyLine in lines.Take(req.MaxLines))
                {
                    var trimmed = bodyLine.Length > 120 ? bodyLine.Substring(0, 117) + "..." : bodyLine;
                    cells.Add(new CellUpdate { R = row++, C = 0, V = trimmed });
                }

                if (truncated)
                    cells.Add(new CellUpdate { R = row++, C = 0, V = "... (" + (lines.Length - req.MaxLines) + " more lines)" });
            }

            return cells;
        }
        catch (TaskCanceledException)
        {
            cells.Add(new CellUpdate { R = 0, C = 0, V = "ERR timeout after " + req.TimeoutSeconds + "s" });
        }
        catch (HttpRequestException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("No such host")) msg = "DNS: " + req.Url;
            else if (msg.Contains("SSL") || msg.Contains("TLS") || msg.Contains("certificate"))
                msg = "TLS error (try -k flag): " + (msg.Length > 60 ? msg.Substring(0, 57) + "..." : msg);
            else if (msg.Length > 80) msg = msg.Substring(0, 77) + "...";
            cells.Add(new CellUpdate { R = 0, C = 0, V = "ERR " + msg });
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Length > 80 ? ex.Message.Substring(0, 77) + "..." : ex.Message;
            cells.Add(new CellUpdate { R = 0, C = 0, V = "ERR " + msg });
        }

        return cells;
    }

    static ParsedHttpRequest ParseInput(string input)
    {
        var req = new ParsedHttpRequest();
        var tokens = Tokenize(input);
        int i = 0;

        if (i < tokens.Count && ValidMethods.Contains(tokens[i]))
        {
            req.Method = tokens[i].ToUpperInvariant();
            i++;
        }

        if (i < tokens.Count && !tokens[i].StartsWith("-"))
        {
            req.Url = tokens[i];
            i++;
        }

        while (i < tokens.Count)
        {
            var tok = tokens[i];

            if (tok == "-H" && i + 1 < tokens.Count)
            {
                i++;
                var headerVal = tokens[i];
                var colonIdx = headerVal.IndexOf(':');
                if (colonIdx > 0)
                {
                    req.Headers[headerVal.Substring(0, colonIdx).Trim()] =
                        headerVal.Substring(colonIdx + 1).Trim();
                }
                i++;
            }
            else if (tok == "--body" && i + 1 < tokens.Count)
            {
                i++;
                req.Body = tokens[i];
                i++;
            }
            else if (tok == "--lines" && i + 1 < tokens.Count)
            {
                i++;
                if (int.TryParse(tokens[i], out int n)) req.MaxLines = Math.Clamp(n, 1, 50);
                i++;
            }
            else if (tok == "--timeout" && i + 1 < tokens.Count)
            {
                i++;
                var t = tokens[i].TrimEnd('s');
                if (int.TryParse(t, out int s)) req.TimeoutSeconds = Math.Clamp(s, 1, 60);
                i++;
            }
            else if (tok == "--headers") { req.ShowHeaders = true; i++; }
            else if (tok == "--json") { req.PrettyJson = true; i++; }
            else if (tok == "--status-only") { req.StatusOnly = true; i++; }
            else if (tok == "--insecure" || tok == "-k") { req.SkipTlsVerify = true; i++; }
            else
            {
                if (tok.StartsWith("{") || tok.StartsWith("["))
                    req.Body = tok;
                else if (req.Url == "" && (tok.Contains("://") || tok.Contains(".")))
                    req.Url = tok;
                i++;
            }
        }

        return req;
    }

    static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        char quoteChar = '"';
        int braceDepth = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (braceDepth > 0)
            {
                sb.Append(c);
                if (c == '{') braceDepth++;
                else if (c == '}') { braceDepth--; if (braceDepth == 0) { tokens.Add(sb.ToString()); sb.Clear(); } }
                continue;
            }

            if (inQuotes)
            {
                if (c == quoteChar) { inQuotes = false; tokens.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
                continue;
            }

            if (c == '"' || c == '\'') { inQuotes = true; quoteChar = c; continue; }
            if (c == '{') { braceDepth = 1; sb.Append(c); continue; }
            if (c == ' ' || c == '\t')
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                continue;
            }
            sb.Append(c);
        }

        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    static async Task<HttpResult> ExecuteRequest(ParsedHttpRequest req)
    {
        var handler = new HttpClientHandler();
        if (req.SkipTlsVerify)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        handler.AutomaticDecompression = DecompressionMethods.All;

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(req.TimeoutSeconds) };
        client.DefaultRequestHeaders.Add("User-Agent", "QuickSheet-HTTP/1.0");

        foreach (var h in req.Headers)
        {
            try { client.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value); }
            catch { }
        }

        var method = new System.Net.Http.HttpMethod(req.Method);
        var request = new HttpRequestMessage(method, req.Url);

        if (req.Body != null && req.Method != "GET" && req.Method != "HEAD")
        {
            var mediaType = "application/json";
            if (req.Headers.TryGetValue("Content-Type", out var ct)) mediaType = ct;
            request.Content = new StringContent(req.Body, Encoding.UTF8, mediaType);
        }

        var sw = Stopwatch.StartNew();
        var response = await client.SendAsync(request);
        sw.Stop();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";

        var bodyBytes = await response.Content.ReadAsByteArrayAsync();
        var body = bodyBytes.Length > 102400
            ? Encoding.UTF8.GetString(bodyBytes, 0, 102400) + "...(truncated)"
            : Encoding.UTF8.GetString(bodyBytes);

        var headers = new List<KeyValuePair<string, string>>();
        foreach (var h in response.Headers)
            headers.Add(new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value)));
        foreach (var h in response.Content.Headers)
            headers.Add(new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value)));

        return new HttpResult
        {
            StatusCode = (int)response.StatusCode,
            StatusText = response.StatusCode.ToString(),
            LatencyMs = sw.ElapsedMilliseconds,
            ContentType = contentType,
            Body = body,
            Headers = headers
        };
    }
}

class HttpResult
{
    public int StatusCode { get; set; }
    public string StatusText { get; set; } = "";
    public long LatencyMs { get; set; }
    public string ContentType { get; set; } = "";
    public string Body { get; set; } = "";
    public List<KeyValuePair<string, string>> Headers { get; set; } = new();
}

