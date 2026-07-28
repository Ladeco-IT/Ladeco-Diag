using System.Windows;

namespace Ladeco.Diag.App.Services;

public sealed class ThemeService : IThemeService
{
    public void ApplyLightTheme() => Apply("Resources/Themes/LightTheme.xaml");

    public void ApplyDarkTheme() => Apply("Resources/Themes/DarkTheme.xaml");

    private static void Apply(string source)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var dictionaries = app.Resources.MergedDictionaries;
        var existingTheme = dictionaries.FirstOrDefault(x => x.Source is not null &&
                                                             (x.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                                                              x.Source.OriginalString.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)));
        if (existingTheme is not null)
        {
            dictionaries.Remove(existingTheme);
        }

        dictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
    }
}
