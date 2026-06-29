using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Ultrawide219
{
    /// <summary>
    /// Rewrites the hardcoded <c>1400</c>-wide internal render space in <c>RoomCamera</c> to the
    /// mod's wider <see cref="Plugin.InternalWidth"/>. Without this the lightmap / snow effect
    /// textures and the camera's horizontal centring (<c>hDisplace</c>) would only cover the stock
    /// 16:9 region, leaving the extra ultrawide columns unlit / misaligned.
    /// </summary>
    internal static class RoomCameraIL
    {
        public static void Apply()
        {
            // The ctor builds three `new RenderTexture(1400, 800, ...)` effect buffers (lightmap, snow,
            // and a temporary). hDisplace is handled separately by a Harmony getter postfix because
            // HookGen does not emit an IL hook for that property.
            IL.RoomCamera.ctor += PatchInternalWidth;
        }

        /// <summary>
        /// Replaces every literal <c>1400</c> (the stock internal width) with
        /// <see cref="Plugin.InternalWidth"/> — the <c>ldc.i4 1400</c> width operands of the ctor's
        /// <c>new RenderTexture(1400, 800, ...)</c> calls. The <c>800</c> height literal is left untouched.
        /// </summary>
        private static void PatchInternalWidth(ILContext il)
        {
            var c = new ILCursor(il);
            int hits = 0;

            while (c.TryGotoNext(MoveType.Before, i => i.MatchLdcI4(Plugin.StockInternalWidth)))
            {
                c.Next!.Operand = Plugin.InternalWidth;
                c.Index++;
                hits++;
            }

            Plugin.Log?.LogInfo(
                $"IL patch {il.Method.Name}: replaced {hits} '{Plugin.StockInternalWidth}' " +
                $"width literal(s) with {Plugin.InternalWidth}.");
        }
    }
}
