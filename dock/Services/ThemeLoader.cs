using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace OmarchyDock.Services;

// Reads theme/mocha.json directly at startup - no code-generation step needed,
// unlike the YASB side of this setup (see dotfiles README). Same single
// source of truth, simpler pipeline because this is a compiled app that can
// just parse JSON instead of needing a pre-baked CSS file.
internal static class ThemeLoader
{
    public static void LoadIntoApplicationResources()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "dotfiles", "theme", "mocha.json");

        if (!File.Exists(path)) return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var colors = doc.RootElement.GetProperty("colors");

        foreach (var prop in colors.EnumerateObject())
        {
            var color = (Color)ColorConverter.ConvertFromString(prop.Value.GetString())!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            Application.Current.Resources[Capitalize(prop.Name) + "Brush"] = brush;
            Application.Current.Resources[Capitalize(prop.Name) + "Color"] = color;
        }
    }

    private static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
