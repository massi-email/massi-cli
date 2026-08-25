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

    #region GetRuleMatches Tests

    [Fact]
    public void GetRuleMatches_WithEmptyMessages_ReturnsEmptyList()
    {
        // Arrange
        var messages = new List<IMessageSummary>();
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "test@example.com", KeepRecent = 1, Delete = true }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetRuleMatches_WithEmptyRules_ReturnsEmptyList()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "Test Subject") };
        var rules = new List<SweepRule>();

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetRuleMatches_WithFromAddressMatch_ReturnsMatchedMessage()
    {
        // Arrange
        var fromAddress = "sender@example.com";
        var messages = new List<IMessageSummary> { CreateMockMessageSummary(fromAddress, "Test Subject") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecent = 0, Delete = true }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Equal(fromAddress, result[0].Message.Envelope.From.Mailboxes.First().Address);
    }

    [Fact]
    public void GetRuleMatches_WithSubjectMatch_ReturnsMatchedMessage()
    {
        // Arrange
        var subject = "Important Update";
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", subject) };
        var rules = new List<SweepRule>
        {
            new SweepRule { SubjectContains = "Important", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Contains("Important", result[0].Message.Envelope.Subject);
    }

    [Fact]
    public void GetRuleMatches_WithCaseInsensitiveFromAddress_ReturnsMatch()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("Sender@Example.COM", "Test") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "sender@example.com", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetRuleMatches_WithCaseInsensitiveSubject_ReturnsMatch()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "NEWSLETTER") };
        var rules = new List<SweepRule>
        {
            new SweepRule { SubjectContains = "newsletter", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetRuleMatches_WithBothFromAndSubjectMatch_ReturnsMatchedMessage()
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
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetRuleMatches_WithPartialFromAddressMatch_ReturnsMatch()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("notifications@github.com", "Test") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "github", KeepRecent = 0, Delete = true }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetRuleMatches_NoMatchFromAddress_ReturnsEmpty()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "Test") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = "nomatch@example.com", KeepRecent = 1, Delete = true }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetRuleMatches_NoMatchSubject_ReturnsEmpty()
    {
        // Arrange
        var messages = new List<IMessageSummary> { CreateMockMessageSummary("sender@example.com", "Hello") };
        var rules = new List<SweepRule>
        {
            new SweepRule { SubjectContains = "Goodbye", KeepRecent = 1, Delete = true }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetRuleMatches_WithKeepRecentCount_FiltersCorrectly()
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
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        // Only messages after keeping the 2 most recent should match (1 message)
        Assert.Single(result);
    }

    [Fact]
    public void GetRuleMatches_WithKeepRecentTime_FiltersOldMessages()
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
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Equal(oldMessage, result[0].Message);
    }

    [Fact]
    public void GetRuleMatches_WithMultipleRules_ReturnsAllMatches()
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
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetRuleMatches_WithMoveToFolderAction_ReturnsMatchedMessages()
    {
        // Arrange
        var fromAddress = "sender@example.com";
        var messages = new List<IMessageSummary> { CreateMockMessageSummary(fromAddress, "Test") };
        var rules = new List<SweepRule>
        {
            new SweepRule { FromAddress = fromAddress, KeepRecent = 0, MoveToFolder = "Archive" }
        };

        // Act
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert
        Assert.Single(result);
        Assert.Equal("Archive", result[0].Rule.MoveToFolder);
    }

    [Fact]
    public void GetRuleMatches_WithKeepRecent_DifferentSubjectsSameSender_CountsPerSender()
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
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert - only the single oldest message should match
        Assert.Single(result);
        Assert.Equal("Order #1", result[0].Message.Envelope.Subject);
    }

    [Fact]
    public void GetRuleMatches_WithKeepRecent_KeepsMostRecentByDateNotInputOrder()
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
        var result = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert - the 2 newest (newest + middle) are kept; only the oldest matches
        Assert.Single(result);
        Assert.Equal(oldest, result[0].Message);
    }

    [Fact]
    public void GetRuleMatches_MessageMatchesMultipleRules_ReturnsMultipleMatches()
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
        var result = _ruleEngine.GetRuleMatches(messages, rules);

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

    [Fact]
    public void EvaluateRules_SelectedMessagesMatchGetRuleMatches()
    {
        // Arrange - two senders, two rules, mixed KeepRecent.
        var now = DateTimeOffset.Now;
        var messages = new List<IMessageSummary>
        {
            CreateMockMessageSummary("a@example.com", "1", now.AddDays(-1)),
            CreateMockMessageSummary("a@example.com", "2", now.AddDays(-2)),
            CreateMockMessageSummary("a@example.com", "3", now.AddDays(-3)),
            CreateMockMessageSummary("b@example.com", "x", now.AddDays(-1))
        };
        var rules = new List<SweepRule>
        {
            new SweepRule { Id = 1, FromAddress = "a@example.com", KeepRecent = 1, Delete = true },
            new SweepRule { Id = 2, FromAddress = "b@example.com", KeepRecent = 0, Delete = true }
        };

        // Act
        var evaluations = _ruleEngine.EvaluateRules(messages, rules);
        var matches = _ruleEngine.GetRuleMatches(messages, rules);

        // Assert - EvaluateRules' Selected sets are exactly what GetRuleMatches returns
        var selectedFromEvaluations = evaluations.SelectMany(e => e.Selected).ToList();
        Assert.Equal(matches.Count, selectedFromEvaluations.Count);
        Assert.All(matches, m => Assert.Contains(m.Message, selectedFromEvaluations));
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