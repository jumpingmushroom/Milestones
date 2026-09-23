using System;
using UnityEngine;

namespace Milestones.Core
{
    /// <summary>
    /// Per-frame driver, from MilestonesPlugin.Update. Notices a new local player, drains stat
    /// marks at most four times a second, and does a full refresh every ten seconds for stats the
    /// patches don't see. Idle frames cost two float compares.
    /// </summary>
    internal static class Runtime
    {
        private const float DirtyInterval = 0.25f;
        private const float SweepInterval = 10f;

        private static Player _player;
        private static float _nextDirty;
        private static float _nextSweep;

        public static event Action<Player> PlayerSpawned;
        public static event Action Ticked;

        public static float Now => Time.unscaledTime;

        public static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p != _player)
            {
                _player = p;
                ProgressCache.Clear();
                if (p != null && Achievements.m_instance != null)
                {
                    MilestonesPlugin.Log.LogInfo("Milestones: player spawned; Game.isModded = " + Game.isModded);
                    if (PlayerSpawned != null)
                        PlayerSpawned(p);
                }
            }
            if (_player == null || !PluginConfig.Enabled.Value)
                return;

            float now = Now;
            if (now >= _nextDirty)
            {
                _nextDirty = now + DirtyInterval;
                if (StatEvents.Any)
                    ProgressCache.ProcessDirty();
            }
            if (now >= _nextSweep)
            {
                _nextSweep = now + SweepInterval;
                ProgressCache.RefreshAll();
            }
            if (Ticked != null)
                Ticked();
        }
    }
}
