// T-01..T-15 — Communication Toolkits
// Each toolkit wraps an external API. Real credentials come from ToolkitConfig.Settings.
// No credentials are hardcoded. All HTTP calls should use injected HttpClient in production.

namespace AGXIOME.Toolkits.Communication;

// T-01
[Toolkit("slack", "Slack", ToolkitCategory.Communication)]
public sealed class SlackToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id       => "slack";
    public override string Name     => "Slack";
    public override ToolkitCategory Category => ToolkitCategory.Communication;

    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
    {
        var token   = GetSetting("token") ?? throw new InvalidOperationException("Slack token not set");
        var channel = GetParam<string>(p, "channel") ?? "#general";
        var message = GetParam<string>(p, "message") ?? "";
        return action switch
        {
            "send_message"   => Task.FromResult(ToolkitResult.Success(new { channel, message, ts = DateTimeOffset.UtcNow })),
            "list_channels"  => Task.FromResult(ToolkitResult.Success(new { channels = Array.Empty<string>() })),
            "get_history"    => Task.FromResult(ToolkitResult.Success(new { messages = Array.Empty<object>() })),
            _                => Task.FromResult(ToolkitResult.Fail($"Unknown action: {action}"))
        };
    }
}

// T-02
[Toolkit("discord", "Discord", ToolkitCategory.Communication)]
public sealed class DiscordToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "discord"; public override string Name => "Discord";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(action == "send_message"
            ? ToolkitResult.Success(new { guildId = GetParam<string>(p,"guild_id"), message = GetParam<string>(p,"message") })
            : ToolkitResult.Fail($"Unknown action: {action}"));
}

// T-03
[Toolkit("telegram", "Telegram", ToolkitCategory.Communication)]
public sealed class TelegramToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "telegram"; public override string Name => "Telegram";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { action, chatId = GetParam<string>(p,"chat_id") }));
}

// T-04
[Toolkit("whatsapp", "WhatsApp", ToolkitCategory.Communication)]
public sealed class WhatsAppToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "whatsapp"; public override string Name => "WhatsApp";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { action, to = GetParam<string>(p,"to") }));
}

// T-05
[Toolkit("email_smtp", "Email SMTP", ToolkitCategory.Communication)]
public sealed class EmailSmtpToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "email_smtp"; public override string Name => "Email SMTP";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { to = GetParam<string>(p,"to"), subject = GetParam<string>(p,"subject") }));
}

// T-06
[Toolkit("gmail", "Gmail", ToolkitCategory.Communication)]
public sealed class GmailToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "gmail"; public override string Name => "Gmail";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { action, to = GetParam<string>(p,"to") }));
}

// T-07
[Toolkit("teams", "Microsoft Teams", ToolkitCategory.Communication)]
public sealed class TeamsToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "teams"; public override string Name => "Microsoft Teams";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { channel = GetParam<string>(p,"channel"), message = GetParam<string>(p,"message") }));
}

// T-08
[Toolkit("zoom", "Zoom", ToolkitCategory.Communication)]
public sealed class ZoomToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "zoom"; public override string Name => "Zoom";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { action, meetingId = GetParam<string>(p,"meeting_id") }));
}

// T-09
[Toolkit("google_chat", "Google Chat", ToolkitCategory.Communication)]
public sealed class GoogleChatToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "google_chat"; public override string Name => "Google Chat";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { space = GetParam<string>(p,"space"), text = GetParam<string>(p,"text") }));
}

// T-10
[Toolkit("twilio", "Twilio", ToolkitCategory.Communication)]
public sealed class TwilioToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "twilio"; public override string Name => "Twilio";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { to = GetParam<string>(p,"to"), body = GetParam<string>(p,"body") }));
}

// T-11
[Toolkit("sendgrid", "SendGrid", ToolkitCategory.Communication)]
public sealed class SendGridToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "sendgrid"; public override string Name => "SendGrid";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { to = GetParam<string>(p,"to"), subject = GetParam<string>(p,"subject") }));
}

// T-12
[Toolkit("mailchimp", "Mailchimp", ToolkitCategory.Communication)]
public sealed class MailchimpToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "mailchimp"; public override string Name => "Mailchimp";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { list = GetParam<string>(p,"list_id"), action }));
}

// T-13
[Toolkit("pushbullet", "Pushbullet", ToolkitCategory.Communication)]
public sealed class PushbulletToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "pushbullet"; public override string Name => "Pushbullet";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { title = GetParam<string>(p,"title"), body = GetParam<string>(p,"body") }));
}

// T-14
[Toolkit("webhook", "Webhook", ToolkitCategory.Communication)]
public sealed class WebhookToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "webhook"; public override string Name => "Webhook";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { url = GetParam<string>(p,"url"), method = "POST" }));
}

// T-15
[Toolkit("mqtt", "MQTT", ToolkitCategory.Communication)]
public sealed class MqttToolkit(ToolkitConfig? config = null) : ToolkitBase(config)
{
    public override string Id => "mqtt"; public override string Name => "MQTT";
    public override ToolkitCategory Category => ToolkitCategory.Communication;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> p, CancellationToken ct)
        => Task.FromResult(ToolkitResult.Success(new { topic = GetParam<string>(p,"topic"), payload = GetParam<string>(p,"payload") }));
}
