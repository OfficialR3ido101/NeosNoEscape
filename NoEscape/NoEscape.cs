using CloudX.Shared;
using FrooxEngine;
using FrooxEngine.LogiX;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NoEscape
{
    public partial class NoEscape : NeosMod
    {
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> AvatarCloudVariablePathKey = new ModConfigurationKey<string>("AvatarCloudVariablePath", "Cloud Variable Path to check for Avatar enforcement. Default value is just a broadcast variable with the Transforming Davali.", () => "U-Banane9.EnforcedAvatar", valueValidator: CloudVariableHelper.IsValidPath);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> DashCloudVariablePathKey = new ModConfigurationKey<string>("DashCloudVariablePath", "Cloud Variable Path to check for opening the dash. Default value is just a broadcast variable with true.", () => "U-Banane9.AllowDash", valueValidator: CloudVariableHelper.IsValidPath);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableAvatarCloudVarKey = new ModConfigurationKey<bool>("EnableAvatarCloudVar", "Use the cloud variable listed here to control the used Avatar.", () => false);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableDashCloudVarKey = new ModConfigurationKey<bool>("EnableDashCloudVar", "Use the cloud variable listed here to control opening the dash.", () => false);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableEnforceAvatarKey = new ModConfigurationKey<bool>("EnableEnforceAvatar", "Enforce which Avatar is worn.", () => false);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableNoDashKey = new ModConfigurationKey<bool>("EnableNoDash", "Disable opening the dash on certain conditions.", () => false);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableNoEscapeKey = new ModConfigurationKey<bool>("EnableNoEscape", "Disable the respawn gesture based on certain conditions.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> EnableRespawnCloudVarKey = new ModConfigurationKey<bool>("EnableCloudVar", "Use the cloud variable listed here to control respawning.", () => false);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> EnforcedAvatarKey = new ModConfigurationKey<Uri>("EnforcedAvatar", "Record URI of the Avatar to enforce. Default value is the Transforming Davali.", () => new Uri("neosrec:///U-Banane9/R-8d9ddf58-8182-4a3c-9dca-07160c2ccc81"));

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> OverrideDashTimeKey = new ModConfigurationKey<float>("OverrideDashTime", "Allow opening the dash when the button is held this many seconds. Set to -1 to disable.", () => 8);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> OverrideDisconnectKey = new ModConfigurationKey<bool>("OverrideDisconnect", "Allow disconnecting regardless of other conditions.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> PanicChargeNotificationKey = new ModConfigurationKey<float>("PanicChargeNotification", "When to signal that respawning is disabled. Default respawn time is 2s. Set to -1 to disable.", () => 2);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> PanicChargeOverrideKey = new ModConfigurationKey<float>("PanicChargeOverride", "How long to hold to respawn regardless of other conditions. Default respawn time is 2s. Set to -1 to disable.", () => 10);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> RespawnCloudVariablePathKey = new ModConfigurationKey<string>("CloudVariablePath", "Cloud Variable Path to check for respawning. Default value is just a broadcast variable with false.", () => "U-Banane9.AllowRespawn", valueValidator: CloudVariableHelper.IsValidPath);

        private static bool cloudAllowRespawn = true;
        private static bool cloudValid = false;
        private static ModConfiguration Config;
        private static DateTime lastCloudUpdate = DateTime.MinValue;
        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosNoEscape";
        public override string Name => "NoEscape";
        public override string Version => "1.2.0";
        internal static bool DisconnectAllowed => Config.GetValue(OverrideDisconnectKey) || RespawnAllowed;
        internal static bool EnableAvatarCloudVar => Config.GetValue(EnableAvatarCloudVarKey);
        internal static bool EnableEnforceAvatar => Config.GetValue(EnableEnforceAvatarKey);
        internal static bool EnableNoDash => Config.GetValue(EnableNoDashKey);
        internal static bool EnableNoEscape => Config.GetValue(EnableNoEscapeKey);
        internal static Uri EnforcedAvatar => Config.GetValue(EnforcedAvatarKey);
        internal static bool OverrideDashEnabled => OverrideDashTime >= 0;
        internal static float OverrideDashTime => Config.GetValue(OverrideDashTimeKey);
        internal static float PanicChargeNotification => Config.GetValue(PanicChargeNotificationKey);
        internal static float PanicChargeOverride => Config.GetValue(PanicChargeOverrideKey);

        internal static bool RespawnAllowed => !EnableNoEscape
                                            || (EnableNoEscape && Config.GetValue(EnableRespawnCloudVarKey) && (!cloudValid || cloudAllowRespawn));

        internal static string RespawnCloudVariablePath => Config.GetValue(RespawnCloudVariablePathKey);

        public override void OnEngineInit()
        {
            Config = GetConfiguration();
            Config.OnThisConfigurationChanged += OnConfigurationChanged;
            Config.Save(true);

            updateRespawnCloudProxy();

            Harmony harmony = new Harmony($"{Author}.{Name}");
            harmony.PatchAll();
        }

        internal static void PollRespawnCloudVariable(Worker worker)
        {
            if (!cloudValid || (DateTime.UtcNow - lastCloudUpdate).TotalSeconds < 5)
                return;

            lastCloudUpdate = DateTime.UtcNow;
            worker.StartTask(async delegate ()
            {
                var proxy = worker.Cloud.Variables.RequestProxy(worker.LocalUser.UserID, Config.GetValue(RespawnCloudVariablePathKey));

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
            if (configurationChangedEvent.Key.Equals(RespawnCloudVariablePathKey))
                updateRespawnCloudProxy();
            else if (configurationChangedEvent.Key.Equals(EnforcedAvatarKey) || configurationChangedEvent.Key.Equals(AvatarCloudVariablePathKey))
            {
                if (EnableEnforceAvatar)
                {
                    if (EnableAvatarCloudVar)
                        PollAvatarCloudVariable();
                    else if (EnforcedAvatar != null)
                        Engine.Current.Cloud.Profile.ActiveAvatarUrl = EnforcedAvatar;
                }
            }
        }

        private void PollAvatarCloudVariable()
        {
            throw new NotImplementedException();
        }

        private void updateRespawnCloudProxy()
        {
            if (!CloudVariableHelper.IsValidPath(RespawnCloudVariablePath))
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
    }
}