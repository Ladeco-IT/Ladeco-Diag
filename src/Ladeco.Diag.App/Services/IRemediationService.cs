namespace Ladeco.Diag.App.Services;

public interface IRemediationService
{
    Task<(bool Success, string Output)> RunSafeActionAsync(
        string actionKey,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
