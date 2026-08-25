using System;
using Spectre.Console;

namespace EmailSweepAutomation;

/// <summary>
/// Updates a Spectre <see cref="StatusContext"/> spinner with fetch progress.
/// Reports synchronously on the caller's thread (no Progress&lt;T&gt; marshaling).
/// </summary>
public sealed class StatusProgress : IProgress<FetchProgress>
{
    private readonly StatusContext context;

    public StatusProgress(StatusContext context) => this.context = context;

    public void Report(FetchProgress value) =>
        context.Status($"Fetched {value.Fetched} / {value.Total} emails");
}
