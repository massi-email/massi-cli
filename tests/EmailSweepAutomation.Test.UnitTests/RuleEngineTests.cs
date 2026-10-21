namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using EmailSweepAutomation;
using MailKit;
using MimeKit;
using Moq;
using Xunit;

public class RuleEngineTests
{
    private readonly IRuleEngine _ruleEngine;

    public RuleEngineTests()
    {
        _ruleEngine = new RuleEngine();
    }

    #region Selection Tests

    /// <summary>Every message a rule selected, paired with that rule.</summary>
    private List<(IMessageSummary Message, SweepRule Rule)> Select(
        IEnumerable<IMessageSummary> messages, IEnumerable<SweepRule> rules) =>
        _ruleEngine.EvaluateRules(messages, rules)
            .SelectMany(evaluation => evaluation.Selected.Select(message => (message, evaluation.Rule)))
            .ToList();

    [Fact]
    public void Select_WithEmptyMessages_ReturnsEmptyList()
    {
        // Arrange
        var messages = new List<IMessageSummary>();
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "test@example.com", KeepRecent = 1, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Select_WithEmptyRules_ReturnsEmptyList()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "Test Subject") };
        var rules = new List<SweepRule>();

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Select_WithFromAddressMatch_ReturnsMatchedMessage()
    {
        // Arrange
        var fromAddress = "sender@example.com";
        var messages = new List<IMessageSummary> { CreateMockMessageSummary(fromAddress, "Test Subject") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecent = 0, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Equal(fromAddress, result[0].Message.Envelope.From.Mailboxes.First().Address);
    }

    [Fact]
    public void Select_WithSubjectMatch_ReturnsMatchedMessage()
    {
        // Arrange
        var subject = "Important Update";
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", subject) };
        var rules = new List<SweepRule>
        {
            new SweepRule { SubjectContains = "Important", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Contains("Important", result[0].Message.Envelope.Subject);
    }

    [Fact]
    public void Select_WithSubjectMismatch_ExcludesMessage()
    {
        // The negative half of the subject filter. Every other subject test uses a matching
        // message, so a filter that let everything through would pass them all — and a
        // subject-only rule would then sweep the whole mailbox.
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("sender@example.com", "Important Update"),
            CreateMockMessageSummary("sender@example.com", "Lunch on Friday"),
        };
        var rules = new List<SweepRule>
        {
            new SweepRule { SubjectContains = "Important", KeepRecent = 0, Delete = true }
        };

        var result = Select(messages, rules);

        var match = Assert.Single(result);
        Assert.Equal("Important Update", match.Message.Envelope.Subject);
    }

    [Fact]
    public void Select_WithNullSubject_ExcludesMessageFromSubjectRule()
    {
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", null!) };
        var rules = new List<SweepRule>
        {
            new SweepRule { SubjectContains = "Important", KeepRecent = 0, Delete = true }
        };

        Assert.Empty(Select(messages, rules));
    }

    [Fact]
    public void Select_WithFromMatchButSubjectMismatch_ExcludesMessage()
    {
        // Criteria combine with AND: the right sender is not enough on its own.
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "Receipt") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "sender@example.com", SubjectContains = "News", KeepRecent = 0, Delete = true }
        };

        Assert.Empty(Select(messages, rules));
    }

    [Fact]
    public void Select_WithCaseInsensitiveFromAddress_ReturnsMatch()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("Sender@Example.COM", "Test") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "sender@example.com", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void Select_WithCaseInsensitiveSubject_ReturnsMatch()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "NEWSLETTER") };
        var rules = new List<SweepRule>
        {
            new SweepRule { SubjectContains = "newsletter", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void Select_WithBothFromAndSubjectMatch_ReturnsMatchedMessage()
    {
        // Arrange
        var fromAddress = "sender@example.com";
        var subject = "Newsletter";
        var messages = new List<IMessageSummary> { CreateMockMessageSummary(fromAddress, subject) };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, SubjectContains = "News", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void Select_WithPartialFromAddressMatch_ReturnsMatch()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("notifications@github.com", "Test") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "github", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void Select_NoMatchFromAddress_ReturnsEmpty()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "Test") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "nomatch@example.com", KeepRecent = 1, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Select_NoMatchSubject_ReturnsEmpty()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "Hello") };
        var rules = new List<SweepRule>
        {
            new SweepRule { SubjectContains = "Goodbye", KeepRecent = 1, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Select_WithKeepRecentCount_FiltersCorrectly()
    {
        // Arrange
        var fromAddress = "sender@example.com";
        var subject = "Newsletter"; // Same subject to ensure all messages are tracked together
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary(fromAddress, subject),
            CreateMockMessageSummary(fromAddress, subject),
            CreateMockMessageSummary(fromAddress, subject)
        };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecent = 2, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        // Only messages after keeping the 2 most recent should match (1 message)
        Assert.Single(result);
    }

    [Fact]
    public void Select_WithKeepRecentTime_FiltersOldMessages()
    {
        // Arrange
        var fromAddress = "sender@example.com";
        var now = DateTimeOffset.Now;
        var oldMessage = CreateMockMessageSummary(fromAddress, "Old Message", now.AddDays(-10));
        var recentMessage = CreateMockMessageSummary(fromAddress, "Recent Message", now.AddHours(-1));

        var messages = new List<IMessageSummary> { oldMessage, recentMessage };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecentTime = TimeSpan.FromDays(5), Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Equal(oldMessage, result[0].Message);
    }

    [Fact]
    public void Select_WithMultipleRules_ReturnsAllMatches()
    {
        // Arrange
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("sender1@example.com", "Newsletter"),
            CreateMockMessageSummary("sender2@example.com", "Report")
        };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "sender1@example.com", KeepRecent = 0, Delete = true },
            new SweepRule { FromAddress = "sender2@example.com", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Select_WithMoveToFolderAction_ReturnsMatchedMessages()
    {
        // Arrange
        var fromAddress = "sender@example.com";
        var messages = new List<IMessageSummary> { CreateMockMessageSummary(fromAddress, "Test") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecent = 0, MoveToFolder = "Archive" }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Equal("Archive", result[0].Rule.MoveToFolder);
    }

    [Fact]
    public void Select_WithKeepRecent_DifferentSubjectsSameSender_CountsPerSender()
    {
        // Arrange - a sender that uses a unique subject for every email.
        // KeepRecent = 2 should keep the 2 most recent emails from the sender
        // and match the rest, regardless of subject.
        var fromAddress = "sender@example.com";
        var now = DateTimeOffset.Now;
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary(fromAddress, "Order #1", now.AddDays(-3)),
            CreateMockMessageSummary(fromAddress, "Order #2", now.AddDays(-2)),
            CreateMockMessageSummary(fromAddress, "Order #3", now.AddDays(-1))
        };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecent = 2, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert - only the single oldest message should match
        Assert.Single(result);
        Assert.Equal("Order #1", result[0].Message.Envelope.Subject);
    }

    [Fact]
    public void Select_WithKeepRecent_KeepsMostRecentByDateNotInputOrder()
    {
        // Arrange - same sender/subject, supplied out of chronological order.
        var fromAddress = "sender@example.com";
        var subject = "Newsletter";
        var now = DateTimeOffset.Now;
        var oldest = CreateMockMessageSummary(fromAddress, subject, now.AddDays(-10));
        var newest = CreateMockMessageSummary(fromAddress, subject, now.AddDays(-1));
        var middle = CreateMockMessageSummary(fromAddress, subject, now.AddDays(-5));
        var messages = new List<IMessageSummary> { oldest, newest, middle };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecent = 2, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert - the 2 newest (newest + middle) are kept; only the oldest matches
        Assert.Single(result);
        Assert.Equal(oldest, result[0].Message);
    }

    [Fact]
    public void Select_MessageMatchesMultipleRules_ReturnsMultipleMatches()
    {
        // Arrange
        var fromAddress = "sender@example.com";
        var subject = "Newsletter";
        var messages = new List<IMessageSummary> { CreateMockMessageSummary(fromAddress, subject) };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecent = 0, Delete = true },
            new SweepRule { SubjectContains = "News", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = Select(messages, rules);

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region EvaluateRules Tests

    [Fact]
    public void EvaluateRules_KeepRecent_ReportsMatchedKeptAndSelectedCounts()
    {
        // Arrange - a sender with 5 emails (unique subjects), KeepRecent = 3.
        // This is the shape of the real "example-sender" rule: matched should count ALL
        // criteria matches, Selected only the ones left after keeping the 3 newest.
        var fromAddress = "sender6@example.com";
        var now = DateTimeOffset.Now;
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary(fromAddress, "Order #1", now.AddDays(-1)),
            CreateMockMessageSummary(fromAddress, "Order #2", now.AddDays(-2)),
            CreateMockMessageSummary(fromAddress, "Order #3", now.AddDays(-3)),
            CreateMockMessageSummary(fromAddress, "Order #4", now.AddDays(-4)),
            CreateMockMessageSummary(fromAddress, "Order #5", now.AddDays(-5))
        };
        var rules = new List<SweepRule>
        {
            new SweepRule { Id = 6, FromAddress = fromAddress, KeepRecent = 3, Delete = true }
        };

        // Act
        var result = _ruleEngine.EvaluateRules(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Same(rules[0], result[0].Rule);
        Assert.Equal(5, result[0].MatchedCriteria);
        Assert.Equal(2, result[0].Selected.Count); // 5 matched, keep 3 newest, 2 selected for deletion
    }

    [Fact]
    public void EvaluateRules_NoMatches_StillReportsRuleWithZeroCounts()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("someone@example.com", "Hi") };
        var rules = new List<SweepRule>
        {
            new SweepRule { Id = 1, FromAddress = "nobody@example.com", KeepRecent = 3, Delete = true }
        };

        // Act
        var result = _ruleEngine.EvaluateRules(messages, rules);

        // Assert - the rule is still reported so the summary shows it acted on nothing
        Assert.Single(result);
        Assert.Equal(0, result[0].MatchedCriteria);
        Assert.Empty(result[0].Selected);
    }

    #endregion

    #region Alert Rule Tests

    [Fact]
    public void EvaluateRules_AlertRule_SelectsAllCriteriaMatches()
    {
        // Alert rules have no keep filters, so Selected must equal every criteria match.
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("boss@example.com", "Urgent: budget", DateTimeOffset.Now.AddDays(-10)),
            CreateMockMessageSummary("boss@example.com", "Urgent: hiring", DateTimeOffset.Now),
            CreateMockMessageSummary("other@example.com", "Urgent: spam"),
        };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "boss@example.com", Alert = true }
        };

        var evaluation = Assert.Single(_ruleEngine.EvaluateRules(messages, rules));

        Assert.Equal(2, evaluation.MatchedCriteria);
        Assert.Equal(2, evaluation.Selected.Count);
    }

    #endregion

    #region Branched rules

    private static SweepRule ShopRule(params SweepBranch[] branches) =>
        new() { Id = 1, FromDomain = "shop.com", Branches = branches.ToList() };

    [Fact]
    public void FromDomain_MatchesSubdomains_NotLookalikes()
    {
        var now = DateTimeOffset.Now;
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("a@shop.com", "1", now),
            CreateMockMessageSummary("b@mail.shop.com", "2", now),
            CreateMockMessageSummary("c@notshop.com", "3", now),
            CreateMockMessageSummary("d@shop.com.evil.io", "4", now),
        };
        var rule = new SweepRule { Id = 1, FromDomain = "shop.com", Delete = true, KeepRecent = 0 };

        var eval = Assert.Single(_ruleEngine.EvaluateRules(messages, [rule]));

        Assert.Equal(2, eval.MatchedCriteria);
        Assert.Equal(2, Assert.Single(eval.Branches).Matched); // a flat rule is one catch-all branch
    }

    [Fact]
    public void FromDomain_MatchesWhenAnyOneSenderIsOnTheDomain()
    {
        var message = CreateMockMessageSummary("x@other.com", "1", DateTimeOffset.Now);
        message.Envelope.From.Add(new MailboxAddress("y@shop.com", "y@shop.com"));
        var rule = new SweepRule { Id = 1, FromDomain = "shop.com", Delete = true, KeepRecent = 0 };

        var eval = Assert.Single(_ruleEngine.EvaluateRules([message], [rule]));

        Assert.Equal(1, eval.MatchedCriteria);
    }

    [Fact]
    public void FromDomain_KeepRecent_CountsAcrossTheWholeDomain()
    {
        var now = DateTimeOffset.Now;
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("a@mail.shop.com", "1", now.AddDays(-1)),
            CreateMockMessageSummary("b@deals.shop.com", "2", now.AddDays(-2)),
            CreateMockMessageSummary("c@shop.com", "3", now.AddDays(-3)),
        };
        var rule = new SweepRule { Id = 1, FromDomain = "shop.com", Delete = true, KeepRecent = 2 };

        var eval = Assert.Single(_ruleEngine.EvaluateRules(messages, [rule]));

        // Keep the 2 newest from shop.com, not 2 per sub-address (which would keep all three).
        Assert.Equal("3", Assert.Single(eval.Selected).Envelope.Subject);
    }

    [Fact]
    public void Branches_RouteEachMessageToTheFirstMatchingBranch()
    {
        var old = DateTimeOffset.Now.AddDays(-40);
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("transaction@notice.shop.com", "Order shipped", old),
            CreateMockMessageSummary("alerts@account.shop.com", "Statement", old),
            CreateMockMessageSummary("promo@mail.shop.com", "Sale 1", old),
            CreateMockMessageSummary("promo@mail.shop.com", "Sale 2", old.AddDays(-1)),
            CreateMockMessageSummary("new@unknown.shop.com", "Hello", old),
        };
        var rule = ShopRule(
            new SweepBranch { Label = "Account", Action = SweepBranchAction.Leave, FromAny = ["@account.shop.com"] },
            new SweepBranch { Label = "Orders", Action = SweepBranchAction.MoveToFolder, FromAny = ["transaction@notice.shop.com"], MoveToFolder = "Massi/Orders", KeepRecentTime = TimeSpan.FromDays(30) },
            new SweepBranch { Label = "Promotions", Action = SweepBranchAction.Delete, FromAny = ["@mail.shop.com"], KeepRecent = 1 });

        var eval = Assert.Single(_ruleEngine.EvaluateRules(messages, [rule]));

        Assert.Equal(5, eval.MatchedCriteria);
        Assert.Equal([1, 1, 2], eval.Branches.Select(b => b.Matched));
        Assert.Empty(eval.Branches[0].Selected);                       // Leave selects nothing
        Assert.Equal("Order shipped", Assert.Single(eval.Branches[1].Selected).Envelope.Subject);
        Assert.Equal("Sale 2", Assert.Single(eval.Branches[2].Selected).Envelope.Subject); // keeps the newest 1
        Assert.Equal(2, eval.Selected.Count);                           // union of move + delete
        Assert.DoesNotContain(eval.Selected, m => m.Envelope.Subject == "Hello"); // no branch: untouched
    }

    [Fact]
    public void Branches_SubjectAndSenderConditions_AreAnded_AndSubjectsAreAnyOf()
    {
        var old = DateTimeOffset.Now.AddDays(-5);
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("news@email.hotel.com", "Your Account Summary", old),
            CreateMockMessageSummary("news@email.hotel.com", "Save 20% Off", old),
            CreateMockMessageSummary("other@email.hotel.com", "Save 20% Off", old),
        };
        var rule = new SweepRule
        {
            Id = 1, FromDomain = "hotel.com",
            Branches =
            [
                new() { Label = "Account", Action = SweepBranchAction.Leave, FromAny = ["news@email.hotel.com"], SubjectContainsAny = ["account summary"] },
                new() { Label = "Promotions", Action = SweepBranchAction.Delete, FromAny = ["news@email.hotel.com"], SubjectContainsAny = ["save", "off"], KeepRecent = 0 },
            ],
        };

        var eval = Assert.Single(_ruleEngine.EvaluateRules(messages, [rule]));

        Assert.Equal([1, 1], eval.Branches.Select(b => b.Matched));
        Assert.Equal("Save 20% Off", Assert.Single(eval.Selected).Envelope.Subject);
    }

    [Fact]
    public void Branches_ACatchAll_TakesWhatEarlierBranchesDidNot()
    {
        var old = DateTimeOffset.Now.AddDays(-5);
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("a@mail.shop.com", "1", old),
            CreateMockMessageSummary("b@other.shop.com", "2", old),
        };
        var rule = ShopRule(
            new SweepBranch { Label = "Promotions", Action = SweepBranchAction.Delete, FromAny = ["@mail.shop.com"], KeepRecent = 0 },
            new SweepBranch { Label = "Everything else", Action = SweepBranchAction.MoveToFolder, MoveToFolder = "Review", KeepRecent = 0 });

        var eval = Assert.Single(_ruleEngine.EvaluateRules(messages, [rule]));

        Assert.Equal("2", Assert.Single(eval.Branches[1].Selected).Envelope.Subject);
    }

    [Fact]
    public void Plan_ForFlatRules_FollowsEachRulesOwnAction_AndAlertsTouchNothing()
    {
        var m = CreateMockMessageSummary("a@b.com", "x", DateTimeOffset.Now.AddDays(-1));
        SweepRule[] rules =
        [
            new() { Id = 1, FromAddress = "a@b.com", Delete = true, KeepRecent = 0 },
            new() { Id = 2, FromAddress = "a@b.com", MoveToFolder = "F", KeepRecent = 0 },
            new() { Id = 3, FromAddress = "a@b.com", Alert = true },
        ];

        var evaluations = _ruleEngine.EvaluateRules([m], rules);
        var plan = SweepPlan.From(evaluations);

        Assert.Equal([m], plan.Deletes);
        Assert.Equal([(m, "F")], plan.Moves);
        Assert.Equal([m], evaluations[2].Selected); // the alert still selects, to report
    }

    [Fact]
    public void Plan_ForABranchedRule_ComesFromEachBranch_AndLeaveProducesNone()
    {
        var a = CreateMockMessageSummary("a@shop.com", "a", DateTimeOffset.Now);
        var b = CreateMockMessageSummary("b@shop.com", "b", DateTimeOffset.Now);
        var c = CreateMockMessageSummary("c@shop.com", "c", DateTimeOffset.Now);
        var rule = ShopRule(
            new SweepBranch { Label = "L", FromAny = ["a@shop.com"], Action = SweepBranchAction.Leave },
            new SweepBranch { Label = "M", FromAny = ["b@shop.com"], Action = SweepBranchAction.MoveToFolder, MoveToFolder = "Orders", KeepRecent = 0 },
            new SweepBranch { Label = "D", FromAny = ["c@shop.com"], Action = SweepBranchAction.Delete, KeepRecent = 0 });

        var plan = SweepPlan.From(_ruleEngine.EvaluateRules([a, b, c], [rule]));

        Assert.Equal([c], plan.Deletes);
        Assert.Equal([(b, "Orders")], plan.Moves);
    }

    [Fact]
    public void Plan_DeleteFalse_DoesNothing()
    {
        // Validation accepts Delete: false as "an action"; it must never read as a delete.
        var m = CreateMockMessageSummary("a@b.com", "x", DateTimeOffset.Now.AddDays(-1));
        var rule = new SweepRule { FromAddress = "a@b.com", Delete = false, KeepRecent = 0 };

        var plan = SweepPlan.From(_ruleEngine.EvaluateRules([m], [rule]));

        Assert.Equal(0, plan.Count);
    }

    #endregion

    #region Helper Methods

    private static IMessageSummary CreateMockMessageSummary(string fromAddress, string subject, DateTimeOffset? date = null)
    {
        var messageDate = date ?? DateTimeOffset.Now;
        
        // Create a real Envelope - the From property returns a mutable InternetAddressList
        // that we can add addresses to
        var envelope = new Envelope();
        envelope.From.Add(new MailboxAddress(fromAddress, fromAddress));
        envelope.Subject = subject;

        // Create a mock message summary
        var mockMessage = new Mock<IMessageSummary>();
        mockMessage.SetupGet(m => m.Envelope).Returns(envelope);
        mockMessage.SetupGet(m => m.Date).Returns(messageDate);
        mockMessage.SetupGet(m => m.UniqueId).Returns(new UniqueId());

        return mockMessage.Object;
    }

    #endregion
}