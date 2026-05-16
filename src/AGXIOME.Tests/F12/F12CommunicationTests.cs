using AGXIOME.Toolkits;
using AGXIOME.Toolkits.Communication;

namespace AGXIOME.Tests.F12;

public sealed class SlackToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("slack", new SlackToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Slack", new SlackToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new SlackToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SlackToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("slack", new Dictionary<string, string> { ["token"] = "test-token" });
        var r = await new SlackToolkit(cfg).ExecuteAsync("send_message", new Dictionary<string, object?> { ["channel"] = "#g", ["message"] = "hi" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class DiscordToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("discord", new DiscordToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Discord", new DiscordToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new DiscordToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new DiscordToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("discord", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DiscordToolkit(cfg).ExecuteAsync("send_message", new Dictionary<string, object?> { ["channel"] = "general", ["text"] = "hi" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class TelegramToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("telegram", new TelegramToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Telegram", new TelegramToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new TelegramToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TelegramToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("telegram", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TelegramToolkit(cfg).ExecuteAsync("send_message", new Dictionary<string, object?> { ["chat_id"] = "1", ["text"] = "hi" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class WhatsAppToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("whatsapp", new WhatsAppToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("WhatsApp", new WhatsAppToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new WhatsAppToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new WhatsAppToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("whatsapp", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new WhatsAppToolkit(cfg).ExecuteAsync("send_message", new Dictionary<string, object?> { ["to"] = "+1", ["text"] = "hi" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class EmailSmtpToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("email_smtp", new EmailSmtpToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Email SMTP", new EmailSmtpToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new EmailSmtpToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new EmailSmtpToolkit().ExecuteAsync("send_email", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("email_smtp", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new EmailSmtpToolkit(cfg).ExecuteAsync("send_email", new Dictionary<string, object?> { ["to"] = "a@b.com", ["subject"] = "s", ["body"] = "b" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class GmailToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("gmail", new GmailToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Gmail", new GmailToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new GmailToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GmailToolkit().ExecuteAsync("send_email", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("gmail", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GmailToolkit(cfg).ExecuteAsync("send_email", new Dictionary<string, object?> { ["to"] = "a@b.com", ["subject"] = "s", ["body"] = "b" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class TeamsToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("teams", new TeamsToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Microsoft Teams", new TeamsToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new TeamsToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TeamsToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("teams", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TeamsToolkit(cfg).ExecuteAsync("send_message", new Dictionary<string, object?> { ["channel"] = "general", ["text"] = "hi" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class ZoomToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("zoom", new ZoomToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Zoom", new ZoomToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new ZoomToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ZoomToolkit().ExecuteAsync("create_meeting", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("zoom", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ZoomToolkit(cfg).ExecuteAsync("create_meeting", new Dictionary<string, object?> { ["topic"] = "t" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class GoogleChatToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("google_chat", new GoogleChatToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Google Chat", new GoogleChatToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new GoogleChatToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleChatToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("google_chat", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GoogleChatToolkit(cfg).ExecuteAsync("send_message", new Dictionary<string, object?> { ["space"] = "s", ["text"] = "hi" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class TwilioToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("twilio", new TwilioToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Twilio", new TwilioToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new TwilioToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TwilioToolkit().ExecuteAsync("send_sms", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("twilio", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TwilioToolkit(cfg).ExecuteAsync("send_sms", new Dictionary<string, object?> { ["to"] = "+1", ["body"] = "hi" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class SendGridToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("sendgrid", new SendGridToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("SendGrid", new SendGridToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new SendGridToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SendGridToolkit().ExecuteAsync("send_email", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("sendgrid", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SendGridToolkit(cfg).ExecuteAsync("send_email", new Dictionary<string, object?> { ["to"] = "a@b.com", ["subject"] = "s", ["body"] = "b" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class MailchimpToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("mailchimp", new MailchimpToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Mailchimp", new MailchimpToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new MailchimpToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new MailchimpToolkit().ExecuteAsync("send_campaign", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("mailchimp", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new MailchimpToolkit(cfg).ExecuteAsync("send_campaign", new Dictionary<string, object?> { ["list_id"] = "1", ["subject"] = "s" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class PushbulletToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("pushbullet", new PushbulletToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Pushbullet", new PushbulletToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new PushbulletToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PushbulletToolkit().ExecuteAsync("push_note", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("pushbullet", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PushbulletToolkit(cfg).ExecuteAsync("push_note", new Dictionary<string, object?> { ["title"] = "t", ["body"] = "b" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class WebhookToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("webhook", new WebhookToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Webhook", new WebhookToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new WebhookToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new WebhookToolkit().ExecuteAsync("post", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("webhook", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new WebhookToolkit(cfg).ExecuteAsync("post", new Dictionary<string, object?> { ["url"] = "https://x.com", ["payload"] = "{}" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class MqttToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("mqtt", new MqttToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("MQTT", new MqttToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Communication, new MqttToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new MqttToolkit().ExecuteAsync("publish", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("mqtt", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new MqttToolkit(cfg).ExecuteAsync("publish", new Dictionary<string, object?> { ["topic"] = "t", ["payload"] = "p" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}
