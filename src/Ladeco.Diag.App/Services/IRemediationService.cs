namespace Ladeco.Diag.App.Services;

public interface IRemediationService
{
    Task<(bool Success, string Output)> RunSafeActionAsync(string actionKey, CancellationToken cancellationToken = default);
}
