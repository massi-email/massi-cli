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
        Assert.Equal("At least one of FromAddress or SubjectContains must be specified", error);
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
        Assert.Contains("FromAddress or SubjectContains", error);
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
        Assert.Contains(errors, e => e.Error == "At least one of FromAddress or SubjectContains must be specified");
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
        Assert.Equal("At least one of FromAddress or SubjectContains must be specified", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IsRuleValid_WithBlankSubjectContains_ReturnsInvalid(string blank)
    {
        var rule = new SweepRule { SubjectContains = blank, KeepRecent = 5, Delete = true };

        var (isValid, error) = rule.IsRuleValid();

        Assert.False(isValid);
        Assert.Equal("At least one of FromAddress or SubjectContains must be specified", error);
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
}
