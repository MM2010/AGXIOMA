// T-01..T-15 — Communication Toolkits
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace AGXIOME.Toolkits.Communication;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http
{
    internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
}

// T-01 — Slack (token required)
[Toolkit("slack", "Slack", ToolkitCategory.Communication)]
public sealed class SlackToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id       => "slack";
    public override string Name     => "Slack";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("token"));

    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
    {
        var token = GetSetting("token") ?? throw new InvalidOperationException("Slack token not set");
        return action switch {
            "send_message"  => await SlackPost("chat.postMessage", new { channel = GetParam<string>(p,"channel") ?? "#general", text = GetParam<string>(p,"text") ?? GetParam<string>(p,"message") ?? "" }, token, ct).ConfigureAwait(false),
            "list_channels" => await SlackGet("conversations.list?limit=200", token, ct).ConfigureAwait(false),
            "get_history"   => await SlackGet($"conversations.history?channel={GetParam<string>(p,"channel")}&limit={GetParam<int?>(p,"limit")??50}", token, ct).ConfigureAwait(false),
            "upload_file"   => await SlackPost("files.getUploadURLExternal", new { filename = GetParam<string>(p,"filename"), length = GetParam<int?>(p,"length")??0 }, token, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: send_message|list_channels|get_history|upload_file")
        };
    }
    private static async Task<ToolkitResult> SlackPost(string method, object body, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://slack.com/api/{method}") { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var ok = json.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
        return ok ? ToolkitResult.Success(json) : ToolkitResult.Fail(json.TryGetProperty("error", out var e) ? e.GetString()! : "Slack error");
    }
    private static async Task<ToolkitResult> SlackGet(string path, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Get, $"https://slack.com/api/{path}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        return ToolkitResult.Success(json);
    }
}

// T-02 — Discord (bot_token required)
[Toolkit("discord", "Discord", ToolkitCategory.Communication)]
public sealed class DiscordToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "discord"; public override string Name => "Discord";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("bot_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var token = GetSetting("bot_token") ?? throw new InvalidOperationException("Discord bot_token not set");
        var channelId = GetParam<string>(p, "channel_id") ?? "";
        return action switch {
            "send_message" => await DiscordPost($"channels/{channelId}/messages", new { content = GetParam<string>(p,"message") ?? GetParam<string>(p,"content") ?? "" }, token, ct).ConfigureAwait(false),
            "get_messages" => await DiscordGet($"channels/{channelId}/messages?limit={GetParam<int?>(p,"limit")??50}", token, ct).ConfigureAwait(false),
            "get_guilds"   => await DiscordGet("users/@me/guilds", token, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: send_message|get_messages|get_guilds")
        };
    }
    private static async Task<ToolkitResult> DiscordPost(string path, object body, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://discord.com/api/v10/{path}") { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> DiscordGet(string path, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10/{path}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-03 — Telegram Bot API (bot_token required)
[Toolkit("telegram", "Telegram", ToolkitCategory.Communication)]
public sealed class TelegramToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "telegram"; public override string Name => "Telegram";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("bot_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var token = GetSetting("bot_token") ?? throw new InvalidOperationException("Telegram bot_token not set");
        string Base(string method) => $"https://api.telegram.org/bot{token}/{method}";
        return action switch {
            "send_message"    => await TgPost(Base("sendMessage"), new { chat_id=GetParam<string>(p,"chat_id"), text=GetParam<string>(p,"text")??GetParam<string>(p,"message")??"", parse_mode=GetParam<string>(p,"parse_mode")??"HTML" }, ct).ConfigureAwait(false),
            "send_photo"      => await TgPost(Base("sendPhoto"), new { chat_id=GetParam<string>(p,"chat_id"), photo=GetParam<string>(p,"photo"), caption=GetParam<string>(p,"caption") }, ct).ConfigureAwait(false),
            "send_document"   => await TgPost(Base("sendDocument"), new { chat_id=GetParam<string>(p,"chat_id"), document=GetParam<string>(p,"document"), caption=GetParam<string>(p,"caption") }, ct).ConfigureAwait(false),
            "get_updates"     => await TgGet(Base($"getUpdates?offset={GetParam<int?>(p,"offset")??0}&limit={GetParam<int?>(p,"limit")??100}"), ct).ConfigureAwait(false),
            "get_me"          => await TgGet(Base("getMe"), ct).ConfigureAwait(false),
            "set_webhook"     => await TgPost(Base("setWebhook"), new { url=GetParam<string>(p,"url") }, ct).ConfigureAwait(false),
            "delete_webhook"  => await TgPost(Base("deleteWebhook"), new { }, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: send_message|send_photo|send_document|get_updates|get_me|set_webhook|delete_webhook")
        };
    }
    private static async Task<ToolkitResult> TgPost(string url, object body, CancellationToken ct) {
        var resp = await Http.Client.PostAsJsonAsync(url, body, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var ok = json.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
        return ok ? ToolkitResult.Success(json.TryGetProperty("result", out var r) ? (object)r : json) : ToolkitResult.Fail(json.TryGetProperty("description", out var d) ? d.GetString()! : "Telegram error");
    }
    private static async Task<ToolkitResult> TgGet(string url, CancellationToken ct) {
        var json = await Http.Client.GetFromJsonAsync<JsonElement>(url, ct).ConfigureAwait(false);
        return ToolkitResult.Success(json.TryGetProperty("result", out var r) ? (object)r : json);
    }
}

// T-04 — WhatsApp Business (requires Meta Cloud API — access_token + phone_number_id)
[Toolkit("whatsapp", "WhatsApp", ToolkitCategory.Communication)]
public sealed class WhatsAppToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "whatsapp"; public override string Name => "WhatsApp";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("access_token")) && !string.IsNullOrWhiteSpace(GetSetting("phone_number_id"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var token = GetSetting("access_token") ?? throw new InvalidOperationException("WhatsApp access_token not set");
        var phoneId = GetSetting("phone_number_id") ?? throw new InvalidOperationException("WhatsApp phone_number_id not set");
        var to = GetParam<string>(p, "to") ?? "";
        return action switch {
            "send_text" => await WaPost(phoneId, new { messaging_product="whatsapp", to, type="text", text=new{body=GetParam<string>(p,"text")??GetParam<string>(p,"message")??""}}, token, ct).ConfigureAwait(false),
            "send_template" => await WaPost(phoneId, new { messaging_product="whatsapp", to, type="template", template=new{name=GetParam<string>(p,"template_name"),language=new{code=GetParam<string>(p,"lang")??"en_US"}}}, token, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: send_text|send_template")
        };
    }
    private static async Task<ToolkitResult> WaPost(string phoneId, object body, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/v18.0/{phoneId}/messages") { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-05 — Email SMTP (System.Net.Mail, BCL — no extra packages needed)
[Toolkit("email_smtp", "Email SMTP", ToolkitCategory.Communication)]
public sealed class EmailSmtpToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "email_smtp"; public override string Name => "Email SMTP";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("host")) && !string.IsNullOrWhiteSpace(GetSetting("username"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        return action switch {
            "send" or "send_email" => await Send(p, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: send|send_email")
        };
    }
    private async Task<ToolkitResult> Send(Dictionary<string,object?> p, CancellationToken ct) {
        var host   = GetSetting("host")     ?? throw new InvalidOperationException("SMTP host not set");
        var port   = int.TryParse(GetSetting("port"), out var pv) ? pv : 587;
        var user   = GetSetting("username") ?? throw new InvalidOperationException("SMTP username not set");
        var pass   = GetSetting("password") ?? "";
        var from   = GetSetting("from")     ?? user;
        var to     = GetParam<string>(p,"to") ?? throw new ArgumentException("to is required");
        var subject = GetParam<string>(p,"subject") ?? "(no subject)";
        var body   = GetParam<string>(p,"body") ?? GetParam<string>(p,"text") ?? "";
        var isHtml = GetParam<bool?>(p,"html") ?? false;
        var cc     = GetParam<string>(p,"cc");
        var bcc    = GetParam<string>(p,"bcc");
        using var client = new SmtpClient(host, port) {
            Credentials  = new NetworkCredential(user, pass),
            EnableSsl    = GetParam<bool?>(p,"enable_ssl") ?? true,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        using var msg = new MailMessage(from, to, subject, body) { IsBodyHtml = isHtml };
        if(!string.IsNullOrWhiteSpace(cc))msg.CC.Add(cc);
        if(!string.IsNullOrWhiteSpace(bcc))msg.Bcc.Add(bcc);
        await client.SendMailAsync(msg, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{from,to,subject,cc,bcc,isHtml,sent=true});
    }
}

// T-06 — Gmail (OAuth2 access_token, send via SMTP or Gmail API)
[Toolkit("gmail", "Gmail", ToolkitCategory.Communication)]
public sealed class GmailToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "gmail"; public override string Name => "Gmail";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var token = GetSetting("access_token") ?? throw new InvalidOperationException("Gmail access_token not set");
        return action switch {
            "send"         => await GmailSend(p, token, ct).ConfigureAwait(false),
            "list"         => await GmailGet($"messages?maxResults={GetParam<int?>(p,"max")??10}&q={GetParam<string>(p,"query")??""}", token, ct).ConfigureAwait(false),
            "get"          => await GmailGet($"messages/{GetParam<string>(p,"id")}", token, ct).ConfigureAwait(false),
            "labels"       => await GmailGet("labels", token, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: send|list|get|labels")
        };
    }
    private static async Task<ToolkitResult> GmailSend(Dictionary<string,object?> p, string token, CancellationToken ct) {
        var to = GetParam(p,"to"); var subject = GetParam(p,"subject")??"(no subject)"; var body = GetParam(p,"body")??"";
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"To: {to}\r\nSubject: {subject}\r\nContent-Type: text/plain\r\n\r\n{body}")).Replace('+','-').Replace('/','_').TrimEnd('=');
        var req = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send") { Content = JsonContent.Create(new{raw}) };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static string? GetParam(Dictionary<string,object?> p, string k) => p.TryGetValue(k,out var v)?v?.ToString():null;
    private static async Task<ToolkitResult> GmailGet(string path, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Get, $"https://gmail.googleapis.com/gmail/v1/users/me/{path}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-07 — Microsoft Teams (incoming webhook_url or bot access_token)
[Toolkit("teams", "Microsoft Teams", ToolkitCategory.Communication)]
public sealed class TeamsToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "teams"; public override string Name => "Microsoft Teams";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("webhook_url")) || !string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var webhookUrl = GetSetting("webhook_url");
        if(!string.IsNullOrWhiteSpace(webhookUrl) && action is "send_message" or "send") {
            var body = new { type="message", attachments=new[]{new{contentType="application/vnd.microsoft.card.adaptive",contentUrl=(string?)null,content=new{type="AdaptiveCard",body=new[]{new{type="TextBlock",text=GetParam<string>(p,"message")??GetParam<string>(p,"text")??""}}}}}};
            var resp = await Http.Client.PostAsJsonAsync(webhookUrl, body, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return ToolkitResult.Success(new{sent=true,channel=GetParam<string>(p,"channel")});
        }
        return ToolkitResult.Fail("Teams requires webhook_url in settings. For Graph API, set access_token.");
    }
}

// T-08 — Zoom (account_id + client_id + client_secret for server-to-server OAuth)
[Toolkit("zoom", "Zoom", ToolkitCategory.Communication)]
public sealed class ZoomToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "zoom"; public override string Name => "Zoom";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("account_id")) && !string.IsNullOrWhiteSpace(GetSetting("client_id"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var accessToken = await GetZoomToken(ct).ConfigureAwait(false);
        return action switch {
            "list_meetings"   => await ZoomGet("users/me/meetings", accessToken, ct).ConfigureAwait(false),
            "create_meeting"  => await ZoomPost("users/me/meetings", new { topic=GetParam<string>(p,"topic"), type=GetParam<int?>(p,"type")??2, start_time=GetParam<string>(p,"start_time"), duration=GetParam<int?>(p,"duration")??60 }, accessToken, ct).ConfigureAwait(false),
            "get_meeting"     => await ZoomGet($"meetings/{GetParam<string>(p,"meeting_id")}", accessToken, ct).ConfigureAwait(false),
            "delete_meeting"  => await ZoomDelete($"meetings/{GetParam<string>(p,"meeting_id")}", accessToken, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: list_meetings|create_meeting|get_meeting|delete_meeting")
        };
    }
    private async Task<string> GetZoomToken(CancellationToken ct) {
        var accountId=GetSetting("account_id")!; var cId=GetSetting("client_id")!; var cSecret=GetSetting("client_secret")??"";
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cId}:{cSecret}"));
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://zoom.us/oauth/token?grant_type=account_credentials&account_id={accountId}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        return json.GetProperty("access_token").GetString()!;
    }
    private static async Task<ToolkitResult> ZoomGet(string path, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.zoom.us/v2/{path}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> ZoomPost(string path, object body, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.zoom.us/v2/{path}") { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> ZoomDelete(string path, string token, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Delete, $"https://api.zoom.us/v2/{path}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        return resp.IsSuccessStatusCode ? ToolkitResult.Success(new{deleted=true}) : ToolkitResult.Fail($"Zoom API error: {resp.StatusCode}");
    }
}

// T-09 — Google Chat (webhook_url)
[Toolkit("google_chat", "Google Chat", ToolkitCategory.Communication)]
public sealed class GoogleChatToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "google_chat"; public override string Name => "Google Chat";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("webhook_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var url = GetSetting("webhook_url") ?? throw new InvalidOperationException("Google Chat webhook_url not set");
        var text = GetParam<string>(p,"text") ?? GetParam<string>(p,"message") ?? "";
        var resp = await Http.Client.PostAsJsonAsync(url, new { text }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(new{sent=true,text});
    }
}

// T-10 — Twilio SMS/Voice (account_sid + auth_token required)
[Toolkit("twilio", "Twilio", ToolkitCategory.Communication)]
public sealed class TwilioToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "twilio"; public override string Name => "Twilio";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("account_sid")) && !string.IsNullOrWhiteSpace(GetSetting("auth_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var sid   = GetSetting("account_sid") ?? throw new InvalidOperationException("Twilio account_sid not set");
        var token = GetSetting("auth_token")  ?? throw new InvalidOperationException("Twilio auth_token not set");
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sid}:{token}"));
        return action switch {
            "send_sms"  => await TwilioPost($"Accounts/{sid}/Messages.json", new Dictionary<string,string>{{"To",GetParam<string>(p,"to")!},{"From",GetSetting("from_number")??GetParam<string>(p,"from")!},{"Body",GetParam<string>(p,"body")??GetParam<string>(p,"message")!}}, creds, ct).ConfigureAwait(false),
            "list_sms"  => await TwilioGet($"Accounts/{sid}/Messages.json?PageSize={GetParam<int?>(p,"limit")??20}", creds, ct).ConfigureAwait(false),
            "get_sms"   => await TwilioGet($"Accounts/{sid}/Messages/{GetParam<string>(p,"sid")}.json", creds, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: send_sms|list_sms|get_sms")
        };
    }
    private static async Task<ToolkitResult> TwilioPost(string path, Dictionary<string,string> form, string creds, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/{path}") { Content = new FormUrlEncodedContent(form) };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> TwilioGet(string path, string creds, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.twilio.com/2010-04-01/{path}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-11 — SendGrid (api_key required)
[Toolkit("sendgrid", "SendGrid", ToolkitCategory.Communication)]
public sealed class SendGridToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "sendgrid"; public override string Name => "SendGrid";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var key = GetSetting("api_key") ?? throw new InvalidOperationException("SendGrid api_key not set");
        return action switch {
            "send" or "send_email" => await SgSend(p, key, ct).ConfigureAwait(false),
            "list_templates"       => await SgGet("templates?generations=dynamic", key, ct).ConfigureAwait(false),
            "stats"                => await SgGet($"stats?start_date={GetParam<string>(p,"start_date")??"2024-01-01"}", key, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: send|send_email|list_templates|stats")
        };
    }
    private static async Task<ToolkitResult> SgSend(Dictionary<string,object?> p, string key, CancellationToken ct) {
        var from = p.TryGetValue("from", out var f) ? f?.ToString() : null;
        var to   = p.TryGetValue("to",   out var t) ? t?.ToString() : null;
        var subj = p.TryGetValue("subject",out var s)?s?.ToString():"(no subject)";
        var body = p.TryGetValue("body",  out var b)?b?.ToString()??"":( p.TryGetValue("text",out var bt)?bt?.ToString()??"":"");
        var html = p.TryGetValue("html",  out var h)?h?.ToString():(string?)null;
        var payload = new {
            personalizations = new[]{new{to=new[]{new{email=to}}}},
            from = new{email=from},
            subject=subj,
            content = html!=null
                ? new[]{new{type="text/plain",value=body},new{type="text/html",value=html}}
                : new[]{new{type="text/plain",value=body}}
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send") { Content = JsonContent.Create(payload) };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        return resp.StatusCode == System.Net.HttpStatusCode.Accepted
            ? ToolkitResult.Success(new{sent=true,to,from,subject=subj})
            : ToolkitResult.Fail($"SendGrid error: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync(ct)}");
    }
    private static async Task<ToolkitResult> SgGet(string path, string key, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.sendgrid.com/v3/{path}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-12 — Mailchimp (api_key required — key has region e.g. abc123-us1)
[Toolkit("mailchimp", "Mailchimp", ToolkitCategory.Communication)]
public sealed class MailchimpToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "mailchimp"; public override string Name => "Mailchimp";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var key    = GetSetting("api_key") ?? throw new InvalidOperationException("Mailchimp api_key not set");
        var region = key.Split('-').LastOrDefault() ?? "us1";
        var base_  = $"https://{region}.api.mailchimp.com/3.0";
        return action switch {
            "list_lists"     => await McGet($"{base_}/lists", key, ct).ConfigureAwait(false),
            "get_list"       => await McGet($"{base_}/lists/{GetParam<string>(p,"list_id")}", key, ct).ConfigureAwait(false),
            "add_member"     => await McPost($"{base_}/lists/{GetParam<string>(p,"list_id")}/members", new{email_address=GetParam<string>(p,"email"),status=GetParam<string>(p,"status")??"subscribed"}, key, ct).ConfigureAwait(false),
            "list_campaigns" => await McGet($"{base_}/campaigns?count={GetParam<int?>(p,"count")??10}", key, ct).ConfigureAwait(false),
            "list_tags"      => await McGet($"{base_}/lists/{GetParam<string>(p,"list_id")}/tag-search", key, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: list_lists|get_list|add_member|list_campaigns|list_tags")
        };
    }
    private static async Task<ToolkitResult> McGet(string url, string key, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"anystring:{key}")));
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> McPost(string url, object body, string key, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"anystring:{key}")));
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-13 — Pushbullet (api_key required)
[Toolkit("pushbullet", "Pushbullet", ToolkitCategory.Communication)]
public sealed class PushbulletToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "pushbullet"; public override string Name => "Pushbullet";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var key = GetSetting("api_key") ?? throw new InvalidOperationException("Pushbullet api_key not set");
        var type = GetParam<string>(p,"type") ?? "note";
        return action switch {
            "push"         => await PbPost("pushes", new { type, title=GetParam<string>(p,"title"), body=GetParam<string>(p,"body"), url=GetParam<string>(p,"url") }, key, ct).ConfigureAwait(false),
            "list_devices" => await PbGet("devices", key, ct).ConfigureAwait(false),
            "list_pushes"  => await PbGet($"pushes?limit={GetParam<int?>(p,"limit")??10}", key, ct).ConfigureAwait(false),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: push|list_devices|list_pushes")
        };
    }
    private static async Task<ToolkitResult> PbPost(string path, object body, string key, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.pushbullet.com/v2/{path}") { Content = JsonContent.Create(body) };
        req.Headers.Add("Access-Token", key);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> PbGet(string path, string key, CancellationToken ct) {
        var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.pushbullet.com/v2/{path}");
        req.Headers.Add("Access-Token", key);
        var resp = await Http.Client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-14 — Webhook (generic HTTP POST/GET — no credentials needed)
[Toolkit("webhook", "Webhook", ToolkitCategory.Communication)]
public sealed class WebhookToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "webhook"; public override string Name => "Webhook";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct) {
        var url = GetParam<string>(p,"url") ?? GetSetting("url") ?? throw new ArgumentException("url is required");
        var method = (GetParam<string>(p,"method") ?? action switch { "get" => "GET", "delete" => "DELETE", _ => "POST" }).ToUpperInvariant();
        var headers = GetParam<Dictionary<string,string>?>(p,"headers");
        var body = GetParam<object?>(p,"body") ?? GetParam<object?>(p,"payload");
        var timeoutSec = GetParam<int?>(p,"timeout_seconds") ?? 30;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if(headers!=null)foreach(var kv in headers)req.Headers.TryAddWithoutValidation(kv.Key,kv.Value);
        var authHeader = GetSetting("authorization");
        if(!string.IsNullOrWhiteSpace(authHeader))req.Headers.TryAddWithoutValidation("Authorization",authHeader);
        if(body!=null && method is "POST" or "PUT" or "PATCH")req.Content=JsonContent.Create(body);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var resp = await Http.Client.SendAsync(req, cts.Token).ConfigureAwait(false);
        sw.Stop();
        var respBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        object? parsedBody = null;
        try { parsedBody = JsonDocument.Parse(respBody).RootElement; } catch { parsedBody = respBody; }
        return ToolkitResult.Success(new{url,method,statusCode=(int)resp.StatusCode,latencyMs=sw.ElapsedMilliseconds,body=parsedBody});
    }
}

// T-15 — MQTT (requires MQTTnet NuGet package)
[Toolkit("mqtt", "MQTT", ToolkitCategory.Communication)]
public sealed class MqttToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "mqtt"; public override string Name => "MQTT";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(GetSetting("broker"));
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Fail("MQTT requires package MQTTnet (v4+). Add it and use MqttClientFactory to publish/subscribe. Settings needed: broker, port (default 1883), username, password."));
}
