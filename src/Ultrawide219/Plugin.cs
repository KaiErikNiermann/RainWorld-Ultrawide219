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
    /// Renders Rain World at a wide internal resolution (true 21:9 by default) instead of the
    /// stock 16:9 presets, revealing more of each room horizontally with no pixel stretching.
    ///
    /// The engine already renders at a fixed 768px height with an arbitrary width
    /// (every stock preset is <c>NxN768</c>; <see cref="Futile"/> reallocates its render target via
    /// <c>UpdateScreenWidth</c>). The devs simply never exposed a wide preset, and the only
    /// genuinely fixed-size internals are the 1400x800 effect textures inside <c>RoomCamera</c>.
    /// This plugin forces a wide <c>Options.ScreenSize</c> and scales that 1400-wide space to match.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "kai.ultrawide219";
        public const string Name = "Ultrawide 21:9";
        public const string Version = "0.1.0";

        /// <summary>The engine's hardcoded render height. Only the width is variable.</summary>
        public const int RenderHeight = 768;

        /// <summary>Width of the original fixed internal render space inside <c>RoomCamera</c>.</summary>
        public const int StockInternalWidth = 1400;

        internal static ManualLogSource? Log { get; private set; }

        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<int> TargetWidth = null!;

        /// <summary>
        /// Width of the <c>RoomCamera</c> internal render space, sized to cover the viewport.
        /// Never below the stock 1400 (narrower would clip effects on standard resolutions).
        /// </summary>
        internal static int InternalWidth =>
            Mathf.Max(StockInternalWidth, Enabled.Value ? TargetWidth.Value : StockInternalWidth);

        /// <summary>The forced screen size, or <c>null</c> when the mod is disabled.</summary>
        internal static Vector2? ForcedScreenSize =>
            Enabled.Value ? new Vector2(TargetWidth.Value, RenderHeight) : (Vector2?)null;

        private bool _applied;

        public void OnEnable()
        {
            Log = Logger;

            Enabled = Config.Bind(
                "General", "Enabled", true,
                "Master switch. When off, the game reverts to its stock 16:9 resolution behaviour.");

            TargetWidth = Config.Bind(
                "Display", "Width", 1792,
                new ConfigDescription(
                    "Internal render width at the engine's fixed 768px height.\n" +
                    "  1792 = true 21:9 (1792 / 768 = 2.333)\n" +
                    "  2560 ~= 32:9 super-ultrawide\n" +
                    "  1366 = stock 16:9 (effectively a no-op)\n" +
                    "Pick the width that matches your monitor's aspect at 768px tall: width = round(768 * AR).",
                    new AcceptableValueRange<int>(1366, 4096)));

            try
            {
                // get_ScreenSize: report a wide size to the whole pipeline (Screen, Futile, RoomCamera,
                // shaders). Harmony postfix is the most robust hook for a property getter and, crucially,
                // it does NOT mutate the saved resolution index, so uninstalling the mod is safe.
                var harmony = new Harmony(Guid);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                // The 1400-wide internal space: rewrite the literal in the RoomCamera ctor textures and
                // in the hDisplace getter so the wider viewport is fully covered.
                RoomCameraIL.Apply();

                Log.LogInfo($"Hooks installed. Target render {TargetWidth.Value}x{RenderHeight} " +
                            $"(AR {TargetWidth.Value / (float)RenderHeight:0.000}), internal width {InternalWidth}.");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to install hooks: {e}");
            }
        }

        /// <summary>
        /// One-shot apply: once the game and Futile exist, push the wide width into Futile's render
        /// target and the OS window. <c>Options.Update</c> would eventually do this too via the forced
        /// ScreenSize, but applying it ourselves guarantees it happens immediately and deterministically.
        /// </summary>
        public void Update()
        {
            if (_applied || !Enabled.Value)
            {
                return;
            }

            if (Futile.instance == null)
            {
                return;
            }

            var rainWorld = UnityEngine.Object.FindObjectOfType<RainWorld>();
            if (rainWorld == null || rainWorld.options == null)
            {
                return;
            }

            _applied = true;
            int width = TargetWidth.Value;

            try
            {
                Futile.instance.UpdateScreenWidth(width);
                Screen.SetResolution(width, RenderHeight, Screen.fullScreen);
                Log?.LogInfo($"Applied {width}x{RenderHeight} (fullScreen={Screen.fullScreen}).");
            }
            catch (Exception e)
            {
                Log?.LogError($"Failed to apply resolution: {e}");
            }
        }
    }

    /// <summary>
    /// Forces <c>Options.ScreenSize</c> to the wide target. Every screen-size consumer reads through
    /// this getter (<c>RainWorld.screenSize</c>, the window setup in <c>Options.Update</c>, the room
    /// camera's <c>sSize</c>, and the screen-size shader globals), so a single override drives them all.
    /// </summary>
    [HarmonyPatch(typeof(Options), nameof(Options.ScreenSize), MethodType.Getter)]
    internal static class OptionsPatch
    {
        // ReSharper disable once InconsistentNaming
        private static void Postfix(ref Vector2 __result)
        {
            if (Plugin.ForcedScreenSize is Vector2 forced)
            {
                __result = forced;
            }
        }
    }

    /// <summary>
    /// Recomputes <c>RoomCamera.hDisplace</c> for the widened internal render space. The stock getter
    /// is <c>(1400 - sSize.x) / 2 - 8</c>, which centres the viewport inside the 1400-wide buffer;
    /// with the buffer widened to <see cref="Plugin.InternalWidth"/> the centring offset must use the
    /// same width or the level graphic drifts horizontally. Done via Harmony because HookGen emits no
    /// IL hook for this property getter.
    /// </summary>
    [HarmonyPatch(typeof(RoomCamera), nameof(RoomCamera.hDisplace), MethodType.Getter)]
    internal static class RoomCameraHDisplacePatch
    {
        // ReSharper disable once InconsistentNaming
        private static void Postfix(RoomCamera __instance, ref float __result)
        {
            if (Plugin.Enabled.Value)
            {
                __result = (Plugin.InternalWidth - __instance.sSize.x) / 2f - 8f;
            }
        }
    }
}
