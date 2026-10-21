namespace EmailSweepAutomation.Test.UnitTests;

using System.Text.Json;
using Xunit;

public class SweepRuleDeclaresActionTests
{
    [Fact]
    public void NothingSet_DoesNotDeclareAction() => Assert.False(new SweepRule().DeclaresAction);

    [Fact]
    public void DeleteFalse_StillDeclaresAction() => Assert.True(new SweepRule { Delete = false }.DeclaresAction);

    [Fact]
    public void DeleteTrue_DeclaresAction() => Assert.True(new SweepRule { Delete = true }.DeclaresAction);

    [Fact]
    public void AlertFalse_DeclaresAction() => Assert.True(new SweepRule { Alert = false }.DeclaresAction);

    [Fact]
    public void MoveToFolder_DeclaresAction() => Assert.True(new SweepRule { MoveToFolder = "Receipts" }.DeclaresAction);

    [Fact]
    public void BlankMoveToFolder_DoesNotDeclareAction() => Assert.False(new SweepRule { MoveToFolder = "  " }.DeclaresAction);

    [Fact]
    public void IsNotSerialized()
    {
        var json = JsonSerializer.Serialize(new SweepRule { Id = 1, FromAddress = "a@b.com", Delete = true, KeepRecent = 1 });
        Assert.DoesNotContain("DeclaresAction", json);
    }
}
