using MailKit;
using Spectre.Console;

namespace EmailSweepAutomation.Commands;

/// <summary>
/// Spectre-escaped cell values for rendering a message in a console table. Centralizes the
/// sender/subject fallbacks so every command renders a message row identically.
/// </summary>
internal static class MessageCells
{
    public static string From(IMessageSummary message) =>
        Markup.Escape(message.Envelope.From.Mailboxes.FirstOrDefault()?.Address ?? "UNKNOWN");

    public static string Subject(IMessageSummary message) =>
        Markup.Escape(message.Envelope.Subject ?? "NO SUBJECT");

    public static string Date(IMessageSummary message) =>
        Markup.Escape(message.Date.ToString("g"));
}
