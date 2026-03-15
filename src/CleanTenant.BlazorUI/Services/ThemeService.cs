using MudBlazor;

namespace CleanTenant.BlazorUI.Services;

/// <summary>
/// Kurumsal tema yönetimi — FluentUI tarzı, yeşil renk teması.
/// Dark/Light toggle desteği.
/// </summary>
public class ThemeService
{
    public bool IsDarkMode { get; set; } = false;
    
    public event Action? OnThemeChanged;

    public void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        OnThemeChanged?.Invoke();
    }

    /// <summary>Kurumsal CleanTenant teması.</summary>
    public MudTheme CleanTenantTheme => new()
    {
        PaletteLight = new PaletteLight
        {
            // Kurumsal yeşil — göz yormayan, albenili
            Primary = "#2e7d32",          // Forest Green
            PrimaryDarken = "#1b5e20",
            PrimaryLighten = "#4caf50",
            Secondary = "#00695c",         // Teal
            SecondaryDarken = "#004d40",
            SecondaryLighten = "#26a69a",
            Tertiary = "#558b2f",          // Light Green accent
            
            // Yüzeyler — FluentUI tarzı temiz, hafif gölgeli
            Background = "#f8faf8",        // Çok hafif yeşilimsi beyaz
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            AppbarBackground = "#ffffff",
            
            // Metin
            TextPrimary = "#1a2e1a",       // Koyu yeşilimsi siyah
            TextSecondary = "#546e54",
            TextDisabled = "#9e9e9e80",
            DrawerText = "#2e4a2e",
            DrawerIcon = "#2e7d32",
            AppbarText = "#1a2e1a",
            
            // Aksiyon renkleri
            ActionDefault = "#546e54",
            ActionDisabled = "#9e9e9e80",
            
            // Durumlar
            Info = "#0288d1",
            Success = "#2e7d32",
            Warning = "#f57c00",
            Error = "#d32f2f",
            
            // Border
            Divider = "#e0e8e0",
            LinesDefault = "#e0e8e0",
            TableLines = "#e8f0e8",
            TableStriped = "#f1f7f1",
            TableHover = "#e8f5e9",
            
            // Özel
            HoverOpacity = 0.06,
            RippleOpacity = 0.08
        },
        
        PaletteDark = new PaletteDark
        {
            Primary = "#66bb6a",          // Light Green
            PrimaryDarken = "#43a047",
            PrimaryLighten = "#81c784",
            Secondary = "#4db6ac",         // Teal light
            SecondaryDarken = "#26a69a",
            SecondaryLighten = "#80cbc4",
            Tertiary = "#8bc34a",
            
            // Yüzeyler — koyu ama sıcak
            Background = "#0f1610",        // Çok koyu yeşilimsi
            Surface = "#1a261c",
            DrawerBackground = "#152018",
            AppbarBackground = "#152018",
            
            // Metin
            TextPrimary = "#e0f2e0de",
            TextSecondary = "#a5c8a5b3",
            TextDisabled = "#9e9e9e4d",
            DrawerText = "#c8e6c8b3",
            DrawerIcon = "#66bb6a",
            AppbarText = "#e0f2e0de",
            
            ActionDefault = "#a5c8a5b3",
            
            Info = "#4fc3f7",
            Success = "#66bb6a",
            Warning = "#ffb74d",
            Error = "#ef5350",
            
            Divider = "#2d4030",
            LinesDefault = "#2d4030",
            TableLines = "#263a28",
            TableStriped = "#1e2e20",
            TableHover = "#243826",
            
            HoverOpacity = 0.12,
            RippleOpacity = 0.15
        },

		// FluentUI tarzı tipografi — Inter fontu, temiz okuma
		Typography = new Typography
		{
			Default = new DefaultTypography
			{
				FontFamily = ["Inter", "Segoe UI", "-apple-system", "sans-serif"],
				FontSize = "0.875rem",
				FontWeight = "400",
				LineHeight = "1.5",
				LetterSpacing = "0.00938em"
			},
			H1 = new H1Typography { FontSize = "2rem", FontWeight = "600", LineHeight = "1.2" },
			H2 = new H2Typography { FontSize = "1.75rem", FontWeight = "600", LineHeight = "1.25" },
			H3 = new H3Typography { FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.3" },
			H4 = new H4Typography { FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.35" },
			H5 = new H5Typography { FontSize = "1.1rem", FontWeight = "600", LineHeight = "1.4" },
			H6 = new H6Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.45" },
			Subtitle1 = new Subtitle1Typography { FontSize = "0.9375rem", FontWeight = "500" },
			Subtitle2 = new Subtitle2Typography { FontSize = "0.8125rem", FontWeight = "500" },
			Body1 = new Body1Typography { FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.6" },
			Body2 = new Body2Typography { FontSize = "0.8125rem", FontWeight = "400", LineHeight = "1.55" },
			Button = new ButtonTypography { FontSize = "0.8125rem", FontWeight = "500", LetterSpacing = "0.02em", TextTransform = "none" }
		},

		// FluentUI tarzı yuvarlatılmış köşeler
		LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "280px"
        },
        
        // Gölgeler — hafif, modern
        Shadows = new Shadow
        {
            Elevation = [
                "none",
                "0 1px 3px rgba(0,0,0,0.08), 0 1px 2px rgba(0,0,0,0.06)",
                "0 2px 6px rgba(0,0,0,0.08), 0 1px 3px rgba(0,0,0,0.06)",
                "0 4px 12px rgba(0,0,0,0.08), 0 2px 4px rgba(0,0,0,0.06)",
                "0 6px 16px rgba(0,0,0,0.08), 0 3px 6px rgba(0,0,0,0.06)",
                "0 8px 24px rgba(0,0,0,0.1), 0 4px 8px rgba(0,0,0,0.06)",
                .. Enumerable.Range(0, 20).Select(_ => "0 10px 32px rgba(0,0,0,0.12), 0 6px 12px rgba(0,0,0,0.08)").ToArray()
            ]
        }
    };
}
