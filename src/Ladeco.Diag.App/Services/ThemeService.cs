using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

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
        ApplyTheme(root, theme);
    }

    private static void ApplyTheme(DependencyObject element, ElementTheme theme)
    {
        if (element is FrameworkElement frameworkElement)
        {
            frameworkElement.RequestedTheme = theme;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            ApplyTheme(VisualTreeHelper.GetChild(element, index), theme);
        }
    }
}
