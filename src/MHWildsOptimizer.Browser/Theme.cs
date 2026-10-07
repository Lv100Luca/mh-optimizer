using MudBlazor;

namespace MHWildsOptimizer.Browser;

/// <summary>The dark, gold-accented look of the server version's client (styles.css variables), as a MudBlazor theme.</summary>
public static class Theme
{
    public static readonly MudTheme Dark = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#e3b64e",
            PrimaryContrastText = "#1a1507",
            Secondary = "#4fa3e6",
            Info = "#4fa3e6",
            Success = "#5fbf7a",
            Warning = "#e8b04b",
            Error = "#e05a4f",
            Background = "#121419",
            BackgroundGray = "#181b22",
            Surface = "#1e222b",
            AppbarBackground = "#181b22",
            DrawerBackground = "#181b22",
            TextPrimary = "#e8e6e1",
            TextSecondary = "#9aa1ad",
            ActionDefault = "#9aa1ad",
            LinesDefault = "#2f3542",
            LinesInputs = "#3b4254",
            Divider = "#2f3542",
            TableLines = "#2f3542",
            OverlayDark = "rgba(10, 11, 14, 0.6)",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "6px" },
    };
}
