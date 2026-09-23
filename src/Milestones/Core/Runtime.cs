using System;
using UnityEngine;

namespace Milestones.Core
{
    /// <summary>
    /// Per-frame driver, from MilestonesPlugin.Update. Notices a new local player, drains stat
    /// marks at most four times a second, and does a full refresh every ten seconds for stats the
    /// patches don't see. Idle frames do a few compares and the Ticked handlers; recomputes
    /// happen only when stats changed.
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
            try
            {
                Player p = Player.m_localPlayer;
                // A null player always commits; a new one only once Achievements exists, so its
                // PlayerSpawned handlers have somewhere to read from. Otherwise retry next frame.
                if (p != _player && (p == null || Achievements.m_instance != null))
                {
                    _player = p;
                    ProgressCache.Clear();
                    if (p != null)
                    {
                        MilestonesPlugin.Log.LogInfo("Milestones: player spawned; Game.isModded = " + Game.isModded);
                        if (PlayerSpawned != null)
                            PlayerSpawned(p);
                    }
                }
                if (_player == null || !PluginConfig.Enabled.Value)
                    return;

                float now = Now;
                Pins.Tick(now);
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
            catch (Exception e)
            {
                MilestonesPlugin.WarnOnce("tick", e);
            }
        }
    }
}
