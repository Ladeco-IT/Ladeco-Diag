using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.App.Services;

public interface IFindingExplanationService
{
    string BuildExplanation(Finding finding);
}
