using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Application.Diagnostics;
using Ladeco.Diag.Domain.Diagnostics;
using Xunit;

namespace Ladeco.Diag.Tests;

public sealed class DiagnosticsOrchestratorTests
{
    [Fact]
    public async Task RunFullScanAsync_ShouldAggregateModuleResults()
    {
        var modules = new List<IDiagnosticModule>
        {
            new FakeModule("M1", 2),
            new FakeModule("M2", 1)
        };

        var orchestrator = new DiagnosticsOrchestrator(modules);
        var report = await orchestrator.RunFullScanAsync();

        Assert.Equal(2, report.ModuleResults.Count);
        Assert.Equal(3, report.AllFindings.Count);
    }

    private sealed class FakeModule : IDiagnosticModule
    {
        private readonly int _findingCount;

        public FakeModule(string name, int findingCount)
        {
            Name = name;
            _findingCount = findingCount;
        }

        public string Name { get; }

        public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var findings = Enumerable.Range(1, _findingCount)
                .Select(i => new Finding(
                    Code: $"{Name}_{i}",
                    Title: $"Issue {i}",
                    Description: "Description",
                    ProbableCause: "Cause",
                    Resolution: "Resolution",
                    Severity: DiagnosticSeverity.Low,
                    Priority: i,
                    AutoFixAvailable: false,
                    AutoFixCommand: null,
                    EstimatedMinutes: 1,
                    Difficulty: "Makkelijk"))
                .ToList();

            return Task.FromResult(new ModuleResult(Name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, findings, new Dictionary<string, string>()));
        }
    }
}
