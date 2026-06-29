using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ultrawide219
{
    /// <summary>
    /// Stretches uniform full-screen filters (darkens / fades / flashes / letterbox bars) to span the
    /// wide screen. All driven through <see cref="UltrawideState.FixFullScreen"/>, which is
    /// magic-value-guarded so it only touches sprites stuck at the old fixed-width constants.
    /// </summary>
    internal static class FilterStretchPatches
    {
        // In-game room effects: their full-screen sprite lives in the public sLeaser.sprites, re-scaled
        // every frame in DrawSprites — so we fix it in a DrawSprites postfix.
        private static readonly Type[] DrawSpriteEffects =
        {
            typeof(SunBlocker), typeof(GhostHunch), typeof(ElectricDeath), typeof(Ghost),
            typeof(AboveCloudsView), typeof(ProjectedScanLines), typeof(SuperStructureProjector),
            typeof(MoreSlugcats.GhostPing),
        };

        // Menu type -> full-screen darken/fade field name(s). Fixed in Menu.GrafUpdate (every frame, so
        // it survives whichever method set the scale). Magic-guard means already-correct ones are skipped.
        private static readonly Dictionary<Type, string[]> MenuDarkenFields = new Dictionary<Type, string[]>
        {
            { typeof(Menu.PauseMenu),            new[] { "blackSprite" } },
            { typeof(Menu.TutorialControlsPage), new[] { "blackSprite" } },
            { typeof(Menu.ArenaOverlay),         new[] { "blackSprite" } },
            { typeof(Menu.EndgameTokens),        new[] { "blackSprite" } },
            { typeof(Menu.EndCredits),           new[] { "blackSprite" } },
            { typeof(Menu.CustomEndGameScreen),  new[] { "blackSprite" } },
            { typeof(MoreSlugcats.ScribbleDreamScreen), new[] { "blackSprite" } },
            { typeof(Menu.Dialog),               new[] { "darkSprite" } },
            { typeof(Menu.OptionsMenu),          new[] { "darkSprite" } },
            { typeof(Menu.ModdingMenu),          new[] { "darkSprite" } },
            { typeof(Menu.MultiplayerMenu),      new[] { "darkSprite", "blackFadeSprite" } },
            { typeof(Menu.FastTravelScreen),     new[] { "fadeSprite" } },
            { typeof(Menu.BackupManager),        new[] { "darkSprite" } },
            { typeof(Menu.MenuDialogBox),        new[] { "darkSprite" } },
            { typeof(Menu.InputTesterHolder),    new[] { "darkSprite" } },
            { typeof(Menu.InputOptionsMenu),     new[] { "darkSprite" } },
            { typeof(MoreSlugcats.CollectionsMenu), new[] { "darkSprite" } },
            { typeof(MoreSlugcats.BackgroundOptionsMenu), new[] { "darkSprite" } },
        };

        private static readonly Dictionary<string, FieldInfo?> FieldCache = new Dictionary<string, FieldInfo?>();

        public static void Apply(Harmony harmony)
        {
            var drawSpritesPostfix = new HarmonyMethod(typeof(FilterStretchPatches), nameof(DrawSpritesPostfix));
            var sig = new[] { typeof(RoomCamera.SpriteLeaser), typeof(RoomCamera), typeof(float), typeof(Vector2) };
            foreach (var type in DrawSpriteEffects)
            {
                TryPatch(harmony, AccessTools.Method(type, "DrawSprites", sig), drawSpritesPostfix, $"{type.Name}.DrawSprites");
            }

            // HUD letterbox bars / fade / room-transition iris.
            TryPatch(harmony, AccessTools.Method(typeof(HUD.TextPrompt), "Draw", new[] { typeof(float) }),
                new HarmonyMethod(typeof(FilterStretchPatches), nameof(TextPromptDrawPostfix)), "HUD.TextPrompt.Draw");
            TryPatch(harmony, AccessTools.Method(typeof(HUD.RoomTransition), "Draw", new[] { typeof(float) }),
                new HarmonyMethod(typeof(FilterStretchPatches), nameof(RoomTransitionDrawPostfix)), "HUD.RoomTransition.Draw");

            // Menu darkens — one patch on the base GrafUpdate, dispatched per instance via the table.
            TryPatch(harmony, AccessTools.Method(typeof(Menu.Menu), "GrafUpdate", new[] { typeof(float) }),
                new HarmonyMethod(typeof(FilterStretchPatches), nameof(MenuGrafUpdatePostfix)), "Menu.Menu.GrafUpdate");
        }

        private static void TryPatch(Harmony harmony, MethodBase? method, HarmonyMethod postfix, string label)
        {
            if (method == null)
            {
                Plugin.Log?.LogWarning($"Filter patch target not found: {label}");
                return;
            }
            try { harmony.Patch(method, postfix: postfix); }
            catch (Exception e) { Plugin.Log?.LogError($"Filter patch failed for {label}: {e.Message}"); }
        }

        // ReSharper disable InconsistentNaming
        private static void DrawSpritesPostfix(RoomCamera.SpriteLeaser sLeaser)
        {
            if (!Plugin.StretchFilters.Value || sLeaser?.sprites == null) return;
            foreach (var s in sLeaser.sprites) UltrawideState.FixFullScreen(s);
        }

        private static void TextPromptDrawPostfix(HUD.TextPrompt __instance)
        {
            if (!Plugin.StretchFilters.Value) return;
            UltrawideState.FixFullScreen(GetField(__instance, "fullscreenFade"));
            if (GetRawField(__instance, "sprites") is FSprite[] bars)
                foreach (var s in bars) UltrawideState.FixFullScreen(s);
        }

        private static void RoomTransitionDrawPostfix(HUD.RoomTransition __instance)
        {
            if (!Plugin.StretchFilters.Value) return;
            UltrawideState.FixFullScreen(GetField(__instance, "sprite"));
        }

        private static void MenuGrafUpdatePostfix(Menu.Menu __instance)
        {
            if (!Plugin.StretchFilters.Value) return;
            if (!MenuDarkenFields.TryGetValue(__instance.GetType(), out var fields)) return;
            foreach (var name in fields) UltrawideState.FixFullScreen(GetField(__instance, name));
        }
        // ReSharper restore InconsistentNaming

        private static FSprite? GetField(object instance, string name) => GetRawField(instance, name) as FSprite;

        private static object? GetRawField(object instance, string name)
        {
            string key = instance.GetType().FullName + "#" + name;
            if (!FieldCache.TryGetValue(key, out var fi))
            {
                var t = instance.GetType();
                while (t != null && fi == null)
                {
                    fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    t = t.BaseType;
                }
                FieldCache[key] = fi;
            }
            return fi?.GetValue(instance);
        }
    }

    /// <summary>
    /// The handful of genuine wide-screen layout bugs (everything else is correct-by-construction via
    /// the engine's central <c>Menu.Init()</c> recentre, so it is deliberately left untouched).
    /// </summary>
    [HarmonyPatch(typeof(RWCustom.Custom), nameof(RWCustom.Custom.GetScreenOffsets))]
    internal static class GetScreenOffsetsPatch
    {
        // Vanilla returns the 1024-wide safe area [171,1195] for ANY width > 1366. Repair to the real
        // width so ManualDialog / MissionTooltip / MusicTrack* / FilterDialog use the full screen.
        // ReSharper disable once InconsistentNaming
        private static void Postfix(ref float[] __result)
        {
            if (Plugin.Enabled.Value && Plugin.MenuFixes.Value && UltrawideState.Width > 1366 && __result != null && __result.Length >= 2)
            {
                __result[0] = 0f;
                __result[1] = UltrawideState.Width;
            }
        }
    }

    /// <summary>
    /// <c>SlideShow</c> reassigns <c>scene</c> on every <c>NextScene()</c> but only the base
    /// <c>Menu.Init()</c> ever calls <c>HorizontalDisplace</c> (on the first, empty scene). So every
    /// real intro/outro/dream illustration is left-aligned on a wide screen. Recentre each new scene.
    /// </summary>
    [HarmonyPatch(typeof(Menu.SlideShow), "NextScene")]
    internal static class SlideShowNextScenePatch
    {
        private static readonly HashSet<Menu.MenuScene> Displaced = new HashSet<Menu.MenuScene>();

        // ReSharper disable once InconsistentNaming
        private static void Postfix(Menu.SlideShow __instance)
        {
            if (!Plugin.Enabled.Value || !Plugin.MenuFixes.Value || UltrawideState.Width <= 0) return;
            var scene = __instance.scene;
            if (scene == null || !Displaced.Add(scene)) return;
            scene.HorizontalDisplace(0f - Menu.Menu.HorizontalMoveToGetCentered(__instance.manager));
        }
    }
}
