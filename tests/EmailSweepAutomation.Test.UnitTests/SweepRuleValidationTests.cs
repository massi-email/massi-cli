namespace EmailSweepAutomation.Test.UnitTests;

using Xunit;

public class SweepRuleValidationTests
{
    #region Valid Rules Tests

    [Fact]
    public void IsRuleValid_WithAllRequiredFields_ReturnsValid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 5,
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsRuleValid_WithSubjectContainsAndKeepRecentTime_ReturnsValid()
    {
        // Arrange
        var rule = new SweepRule
        {
            SubjectContains = "Newsletter",
            KeepRecentTime = TimeSpan.FromDays(7),
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsRuleValid_WithMoveToFolderAction_ReturnsValid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 3,
            MoveToFolder = "Archive"
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsRuleValid_WithBothFromAddressAndSubjectContains_ReturnsValid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            SubjectContains = "Weekly Report",
            KeepRecent = 2,
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsRuleValid_WithBothKeepRecentAndKeepRecentTime_ReturnsValid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 5,
            KeepRecentTime = TimeSpan.FromDays(30),
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    #endregion

    #region Missing Matching Criteria Tests

    [Fact]
    public void IsRuleValid_WithNoFromAddressOrSubjectContains_ReturnsInvalid()
    {
        // Arrange
        var rule = new SweepRule
        {
            KeepRecent = 5,
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.Equal("At least one of FromAddress, FromDomain or SubjectContains must be specified", error);
    }

    [Fact]
    public void IsRuleValid_WithNullFromAddressAndNullSubjectContains_ReturnsInvalid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = null,
            SubjectContains = null,
            KeepRecent = 5,
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.Contains("FromAddress, FromDomain or SubjectContains", error);
    }

    #endregion

    #region Missing Keep Recent Criteria Tests

    [Fact]
    public void IsRuleValid_WithNoKeepRecentOrKeepRecentTime_ReturnsInvalid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.Equal("At least one of KeepRecent or KeepRecentTime must be specified", error);
    }

    [Fact]
    public void IsRuleValid_WithNullKeepRecentAndNullKeepRecentTime_ReturnsInvalid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = null,
            KeepRecentTime = null,
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.Contains("KeepRecent or KeepRecentTime", error);
    }

    #endregion

    #region Missing Action Tests

    [Fact]
    public void IsRuleValid_WithNoDeleteOrMoveToFolder_ReturnsInvalid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 5
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.Equal("At least one of Delete, MoveToFolder or Alert must be specified", error);
    }

    [Fact]
    public void IsRuleValid_WithNullDeleteAndNullMoveToFolder_ReturnsInvalid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 5,
            Delete = null,
            MoveToFolder = null
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.Contains("Delete, MoveToFolder or Alert", error);
    }

    #endregion

    #region Conflicting Actions Tests

    [Fact]
    public void IsRuleValid_WithBothDeleteAndMoveToFolder_ReturnsInvalid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 5,
            Delete = true,
            MoveToFolder = "Archive"
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.Equal("Only one of Delete, MoveToFolder or Alert can be specified", error);
    }

    [Fact]
    public void IsRuleValid_WithDeleteFalseAndMoveToFolder_ReturnsInvalid()
    {
        // Arrange - Even Delete = false counts as "specified"
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 5,
            Delete = false,
            MoveToFolder = "Archive"
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.Contains("Only one of Delete, MoveToFolder or Alert", error);
    }

    #endregion

    #region Alert Action Tests

    [Fact]
    public void IsRuleValid_AlertOnly_ReturnsValid()
    {
        var rule = new SweepRule
        {
            FromAddress = "boss@example.com",
            Alert = true
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsRuleValid_AlertWithSubjectOnly_ReturnsValid()
    {
        var rule = new SweepRule
        {
            SubjectContains = "urgent",
            Alert = true
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsRuleValid_AlertWithDelete_ReturnsInvalid()
    {
        var rule = new SweepRule
        {
            FromAddress = "boss@example.com",
            Alert = true,
            Delete = true
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("Only one of Delete, MoveToFolder or Alert can be specified", error);
    }

    [Fact]
    public void IsRuleValid_AlertWithMoveToFolder_ReturnsInvalid()
    {
        var rule = new SweepRule
        {
            FromAddress = "boss@example.com",
            Alert = true,
            MoveToFolder = "Important"
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("Only one of Delete, MoveToFolder or Alert can be specified", error);
    }

    [Fact]
    public void IsRuleValid_AlertWithKeepRecent_ReturnsInvalid()
    {
        var rule = new SweepRule
        {
            FromAddress = "boss@example.com",
            Alert = true,
            KeepRecent = 5
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("KeepRecent and KeepRecentTime cannot be used with Alert", error);
    }

    [Fact]
    public void IsRuleValid_AlertWithKeepRecentTime_ReturnsInvalid()
    {
        var rule = new SweepRule
        {
            FromAddress = "boss@example.com",
            Alert = true,
            KeepRecentTime = TimeSpan.FromDays(7)
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("KeepRecent and KeepRecentTime cannot be used with Alert", error);
    }

    [Fact]
    public void IsRuleValid_AlertFalseOnly_ReturnsValid()
    {
        // Alert = false is still a "specified" action, consistent with the Delete = false quirk.
        var rule = new SweepRule
        {
            FromAddress = "boss@example.com",
            Alert = false
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.True(isValid);
        Assert.Null(error);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void IsRuleValid_WithEmptyRule_ReturnsInvalid()
    {
        // Arrange
        var rule = new SweepRule();

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void IsRuleValid_WithKeepRecentZero_ReturnsValid()
    {
        // Arrange - KeepRecent = 0 means delete all matching messages
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 0,
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsRuleValid_WithKeepRecentTimeZero_ReturnsValid()
    {
        // Arrange
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecentTime = TimeSpan.Zero,
            Delete = true
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void GetValidationErrors_ReturnsOnlyInvalidRulesWithMessages()
    {
        // Arrange
        var rules = new[]
        {
            new SweepRule { FromAddress = "a@example.com", KeepRecent = 1, Delete = true },        // valid
            new SweepRule { KeepRecent = 1, Delete = true },                                       // invalid: no from/subject
            new SweepRule { FromAddress = "b@example.com", KeepRecent = 1, Delete = true, MoveToFolder = "X" } // invalid: both actions
        };

        // Act
        var errors = rules.GetValidationErrors();

        // Assert
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Error == "At least one of FromAddress, FromDomain or SubjectContains must be specified");
        Assert.Contains(errors, e => e.Error == "Only one of Delete, MoveToFolder or Alert can be specified");
    }

    [Fact]
    public void GetValidationErrors_AllValid_ReturnsEmpty()
    {
        // Arrange
        var rules = new[]
        {
            new SweepRule { FromAddress = "a@example.com", KeepRecent = 1, Delete = true }
        };

        // Act & Assert
        Assert.Empty(rules.GetValidationErrors());
    }

    [Fact]
    public void IsRuleValid_WithDeleteFalseOnly_ReturnsValid()
    {
        // Arrange - Delete = false is still a valid action specification
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 5,
            Delete = false
        };

        // Act
        var (isValid, error) = rule.IsRuleValid();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    #endregion

    #region Blank And Out-Of-Range Values

    // A criterion that is present but blank used to pass validation and then match every
    // message: TryMatchCriteria skips a criterion it sees as empty, so "" is "no criterion".
    // The web client nulls blanks before it posts; MCP and a raw API call do not.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void IsRuleValid_WithBlankFromAddress_ReturnsInvalid(string blank)
    {
        var rule = new SweepRule { FromAddress = blank, KeepRecent = 5, Delete = true };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("At least one of FromAddress, FromDomain or SubjectContains must be specified", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IsRuleValid_WithBlankSubjectContains_ReturnsInvalid(string blank)
    {
        var rule = new SweepRule { SubjectContains = blank, KeepRecent = 5, Delete = true };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("At least one of FromAddress, FromDomain or SubjectContains must be specified", error);
    }

    [Fact]
    public void IsRuleValid_WithBlankFromAddressAndRealSubject_ReturnsValid()
    {
        // One real criterion is enough — a blank sibling is absent, not invalid.
        var rule = new SweepRule
        {
            FromAddress = "",
            SubjectContains = "Newsletter",
            KeepRecent = 5,
            Delete = true,
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.True(isValid);
        Assert.Null(error);
    }

    // A blank MoveToFolder is not an action. Left as one, it counted toward actionsSpecified
    // and then reached MoveMessagesAsync, whose own filter is IsNullOrEmpty — so "  " passed
    // that too and became a folder name.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsRuleValid_WithBlankMoveToFolder_ReturnsInvalid(string blank)
    {
        var rule = new SweepRule { FromAddress = "sender@example.com", KeepRecent = 5, MoveToFolder = blank };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("At least one of Delete, MoveToFolder or Alert must be specified", error);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData(" / ")]
    public void IsRuleValid_WithMoveToFolderOfOnlySeparators_ReturnsInvalid(string separators)
    {
        // '/' separates the levels of a destination path, so a path made only of separators
        // names no folder at all.
        var rule = new SweepRule { FromAddress = "sender@example.com", KeepRecent = 5, MoveToFolder = separators };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("MoveToFolder must name a folder", error);
    }

    [Fact]
    public void IsRuleValid_WithNestedMoveToFolder_ReturnsValid()
    {
        // '/' is the destination path separator, not an illegal character.
        var rule = new SweepRule { FromAddress = "sender@example.com", KeepRecent = 5, MoveToFolder = "Archive/2026" };

        var (isValid, error) = rule.IsRuleValid();

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsRuleValid_WithNegativeKeepRecent_ReturnsInvalid()
    {
        // Skip(-1) skips nothing, so a negative keep is a silent "keep none" — and unlike 0,
        // nobody means it.
        var rule = new SweepRule { FromAddress = "sender@example.com", KeepRecent = -1, Delete = true };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("KeepRecent cannot be negative", error);
    }

    [Fact]
    public void IsRuleValid_WithNegativeKeepRecentTime_ReturnsInvalid()
    {
        // A negative keep window puts the cutoff in the future, selecting everything matched.
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecentTime = TimeSpan.FromDays(-1),
            Delete = true,
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("KeepRecentTime cannot be negative", error);
    }

    #endregion

    [Fact]
    public void IsRuleValid_DeleteRuleCarryingABlankMoveToFolder_IsValid()
    {
        // Blank has to mean absent everywhere or nowhere. The action count already ignores a
        // blank folder; the folder-shape check below it must ignore the same value, or a
        // perfectly good delete rule is rejected for the shape of a field it does not use.
        var rule = new SweepRule
        {
            FromAddress = "sender@example.com",
            KeepRecent = 5,
            Delete = true,
            MoveToFolder = "   ",
        };

        var (isValid, error) = rule.IsRuleValid();

        Assert.True(isValid);
        Assert.Null(error);
    }

    #region Branched Rules

    private static SweepBranch Branch(string label, SweepBranchAction action, string[]? from = null,
        string[]? subjects = null, string? folder = null, int? keep = null, TimeSpan? keepTime = null) => new()
    {
        Label = label, Action = action, FromAny = from?.ToList(), SubjectContainsAny = subjects?.ToList(),
        MoveToFolder = folder, KeepRecent = keep, KeepRecentTime = keepTime,
    };

    private static SweepRule Branched(params SweepBranch[] branches) =>
        new() { FromDomain = "shop.com", Branches = branches.ToList() };

    [Fact]
    public void IsRuleValid_FromDomainAlone_WithAnAction_IsValid()
        => Assert.True(new SweepRule { FromDomain = "shop.com", Delete = true, KeepRecent = 3 }.IsRuleValid().isValid);

    [Theory]
    [InlineData("shop")]
    [InlineData("a@shop.com")]
    [InlineData("sh op.com")]
    public void IsRuleValid_AMalformedFromDomain_IsInvalid(string domain)
    {
        var (ok, error) = new SweepRule { FromDomain = domain, Delete = true, KeepRecent = 3 }.IsRuleValid();
        Assert.False(ok);
        Assert.Equal("FromDomain must be a domain such as shop.com", error);
    }

    [Fact]
    public void IsRuleValid_FromAddressAndFromDomain_IsInvalid()
    {
        var (ok, error) = new SweepRule { FromAddress = "a@shop.com", FromDomain = "shop.com", Delete = true, KeepRecent = 3 }.IsRuleValid();
        Assert.False(ok);
        Assert.Equal("Only one of FromAddress or FromDomain can be specified", error);
    }

    [Fact]
    public void IsRuleValid_ATypicalBranchedRule_IsValid()
    {
        var rule = Branched(
            Branch("Account", SweepBranchAction.Leave, from: ["@account.shop.com"]),
            Branch("Orders", SweepBranchAction.MoveToFolder, from: ["transaction@notice.shop.com"], folder: "Massi/Orders", keepTime: TimeSpan.FromDays(30)),
            Branch("Promotions", SweepBranchAction.Delete, from: ["@mail.shop.com", "@*.deals.shop.com"], keep: 3));
        Assert.Equal((true, (string?)null), rule.IsRuleValid());
    }

    [Fact]
    public void IsRuleValid_BranchSenderPatterns_AreValid()
    {
        var rule = Branched(Branch("Promotions", SweepBranchAction.Delete,
            from: ["ae-market*@deals.shop.com", "*-deals@shop.com", "*best-care-market*@deals.shop.com"], keep: 3));
        Assert.Equal((true, (string?)null), rule.IsRuleValid());
    }

    [Fact]
    public void IsRuleValid_ASingleCatchAllDeleteBranch_IsValid()
        => Assert.True(Branched(Branch("All", SweepBranchAction.Delete, keep: 3)).IsRuleValid().isValid);

    [Fact]
    public void IsRuleValid_ExactlyTheMaximumBranches_IsValid()
    {
        var rule = Branched(Enumerable.Range(1, SweepRuleExtensions.MaxBranches)
            .Select(i => Branch($"B{i}", SweepBranchAction.Leave, from: [$"a{i}@shop.com"])).ToArray());
        Assert.Equal((true, (string?)null), rule.IsRuleValid());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void IsRuleValid_ABlankTopLevelFolderOnABranchedRule_IsNotAnAction(string folder)
    {
        // Forms send blanks, not nulls; a blank folder names nothing to move into.
        var rule = Branched(Branch("All", SweepBranchAction.Delete, keep: 3));
        rule.MoveToFolder = folder;
        Assert.Equal((true, (string?)null), rule.IsRuleValid());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void IsRuleValid_ALeaveBranchWithABlankFolder_IsValid(string folder)
        => Assert.Equal((true, (string?)null),
            Branched(Branch("A", SweepBranchAction.Leave, from: ["@a.shop.com"], folder: folder)).IsRuleValid());

    [Theory]
    [MemberData(nameof(InvalidBranchedRules))]
    public void IsRuleValid_InvalidBranchedRules(SweepRule rule, string expected)
    {
        var (ok, error) = rule.IsRuleValid();
        Assert.False(ok);
        Assert.Equal(expected, error);
    }

    public static TheoryData<SweepRule, string> InvalidBranchedRules()
    {
        var promo = Branch("Promotions", SweepBranchAction.Delete, from: ["@mail.shop.com"], keep: 3);
        var withTopLevelAction = Branched(promo);
        withTopLevelAction.Delete = true;
        var withTopLevelKeep = Branched(promo);
        withTopLevelKeep.KeepRecent = 3;
        var alert = Branched(promo);
        alert.Alert = true;
        return new()
        {
            { withTopLevelAction, "A rule with branches cannot have an action of its own" },
            { withTopLevelKeep, "A rule with branches cannot have KeepRecent or KeepRecentTime of its own" },
            { alert, "A rule with branches cannot have an action of its own" },
            { Branched(Enumerable.Range(1, 13).Select(i => Branch($"B{i}", SweepBranchAction.Leave, from: [$"a{i}@shop.com"])).ToArray()), "A rule can have at most 12 branches" },
            { Branched(Branch(" ", SweepBranchAction.Leave, from: ["@mail.shop.com"])), "Every branch needs a label" },
            { Branched(Branch("All", SweepBranchAction.Leave), promo), "Only the last branch can have no conditions" },
            { Branched(promo, Branch("Rest", SweepBranchAction.Delete, keep: 3)), "The last branch cannot delete everything else" },
            { Branched(Branch("P", SweepBranchAction.Delete, from: ["@mail.shop.com"])), "Branch 'P' needs KeepRecent or KeepRecentTime" },
            { Branched(Branch("O", SweepBranchAction.MoveToFolder, from: ["@notice.shop.com"], keep: 1)), "Branch 'O' must name a folder" },
            { Branched(Branch("P", SweepBranchAction.Delete, from: ["@mail.shop.com"], folder: "X", keep: 3)), "Only a move branch can name a folder" },
            { Branched(Branch("A", SweepBranchAction.Leave, from: ["@a.shop.com"], keep: 3)), "A leave branch keeps everything, so it has no KeepRecent or KeepRecentTime" },
            { Branched(Branch("P", SweepBranchAction.Delete, from: ["@mail.shop.com"], keep: -1)), "KeepRecent cannot be negative" },
            { Branched(Branch("P", SweepBranchAction.Delete, from: ["mail.shop.com"], keep: 3)), "Branch 'P' has a sender that is not user@host, @host, @*.host or a pattern (name*@host, *name@host, *name*@host): mail.shop.com" },
            { Branched(Branch("P", SweepBranchAction.Delete, from: ["pro*mo@shop.com"], keep: 3)), "Branch 'P' has a sender that is not user@host, @host, @*.host or a pattern (name*@host, *name@host, *name*@host): pro*mo@shop.com" },
            { Branched(Branch("P", SweepBranchAction.Delete, subjects: [" "], keep: 3)), "Branch 'P' has a blank subject keyword" },
            { Branched(Branch("P", SweepBranchAction.Delete, subjects: ["sale", " "], keep: 3)), "Branch 'P' has a blank subject keyword" },
            { Branched(Branch("P", (SweepBranchAction)42, from: ["@mail.shop.com"], keep: 3)), "Branch 'P' has an unknown action" },
            { Branched(Branch("P", SweepBranchAction.Alert, from: ["@mail.shop.com"])), "Branch 'P' cannot alert; only a rule without branches can" },
        };
    }

    #endregion
}
