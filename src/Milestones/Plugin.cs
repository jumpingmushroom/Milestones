using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Milestones.Core;

namespace Milestones
{
    /// <summary>
    /// Real progress for Valheim's achievements. Reads the same per-difficulty stat
    /// dictionaries the game's unlock check reads, and never writes game state: no stat, no
    /// achievement flag, and never Game.isModded. Purely client-side.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim.x86_64")]
    public sealed class MilestonesPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.jumpingmushroom.milestones";
        public const string PluginName = "Milestones";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;

        private static readonly HashSet<string> Warned = new HashSet<string>();
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            PluginConfig.Bind(base.Config);
            ConsoleCommands.Register();
            Pins.Init();
            UI.PinButton.Init();
            UI.ListMarks.Init();
            UI.TrackerHud.Init();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(MilestonesPlugin).Assembly);

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void Update()
        {
            Runtime.Tick();
        }

        private void OnDestroy()
        {
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }

        /// <summary>Log an exception once per key, so a broken patch can't flood the log every frame.</summary>
        internal static void WarnOnce(string key, Exception e)
        {
            if (Warned.Add(key))
                Log.LogWarning(key + ": " + e);
        }
    }
}
