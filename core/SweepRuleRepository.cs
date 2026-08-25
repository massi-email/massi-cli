using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EmailSweepAutomation
{
    public class SweepRuleRepository : IWritableSweepRuleRepository
    {
        private readonly string file;
        private readonly ILogger<SweepRuleRepository> logger;

        // In-memory working copy, loaded on first access from the backing file.
        private List<SweepRule>? rules;

        // Logger is optional so the repository stays trivially constructable
        // (e.g. `new SweepRuleRepository(path)` in the CLI commands and tests).
        public SweepRuleRepository(string file, ILogger<SweepRuleRepository>? logger = null)
        {
            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentException($"'{nameof(file)}' cannot be null or empty.", nameof(file));
            }

            this.file = file;
            this.logger = logger ?? NullLogger<SweepRuleRepository>.Instance;
        }

        public async Task<IEnumerable<SweepRule>> GetRulesAsync()
        {
            await EnsureLoadedAsync();
            // Return a copy so callers can't mutate the working set out from under us.
            return rules!.ToList();
        }

        public IEnumerable<SweepRule> GetRulesFromString(string json)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new TimeSpanConverter());

            return JsonSerializer.Deserialize<IEnumerable<SweepRule>>(json, options) ?? Enumerable.Empty<SweepRule>();
        }

        public async Task AddRuleAsync(SweepRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            await EnsureLoadedAsync();

            if (rule.Id == 0)
            {
                rule.Id = rules!.Count == 0 ? 1 : rules.Max(r => r.Id) + 1;
            }

            rules!.Add(rule);
        }

        public async Task AddRulesAsync(IEnumerable<SweepRule> newRules)
        {
            ArgumentNullException.ThrowIfNull(newRules);
            foreach (var rule in newRules)
            {
                await AddRuleAsync(rule);
            }
        }

        public async Task UpdateRuleAsync(SweepRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            await EnsureLoadedAsync();

            var index = rules!.FindIndex(r => r.Id == rule.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"No rule with Id {rule.Id} exists.");
            }

            rules[index] = rule;
        }

        public async Task DeleteRuleAsync(int id)
        {
            await EnsureLoadedAsync();

            var removed = rules!.RemoveAll(r => r.Id == id);
            if (removed == 0)
            {
                throw new InvalidOperationException($"No rule with Id {id} exists.");
            }
        }

        public async Task SaveAsync()
        {
            await EnsureLoadedAsync();

            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new TimeSpanConverter());

            var json = JsonSerializer.Serialize(rules, options);
            await File.WriteAllTextAsync(file, json);
        }

        private async Task EnsureLoadedAsync()
        {
            if (rules is not null)
            {
                return;
            }

            logger.LogInformation("Reading rules from {File}", file);
            if (File.Exists(file))
            {
                var json = await File.ReadAllTextAsync(file);
                rules = GetRulesFromString(json).ToList();
            }
            else
            {
                logger.LogWarning("No rules file found in {File}", file);
                rules = new List<SweepRule>();
            }
        }
    }
}
