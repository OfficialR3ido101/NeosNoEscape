using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using SkyFrost.Base;
using SkyFrost;

namespace NoEscape
{
    [HarmonyPatch(typeof(InventoryBrowser))]
    internal static class InventoryBrowserPatches
    {
        [HarmonyTranspiler]
        [HarmonyPatch("OnItemSelected")]
        private static IEnumerable<CodeInstruction> OnItemSelectedTranspiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var getBoltMethod = typeof(OfficialAssets.Common.Icons).GetProperty("Bolt", AccessTools.allDeclared).GetMethod;
            var itemField = typeof(InventoryItemUI).GetField("Item", AccessTools.allDeclared);
            var getUrlMethod = typeof(Record).GetProperty("URL", AccessTools.allDeclared).GetMethod;

            var showAvatarButtonsMethod = typeof(InventoryBrowserPatches).GetMethod(nameof(ShowAvatarButtons), AccessTools.allDeclared);
            var showSetDefaultButtonMethod = typeof(InventoryBrowserPatches).GetMethod(nameof(ShowSetDefaultButton), AccessTools.allDeclared);

            var instructions = new List<CodeInstruction>(codeInstructions);

            var getBoltIndex = instructions.FindIndex(instruction => instruction.Calls(getBoltMethod));
            var jumpIndex = instructions.FindLastIndex(getBoltIndex, instruction => instruction.opcode == OpCodes.Bne_Un_S);

            instructions.Insert(jumpIndex + 1, new CodeInstruction(OpCodes.Ldloc_0));
            instructions.Insert(jumpIndex + 2, new CodeInstruction(OpCodes.Ldfld, itemField));
            instructions.Insert(jumpIndex + 3, new CodeInstruction(OpCodes.Callvirt, getUrlMethod));
            instructions.Insert(jumpIndex + 4, new CodeInstruction(OpCodes.Call, showAvatarButtonsMethod));
            instructions.Insert(jumpIndex + 5, new CodeInstruction(OpCodes.Brfalse_S, instructions[jumpIndex].operand));

            var buttonPopIndex = instructions.FindIndex(jumpIndex + 6, instruction => instruction.opcode == OpCodes.Pop);

            instructions.Insert(buttonPopIndex + 1, new CodeInstruction(OpCodes.Call, showSetDefaultButtonMethod));
            instructions.Insert(buttonPopIndex + 2, new CodeInstruction(OpCodes.Brfalse_S, instructions[jumpIndex].operand));

            return instructions;
        }

        private static bool ShowAvatarButtons(Uri recordUri)
        {
            return !NoEscape.EnableEnforceAvatar || recordUri == NoEscape.EnforcedAvatar;
        }

        private static bool ShowSetDefaultButton()
        {
            return !NoEscape.EnableEnforceAvatar;
        }
    }
}