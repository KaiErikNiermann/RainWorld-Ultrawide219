using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Ultrawide219
{
    /// <summary>
    /// Ultrawide 21:9 — UI companion to SBCameraScroll.
    ///
    /// SBCameraScroll widens the gameplay camera (stitching room textures); this mod fixes everything
    /// AROUND the gameplay so 21:9 is seamless: it forces a 768-tall internal render at a 21:9 width
    /// (keeping the engine's UI coordinate space valid), stretches uniform full-screen filters
    /// (darkens / fades / flashes) to span the wide screen, and patches the handful of genuine
    /// wide-screen layout bugs (Custom.GetScreenOffsets, SlideShow scene centring, end-screen buttons).
    /// Hand-drawn art stays centred + pillarboxed by design.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "kai.ultrawide219";
        public const string Name = "Ultrawide 21:9 UI";
        public const string Version = "0.2.0";

        internal static ManualLogSource? Log { get; private set; }

        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<ResolutionPreset> Preset = null!;
        internal static ConfigEntry<bool> StretchFilters = null!;
        internal static ConfigEntry<bool> MenuFixes = null!;

        private bool _applied;

        public void OnEnable()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch for the whole mod.");
            Preset = Config.Bind("Display", "Preset", ResolutionPreset.Monitor_3440x1440,
                "21:9 resolution preset. The internal render is always 768px tall (keeps the UI coordinate " +
                "space valid); only the width changes. Pick the one matching your monitor. " +
                "'Off' applies the UI fixes to whatever resolution is already active (e.g. SBCameraScroll's).");
            StretchFilters = Config.Bind("Fixes", "StretchFilters", true,
                "Stretch uniform full-screen filters (pause darken, fades, ghost/electric flashes, " +
                "letterbox bars) to cover the full width.");
            MenuFixes = Config.Bind("Fixes", "MenuLayout", true,
                "Fix genuine wide-screen menu bugs (safe-area offsets, slideshow centring, end-screen buttons).");

            if (!Enabled.Value)
            {
                Log.LogInfo("Disabled via config.");
                return;
            }

            UltrawideState.Width = UltrawideState.WidthForPreset(Preset.Value);

            try
            {
                var harmony = new Harmony(Guid);
                harmony.PatchAll(Assembly.GetExecutingAssembly()); // attribute patches (ScreenSize, menu bugs)
                FilterStretchPatches.Apply(harmony);               // programmatic table patches (filters)
                Log.LogInfo($"Hooks installed. Preset={Preset.Value} → internal {UltrawideState.Width}x{UltrawideState.RenderHeight} " +
                            $"(AR {UltrawideState.Width / (float)UltrawideState.RenderHeight:0.000}).");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to install hooks: {e}");
            }
        }

        /// <summary>One-shot: push the wide width into Futile + the OS window once the game is live.</summary>
        public void Update()
        {
            if (_applied || !Enabled.Value || UltrawideState.Width <= 0)
            {
                return;
            }
            if (Futile.instance == null || UnityEngine.Object.FindObjectOfType<RainWorld>()?.options == null)
            {
                return;
            }

            _applied = true;
            try
            {
                Futile.instance.UpdateScreenWidth(UltrawideState.Width);
                Screen.SetResolution(UltrawideState.Width, UltrawideState.RenderHeight, Screen.fullScreen);
                Log?.LogInfo($"Applied {UltrawideState.Width}x{UltrawideState.RenderHeight} (fullScreen={Screen.fullScreen}).");
            }
            catch (Exception e)
            {
                Log?.LogError($"Apply failed: {e}");
            }
        }
    }

    /// <summary>
    /// Forces <c>Options.ScreenSize</c> to (presetWidth, 768). Every menu/HUD layout formula and the
    /// SBCameraScroll camera read through this, so one override drives them all to the wide width while
    /// keeping the height — and therefore all the <c>(…, 384)</c> vertical centring — correct.
    /// </summary>
    [HarmonyPatch(typeof(Options), nameof(Options.ScreenSize), MethodType.Getter)]
    internal static class ScreenSizePatch
    {
        // ReSharper disable once InconsistentNaming
        private static void Postfix(ref Vector2 __result)
        {
            if (Plugin.Enabled.Value && UltrawideState.Width > 0)
            {
                __result = new Vector2(UltrawideState.Width, UltrawideState.RenderHeight);
            }
        }
    }
}
