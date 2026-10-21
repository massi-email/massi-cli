namespace EmailSweepAutomation.Test.UnitTests;

using System.Reflection;
using System.Text.Json.Serialization;
using EmailSweepAutomation.Commands;
using Xunit;

public class RulesFileHelpTests
{
    public static TheoryData<string> RuleFileProperties()
    {
        var data = new TheoryData<string>();
        var seen = new HashSet<string>();
        foreach (var type in new[] { typeof(SweepRule), typeof(SweepBranch) })
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.SetMethod is not { IsPublic: true }) continue;
                // Only an unconditional [JsonIgnore] keeps a property out of the file; FromDomain and
                // Branches carry a WhenWritingNull one and are very much part of it.
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always }) continue;
                if (seen.Add(property.Name)) data.Add(property.Name);
            }
        return data;
    }

    [Theory]
    [MemberData(nameof(RuleFileProperties))]
    public void Text_NamesEveryRuleFileProperty(string property)
    {
        Assert.Contains(property, RulesFileHelp.Text);
    }

    public static IEnumerable<object[]> DocumentedBranchActions() =>
        Enum.GetValues<SweepBranchAction>().Where(a => a != SweepBranchAction.Alert).Select(a => new object[] { a });

    [Theory]
    [MemberData(nameof(DocumentedBranchActions))]
    public void Text_NamesEveryBranchAction(SweepBranchAction action)
    {
        Assert.Contains(action.ToString(), RulesFileHelp.Text);
    }

    [Fact]
    public void Text_StatesTheBranchLimit()
    {
        Assert.Contains($"at most {SweepRuleExtensions.MaxBranches}", RulesFileHelp.Text);
    }

    [Fact]
    public void Example_IsAValidRulesFile()
    {
        var rules = new SweepRuleRepository("unused.json").GetRulesFromString(RulesFileHelp.Example).ToList();

        Assert.NotEmpty(rules);
        Assert.All(rules, rule => Assert.Null(rule.Validate()));
        Assert.Contains(rules, rule => rule.IsBranched);
    }

    [Fact]
    public void Text_IncludesTheExample()
    {
        Assert.Contains(RulesFileHelp.Example, RulesFileHelp.Text);
    }
}
