using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EmailSweepAutomation;

/// <summary>
/// Parses the <c>--since</c> option into a <see cref="TimeSpan"/> of at least one day.
/// Accepts a day count (<c>30d</c>) or a .NET TimeSpan string (<c>90.00:00:00</c>).
/// Sub-day windows are rejected because the IMAP sent-date search is per-calendar-day.
/// </summary>
public static class DurationParser
{
    private static readonly Regex Shorthand =
        new(@"^(\d+)d$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string FormatHelp =
        "Examples: 30d, 90d, or a .NET TimeSpan like 90.00:00:00. Minimum 1 day.";

    private static readonly TimeSpan Minimum = TimeSpan.FromDays(1);

    public static TimeSpan Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException($"Duration is required. {FormatHelp}");

        var trimmed = input.Trim();
        TimeSpan result;

        var match = Shorthand.Match(trimmed);
        if (match.Success)
        {
            try
            {
                var days = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                result = TimeSpan.FromDays(days);
            }
            catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
            {
                throw new FormatException($"Duration is too large: '{input}'. {FormatHelp}");
            }
        }
        else if (!TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out result))
        {
            throw new FormatException($"Invalid duration '{input}'. {FormatHelp}");
        }

        if (result < Minimum)
            throw new FormatException($"Duration must be at least 1 day: '{input}'. {FormatHelp}");

        return result;
    }
}
