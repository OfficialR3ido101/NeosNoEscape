using BaseX;
using CloudX.Shared;
using FrooxEngine;
using FrooxEngine.LogiX;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NoEscape
{
    public class NoEscape : NeosMod
    {
        private static bool cloudAllowRespawn = true;
        private static bool cloudValid = false;

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> EnableNoEscape = new ModConfigurationKey<bool>("EnableNoEscape", "Disable the respawn gesture based on certain conditions.", () => true);

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> OverrideDisconnect = new ModConfigurationKey<bool>("OverrideDisconnect", "Allow disconnecting regardless of other conditions.", () => true);

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<float> PanicChargeNotification = new ModConfigurationKey<float>("PanicChargeNotification", "When to signal that respawning is disabled. Default respawn time is 2s. Set to -1 to disable.", () => 2);

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<float> PanicChargeOverride = new ModConfigurationKey<float>("PanicChargeOverride", "How long to hold to respawn regardless of other conditions. Default respawn time is 2s. Set to -1 to disable.", () => 10);

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> EnableCloudVar = new ModConfigurationKey<bool>("EnableCloudVar", "Use the cloud variable listed here to control respawning.", () => false);

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<string> CloudVariablePath = new ModConfigurationKey<string>("CloudVariablePath", "Cloud Variable Path to check for respawning. Default value is just a broadcast variable with false.", () => "U-Banane9.AllowRespawn");

        private static ModConfiguration Config;

        private static DateTime lastCloudUpdate = DateTime.MinValue;

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosNoEscape";
        public override string Name => "NoEscape";
        public override string Version => "1.1.0";

        private static bool DisconnectAllowed => Config.GetValue(OverrideDisconnect) || RespawnAllowed;

        private static bool RespawnAllowed => !Config.GetValue(EnableNoEscape)
                                           || (Config.GetValue(EnableNoEscape) && Config.GetValue(EnableCloudVar) && (!cloudValid || cloudAllowRespawn));

        public override void OnEngineInit()
        {
            Config = GetConfiguration();
            Config.OnThisConfigurationChanged += OnConfigurationChanged;
            Config.Save(true);

            updateCloudProxy();

            Harmony harmony = new Harmony($"{Author}.{Name}");
            harmony.PatchAll();
        }

        private static void pollCloudVariable(Worker worker)
        {
            if (!cloudValid || (DateTime.UtcNow - lastCloudUpdate).TotalSeconds < 5)
                return;

            lastCloudUpdate = DateTime.UtcNow;
            worker.StartTask(async delegate ()
            {
                var proxy = worker.Cloud.Variables.RequestProxy(worker.LocalUser.UserID, Config.GetValue(CloudVariablePath));

                await proxy.Refresh();

                if (proxy.State == CloudVariableState.Invalid || proxy.State == CloudVariableState.Unregistered)
                {
                    Error("Failed to poll cloud variable.");
                    cloudAllowRespawn = true;
                }
                else
                {
                    cloudAllowRespawn = proxy.ReadValue<bool>();
                    Msg("Polled cloud variable for " + cloudAllowRespawn);
                    lastCloudUpdate = DateTime.UtcNow;
                }
            });
        }

        private void OnConfigurationChanged(ConfigurationChangedEvent configurationChangedEvent)
        {
            if (!configurationChangedEvent.Key.Equals(CloudVariablePath))
                return;

            updateCloudProxy();
        }

        private void updateCloudProxy()
        {
            if (!CloudVariableHelper.IsValidPath(Config.GetValue(CloudVariablePath)))
            {
                Error("Invalid Cloud Variable Path");
                cloudValid = false;
                cloudAllowRespawn = true;
                return;
            }
            else
            {
                Msg("Valid Cloud Variable Path");
                cloudValid = true;
                cloudAllowRespawn = false;
            }
        }

        [HarmonyPatch(typeof(CommonTool))]
        private static class CommonToolPatch
        {
            private static readonly ConditionalWeakTable<CommonTool, ToolData> toolDataTable = new ConditionalWeakTable<CommonTool, ToolData>();

            [HarmonyPrefix]
            [HarmonyPatch("HoldMenu")]
            private static void HoldMenuPrefix(CommonTool __instance, ref float ___panicCharge)
            {
                pollCloudVariable(__instance);
                var toolData = toolDataTable.GetOrCreateValue(__instance);

                var panicCharge = toolData.PanicCharge;
                toolData.PanicCharge += __instance.Time.Delta;
                // Allow changing to disconnect instead of respawn only until the regular 2s are over.
                toolData.IsDisconnect = !(__instance.Inputs.Grab.Held || __instance.OtherTool.Inputs.Grab.Held);

                if (!isRespawnGesture(__instance) || RespawnAllowed || (DisconnectAllowed && toolData.IsDisconnect))
                {
                    if (toolData.PanicCharge >= 2)
                        ___panicCharge = toolData.PanicCharge;

                    return;
                }

                // Let it activate when held long enough
                if (Config.GetValue(PanicChargeOverride) > 0 && toolData.PanicCharge >= Config.GetValue(PanicChargeOverride))
                    ___panicCharge = panicCharge;
                // Disable notification of it not going to work
                else if (Config.GetValue(PanicChargeNotification) < 0)
                    ___panicCharge = MathX.Min(1.9f - __instance.Time.Delta, panicCharge);
                // Stop the vibration to signal it won't work when held long enough
                else if (Config.GetValue(PanicChargeNotification) >= 0 && toolData.PanicCharge >= Config.GetValue(PanicChargeNotification))
                    ___panicCharge = 0;

                if (___panicCharge >= 2)
                    toolData.PanicCharge = float.MinValue;
            }

            private static bool isRespawnGesture(CommonTool tool)
            {
                return tool.World == Userspace.UserspaceWorld
                    && tool.IsNearHead && tool.OtherTool != null && tool.Side.Value == Chirality.Left
                    && tool.OtherTool.Inputs.Menu.Held && tool.OtherTool.IsNearHead;
            }

            [HarmonyPostfix]
            [HarmonyPatch("StartMenu")]
            private static void StartMenuPostfix(CommonTool __instance)
            {
                toolDataTable.GetOrCreateValue(__instance).PanicCharge = 0;
            }

            private class ToolData
            {
                private bool isDisconnect;

                public bool IsDisconnect
                {
                    get => isDisconnect;
                    set => isDisconnect = PanicCharge < 2 ? value : isDisconnect;
                }

                public float PanicCharge { get; set; } = 0;
            }
        }
    }
}