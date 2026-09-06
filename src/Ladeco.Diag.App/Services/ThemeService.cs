using Microsoft.UI.Xaml;

namespace Ladeco.Diag.App.Services;

public sealed class ThemeService : IThemeService
{
    public void ApplyLightTheme() => Apply(ElementTheme.Light);

    public void ApplyDarkTheme() => Apply(ElementTheme.Dark);

    private static void Apply(ElementTheme theme)
    {
        if (App.MainWindow?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = theme;
    }
}
