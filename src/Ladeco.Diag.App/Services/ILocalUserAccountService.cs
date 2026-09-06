namespace Ladeco.Diag.App.Services;

public interface ILocalUserAccountService
{
    Task<AccountOperationResult> CreateAdministratorAsync(string userName, string password);
    void LogoffCurrentSession();
}

public sealed record AccountOperationResult(bool Success, string Message);