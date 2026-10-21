using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmailSweepAutomation
{
    public interface ISweepRuleRepository
    {
        Task<IEnumerable<SweepRule>> GetRulesAsync();
    }
}
