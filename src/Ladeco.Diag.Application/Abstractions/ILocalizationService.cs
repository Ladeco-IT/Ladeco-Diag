namespace Ladeco.Diag.Application.Abstractions;

public interface ILocalizationService
{
    string this[string key] { get; }
}
