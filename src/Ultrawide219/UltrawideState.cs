using UnityEngine;

namespace Ultrawide219
{
    /// <summary>21:9 resolution presets. The internal render is always 768px tall (the engine's
    /// fixed UI coordinate height); only the width changes to the chosen aspect.</summary>
    public enum ResolutionPreset
    {
        /// <summary>Don't manage resolution; only apply UI fixes to whatever resolution is active.</summary>
        Off,

        /// <summary>True 21:9 (2.333:1) → 1792×768.</summary>
        TrueUltrawide_1792x768,

        /// <summary>For a 2560×1080 (UW-FHD) monitor → 1820×768 internal.</summary>
        Monitor_2560x1080,

        /// <summary>For a 3440×1440 (UW-QHD) monitor → 1835×768 internal.</summary>
        Monitor_3440x1440,

        /// <summary>Super-ultrawide 32:9 (3.556:1) → 2731×768.</summary>
        SuperUltrawide_32x9,

        /// <summary>Compute the width from the actual display aspect at launch.</summary>
        MatchDisplay,
    }

    /// <summary>
    /// Central state: the active internal width and the shared full-screen-fill fixer. The render
    /// height is always <see cref="RenderHeight"/> (768) so the engine's UI coordinate space — which
    /// centres everything on (1366/2, 768/2) and drives all menu/HUD layout off
    /// <c>Options.ScreenSize</c> — stays valid; we only widen it.
    /// </summary>
    public static class UltrawideState
    {
        public const int RenderHeight = 768;

        /// <summary>The active internal render width (e.g. 1835), or 0 when the mod manages no resolution.</summary>
        public static int Width { get; internal set; }

        /// <summary>The active internal width as a float, for sprite math.</summary>
        public static float ScreenW => Width;

        /// <summary>Resolves a preset to an internal width at the fixed 768px height.</summary>
        public static int WidthForPreset(ResolutionPreset preset)
        {
            switch (preset)
            {
                case ResolutionPreset.TrueUltrawide_1792x768: return 1792;
                case ResolutionPreset.Monitor_2560x1080: return AspectWidth(2560, 1080); // 1820
                case ResolutionPreset.Monitor_3440x1440: return AspectWidth(3440, 1440); // 1835
                case ResolutionPreset.SuperUltrawide_32x9: return AspectWidth(32, 9);    // 2731
                case ResolutionPreset.MatchDisplay:
                    var r = Screen.currentResolution;
                    return (r.width > 0 && r.height > 0) ? AspectWidth(r.width, r.height) : 1792;
                default: return 0;
            }
        }

        /// <summary>Internal width that matches a monitor's aspect ratio at the fixed 768px height.</summary>
        private static int AspectWidth(int monitorW, int monitorH) =>
            Mathf.RoundToInt(RenderHeight * (monitorW / (float)monitorH));

        /// <summary>
        /// If <paramref name="sprite"/> is a full-screen uniform fill stuck at a known fixed-width
        /// constant, rescale its width to span the wide screen. Idempotent and magic-value-guarded so
        /// it only ever touches the broken fixed-width fills, never legitimately-sized sprites:
        /// <list type="bullet">
        /// <item><c>"Futile_White"</c> fades (16px/unit) use <c>scaleX ≈ 87.5</c> (=1400/16) → <c>(W+2)/16</c>.</item>
        /// <item><c>"pixel"</c> fills (1px/unit) use <c>scaleX ≈ 1366..1500</c> → <c>W+2</c>.</item>
        /// </list>
        /// Height (<c>scaleY</c>) is left untouched — it is correct at the fixed 768px render height.
        /// </summary>
        public static void FixFullScreen(FSprite? sprite)
        {
            if (sprite == null || Width <= 0)
            {
                return;
            }

            float sx = sprite.scaleX;
            if (sx > 80f && sx < 95f)
            {
                sprite.scaleX = (ScreenW + 2f) / 16f; // Futile_White units
            }
            else if (sx >= 1300f && sx <= 1700f)
            {
                sprite.scaleX = ScreenW + 2f; // pixel units
            }
        }
    }
}
