using UnityEngine;

namespace FinalDefense.UI
{
    [CreateAssetMenu(fileName = "UITheme", menuName = "FinalDefense/UI/Theme")]
    public class UITheme : ScriptableObject
    {
        private static UITheme _instance;
        public static UITheme Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<UITheme>("UITheme");
                    if (_instance == null)
                    {
                        _instance = CreateInstance<UITheme>();
                        _instance.SetDefaultValues();
                    }
                }
                return _instance;
            }
        }

        [Header("Background Colors")]
        public Color background = new Color(0xFF / 255f, 0xF8 / 255f, 0xFB / 255f);
        public Color foreground = new Color(0x2C / 255f, 0x21 / 255f, 0x34 / 255f);
        public Color card = new Color(0xFF / 255f, 0xFF / 255f, 0xFF / 255f);
        public Color cardForeground = new Color(0x2F / 255f, 0x23 / 255f, 0x40 / 255f);
        public Color popover = new Color(0xFF / 255f, 0xFA / 255f, 0xFD / 255f);
        public Color popoverForeground = new Color(0x2F / 255f, 0x23 / 255f, 0x40 / 255f);

        [Header("Brand Colors")]
        public Color primary = new Color(0xE6 / 255f, 0x6B / 255f, 0x8A / 255f);
        public Color primaryForeground = new Color(0xFF / 255f, 0xF7 / 255f, 0xFB / 255f);
        public Color secondary = new Color(0xF8 / 255f, 0xD7 / 255f, 0xE2 / 255f);
        public Color secondaryForeground = new Color(0x6D / 255f, 0x43 / 255f, 0x5B / 255f);
        public Color muted = new Color(0xF8 / 255f, 0xEE / 255f, 0xF3 / 255f);
        public Color mutedForeground = new Color(0x7D / 255f, 0x64 / 255f, 0x75 / 255f);
        public Color accent = new Color(0xFF / 255f, 0xE9 / 255f, 0xF1 / 255f);
        public Color accentForeground = new Color(0x7F / 255f, 0x47 / 255f, 0x62 / 255f);
        public Color destructive = new Color(0xD4 / 255f, 0x55 / 255f, 0x6F / 255f);
        public Color destructiveForeground = new Color(0xFF / 255f, 0xF7 / 255f, 0xFB / 255f);

        [Header("Border & Input")]
        public Color border = new Color(0xED / 255f, 0xD6 / 255f, 0xDF / 255f);
        public Color input = new Color(0xF1 / 255f, 0xDF / 255f, 0xE7 / 255f);
        public Color ring = new Color(0xF0 / 255f, 0x9A / 255f, 0xB1 / 255f);

        [Header("Chart Colors")]
        public Color chart1 = new Color(0xE6 / 255f, 0x6B / 255f, 0x8A / 255f);
        public Color chart2 = new Color(0x7F / 255f, 0x7C / 255f, 0xE0 / 255f);
        public Color chart3 = new Color(0xF3 / 255f, 0xB5 / 255f, 0x5A / 255f);
        public Color chart4 = new Color(0x7E / 255f, 0xC8 / 255f, 0xB8 / 255f);
        public Color chart5 = new Color(0xB7 / 255f, 0x8C / 255f, 0xF5 / 255f);

        [Header("Sidebar")]
        public Color sidebar = new Color(0xFF / 255f, 0xF2 / 255f, 0xF7 / 255f);
        public Color sidebarForeground = new Color(0x38 / 255f, 0x2B / 255f, 0x42 / 255f);
        public Color sidebarPrimary = new Color(0xE6 / 255f, 0x6B / 255f, 0x8A / 255f);
        public Color sidebarPrimaryForeground = new Color(0xFF / 255f, 0xF7 / 255f, 0xFB / 255f);
        public Color sidebarAccent = new Color(0xFC / 255f, 0xE5 / 255f, 0xEE / 255f);
        public Color sidebarAccentForeground = new Color(0x6E / 255f, 0x47 / 255f, 0x61 / 255f);

        [Header("Surface")]
        public Color surfaceStrong = new Color(0xFF / 255f, 0xE3 / 255f, 0xEC / 255f);
        public Color surfaceSoft = new Color(0xFF / 255f, 0xF2 / 255f, 0xF6 / 255f);
        public Color surfaceTint = new Color(0xFF / 255f, 0xF7 / 255f, 0xFA / 255f);

        [Header("State Colors")]
        public Color stateSuccess = new Color(0x4A / 255f, 0xA8 / 255f, 0x86 / 255f);
        public Color stateWarning = new Color(0xE5 / 255f, 0xA3 / 255f, 0x4F / 255f);
        public Color stateError = new Color(0xD4 / 255f, 0x55 / 255f, 0x6F / 255f);
        public Color stateInfo = new Color(0x6D / 255f, 0x7D / 255f, 0xE7 / 255f);

        [Header("Tower Type Colors")]
        public Color towerNotebook = new Color(0xE6 / 255f, 0x6B / 255f, 0x8A / 255f);
        public Color towerCalculator = new Color(0x7F / 255f, 0x7C / 255f, 0xE0 / 255f);
        public Color towerCoffeeCup = new Color(0xF3 / 255f, 0xB5 / 255f, 0x5A / 255f);
        public Color towerCoffee = new Color(0xF0 / 255f, 0xB4 / 255f, 0x7A / 255f);

        [Header("Enemy Type Colors")]
        public Color enemyMultipleChoice = new Color(0xCB / 255f, 0x6C / 255f, 0x82 / 255f);
        public Color enemyFillBlank = new Color(0x8C / 255f, 0x7B / 255f, 0xE7 / 255f);
        public Color enemyEssay = new Color(0x9E / 255f, 0x77 / 255f, 0xE6 / 255f);

        [Header("Radius")]
        public float radiusSmall = 10f;
        public float radiusMedium = 18f;
        public float radiusLarge = 28f;
        public float radiusCard = 24f;
        public float radiusButton = 18f;
        public float radiusPill = 100f;

        [Header("Typography")]
        public int h1FontSize = 44;
        public int h2FontSize = 30;
        public int h3FontSize = 24;
        public int bodyFontSize = 15;
        public int smallFontSize = 12;
        public int captionFontSize = 10;
        public float defaultLineSpacing = 1.7f;

        [Header("Shadows")]
        public Color shadowSoftColor = new Color(0xE6 / 255f, 0x6B / 255f, 0x8A / 255f, 0.10f);
        public Vector2 shadowSoftOffset = new Vector2(0, 14);
        public float shadowSoftBlur = 34f;

        public Color shadowCardColor = new Color(0x5B / 255f, 0x3F / 255f, 0x60 / 255f, 0.06f);
        public Vector2 shadowCardOffset = new Vector2(0, 8);
        public float shadowCardBlur = 22f;

        [Header("Gradients")]
        public Color gradientStart = new Color(0xFF / 255f, 0xFA / 255f, 0xFC / 255f);
        public Color gradientEnd = new Color(0xFF / 255f, 0xF2 / 255f, 0xF7 / 255f);

        public Color primaryGradientStart = new Color(0xE6 / 255f, 0x6B / 255f, 0x8A / 255f);
        public Color primaryGradientEnd = new Color(0x8D / 255f, 0x7E / 255f, 0xE8 / 255f);

        public void SetDefaultValues()
        {
            background = new Color(0xFF / 255f, 0xF8 / 255f, 0xFB / 255f);
            foreground = new Color(0x2C / 255f, 0x21 / 255f, 0x34 / 255f);
            card = Color.white;
            cardForeground = new Color(0x2F / 255f, 0x23 / 255f, 0x40 / 255f);
            primary = new Color(0xE6 / 255f, 0x6B / 255f, 0x8A / 255f);
            primaryForeground = new Color(0xFF / 255f, 0xF7 / 255f, 0xFB / 255f);
            secondary = new Color(0xF8 / 255f, 0xD7 / 255f, 0xE2 / 255f);
            secondaryForeground = new Color(0x6D / 255f, 0x43 / 255f, 0x5B / 255f);
            muted = new Color(0xF8 / 255f, 0xEE / 255f, 0xF3 / 255f);
            mutedForeground = new Color(0x7D / 255f, 0x64 / 255f, 0x75 / 255f);
            accent = new Color(0xFF / 255f, 0xE9 / 255f, 0xF1 / 255f);
            border = new Color(0xED / 255f, 0xD6 / 255f, 0xDF / 255f);
            chart2 = new Color(0x7F / 255f, 0x7C / 255f, 0xE0 / 255f);
            chart3 = new Color(0xF3 / 255f, 0xB5 / 255f, 0x5A / 255f);
            chart4 = new Color(0x7E / 255f, 0xC8 / 255f, 0xB8 / 255f);
            stateSuccess = new Color(0x4A / 255f, 0xA8 / 255f, 0x86 / 255f);
            stateWarning = new Color(0xE5 / 255f, 0xA3 / 255f, 0x4F / 255f);
            stateError = new Color(0xD4 / 255f, 0x55 / 255f, 0x6F / 255f);
            stateInfo = new Color(0x6D / 255f, 0x7D / 255f, 0xE7 / 255f);
            radiusSmall = 10f;
            radiusMedium = 18f;
            radiusLarge = 28f;
            radiusCard = 24f;
            radiusButton = 18f;
        }
    }
}
