namespace EmailSweepAutomation.Commands;

/// <summary>
/// The rules-file reference that ends <c>sweep --help</c> and <c>print-rules --help</c> (see
/// <c>RulesFileHelpProvider</c>). Plain text, not Spectre markup: the JSON's brackets are literal.
/// A unit test checks it names every property of the rule model, so a new field cannot go
/// undocumented.
/// </summary>
public static class RulesFileHelp
{
    /// <summary>A valid rules file, checked by a unit test.</summary>
    public const string Example = """
        [
          { "Id": 1, "FromAddress": "news@shop.com", "Delete": true, "KeepRecent": 3 },
          { "Id": 2, "FromDomain": "bank.com", "Branches": [
            { "Label": "Statements", "SubjectContainsAny": ["statement"],
              "Action": "MoveToFolder", "MoveToFolder": "Finance/Bank",
              "KeepRecentTime": "90.00:00:00" },
            { "Label": "Offers", "FromAny": ["offers@bank.com", "promo*@bank.com"],
              "Action": "Delete", "KeepRecent": 0 },
            { "Label": "Everything else", "Action": "Leave" } ] }
        ]
        """;

    /// <summary>The reference, under a <c>RULES FILE:</c> heading the help provider adds.</summary>
    public static readonly string Text = $"""
        A JSON array of rules. A rule's filter picks mail; its action, or its branches,
        decide what happens to it.

        Rule filter: at least one; all that are set must match.
          FromAddress         sender address contains this (case-insensitive)
          FromDomain          sender host is this domain or a subdomain of it
                              (not together with FromAddress)
          SubjectContains     subject contains this (case-insensitive)
          Id                  the rule's number in output

        Flat rule, exactly one action:
          Delete: true        delete matches
          MoveToFolder        move matches to this folder (nested with /)
          Alert: true         only list matches; takes no keep
        Delete and MoveToFolder need KeepRecent and/or KeepRecentTime (0 = act on all):
          KeepRecent          keep the N newest per sender
          KeepRecentTime      keep mail newer than this TimeSpan, e.g. 30.00:00:00

        Branches: ordered, at most {SweepRuleExtensions.MaxBranches}. Each match goes to the first branch it
        fits; mail fitting none is left alone. A branched rule has no action or keep.
          Label               required
          FromAny             sender is any of these entries (below)
          SubjectContainsAny  subject contains any of these (case-insensitive)
                              Both set: both must match. Neither: a catch-all,
                              allowed only as the last branch.
          Action              Delete | MoveToFolder | Leave (missing = Leave)
          MoveToFolder        folder for a MoveToFolder branch (nested with /)
          KeepRecent, KeepRecentTime
                              required for Delete/MoveToFolder; not for Leave
        Branches cannot Alert. After other branches, a catch-all cannot Delete.

        Sender entries:
          user@host           exactly that address
          @host               exactly that host
          @*.host             the host and its subdomains
          name*@host          on that host, name starts with (*name@host: ends
                              with, *name*@host: contains)

        Preview a sweep with: sweep <email> <file> --dry-run
        Check a file with:    print-rules <file>

        Example:
        {Example}
        """;
}
