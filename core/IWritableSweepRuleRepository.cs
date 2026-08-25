using System.Threading.Tasks;

namespace EmailSweepAutomation
{
    public interface IWritableSweepRuleRepository : ISweepRuleRepository
    {
        Task AddRuleAsync(SweepRule rule);
        Task AddRulesAsync(IEnumerable<SweepRule> rules);
        Task UpdateRuleAsync(SweepRule rule);
        Task DeleteRuleAsync(int id);
        Task SaveAsync();
    }
}
