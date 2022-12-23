using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoEscape
{
    [HarmonyPatch(typeof(ScreenModeController))]
    internal static class ScreenModeControllerPatches
    {
        private static double pressTime = 0;

        [HarmonyPostfix]
        [HarmonyPatch("OnInputUpdate")]
        private static void OnInputUpdatePostfix(SyncRef<UserspaceRadiantDash> ____dash, GlobalActions ____actions)
        {
            if (!NoEscape.EnableNoDash
             || ____dash.Target == null)
                return;

            if (____actions.ToggleDash.Pressed)
            {
                pressTime = ____dash.Time.WorldTime;
                ____dash.Target.Open = false;
            }
            else if (____actions.ToggleDash.Held && !____dash.Target.Open
                  && NoEscape.OverrideDashEnabled && ____dash.Time.WorldTime - pressTime >= NoEscape.OverrideDashTime)
            {
                ____dash.Target.ToggleOpen();
            }
        }
    }
}