using MailKit;

namespace EmailSweepAutomation;

public record SenderSummary(string Address, int Count, DateTimeOffset LastReceived, DateTimeOffset FirstReceived, List<string>? Subjects, List<IMessageSummary>? Messages)
{
    public List<string> Subjects { get; init; } = Subjects ?? new List<string>();
    public List<IMessageSummary> Messages { get; init; } = Messages ?? new List<IMessageSummary>();
}