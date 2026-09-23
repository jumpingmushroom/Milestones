using System.Collections.Generic;
using Milestones.Core;
using Milestones.Core.Model;

namespace Milestones.UI
{
    /// <summary>
    /// Snapshots every achievement when a player spawns, so a login never replays old progress,
    /// then turns each ProgressCache change into at most one line through ToastDiff.
    /// </summary>
    internal static class Toasts
    {
        private static readonly Dictionary<string, Snapshot> Seen = new Dictionary<string, Snapshot>();
        private static ToastRules _rules;

        public static void Init()
        {
            Runtime.PlayerSpawned += p => Reset();
            ProgressCache.Changed += OnChanged;
            PluginConfig.Thresholds.SettingChanged += (s, e) => _rules = null;
            PluginConfig.PinnedToasts.SettingChanged += (s, e) => _rules = null;
            PluginConfig.UnpinnedToasts.SettingChanged += (s, e) => _rules = null;
            // Recomputing under a new mode changes every value; re-snapshot first so the
            // switch itself doesn't read as progress and toast.
            PluginConfig.Overall.SettingChanged += (s, e) => { ProgressCache.Clear(); Reset(); };
        }

        private static ToastRules Rules
        {
            get
            {
                if (_rules == null)
                {
                    _rules = new ToastRules
                    {
                        Pinned = PluginConfig.PinnedToasts.Value,
                        Unpinned = PluginConfig.UnpinnedToasts.Value,
                        Thresholds = ToastRules.ParseThresholds(PluginConfig.Thresholds.Value)
                    };
                }
                return _rules;
            }
        }

        private static void Reset()
        {
            Seen.Clear();
            foreach (Achievement a in AchievementReader.All())
            {
                AchievementProgress p = ProgressCache.Get(a);
                if (p != null)
                    Seen[a.m_id] = Snapshot.Take(p);
            }
        }

        private static void OnChanged(Achievement a, AchievementProgress now)
        {
            Snapshot before;
            Seen.TryGetValue(a.m_id, out before);
            Seen[a.m_id] = Snapshot.Take(now);
            if (before == null || !PluginConfig.Enabled.Value)
                return;
            string line = ToastDiff.Diff(before, now, AchievementReader.Name(a), Pins.Contains(a.m_id), Rules);
            if (line == null || MessageHud.instance == null)
                return;
            MessageHud.MessageType where = PluginConfig.ToastAt.Value == ToastPosition.Center
                ? MessageHud.MessageType.Center
                : MessageHud.MessageType.TopLeft;
            MessageHud.instance.ShowMessage(where, line);
            if (PluginConfig.Verbose.Value)
                MilestonesPlugin.Log.LogInfo("Milestones: toast: " + line);
        }
    }
}
