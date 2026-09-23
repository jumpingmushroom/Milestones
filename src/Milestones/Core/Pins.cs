using System;
using System.Collections.Generic;
using Milestones.Core.Model;

namespace Milestones.Core
{
    /// <summary>
    /// Pins live in Player.m_customData["milestones.pins"], which the game saves inside the
    /// character file. The in-memory list is keyed by profile, so a respawn after death (a new
    /// Player loaded from the last save) keeps pins changed since that save.
    /// </summary>
    internal static class Pins
    {
        private const string Key = "milestones.pins";
        private const float UnpinDelay = 10f;

        private static PinList _list = new PinList();
        private static string _profile;
        private static Player _owner;
        private static readonly Dictionary<string, float> UnpinAt = new Dictionary<string, float>();

        public static event Action Changed;

        public static IReadOnlyList<string> Ids => _list.Ids;
        public static int Count => _list.Count;
        public static bool IsFull => _list.IsFull;

        static Pins()
        {
            Runtime.PlayerSpawned += OnPlayerSpawned;
            StatEvents.Unlocked += OnUnlocked;
        }

        public static void Init()
        {
            // Forces the static constructor so the event subscriptions exist before the first spawn.
        }

        private static void OnPlayerSpawned(Player p)
        {
            string profile = Game.instance.GetPlayerProfile().GetFilename();
            if (profile != _profile)
            {
                string raw;
                p.m_customData.TryGetValue(Key, out raw);
                _list = PinList.Parse(raw);
                _profile = profile;
                UnpinAt.Clear();
            }
            _owner = p;
            _list.RemoveWhere(id => AchievementReader.ById(id) == null);
            Save();
            Raise();
        }

        public static bool Contains(string id)
        {
            return _list.Contains(id);
        }

        public static bool Toggle(Achievement a)
        {
            return Contains(a.m_id) ? Unpin(a.m_id) : Pin(a.m_id);
        }

        public static bool Pin(string id)
        {
            if (!_list.Add(id))
                return false;
            Save();
            Raise();
            return true;
        }

        public static bool Unpin(string id)
        {
            UnpinAt.Remove(id);
            if (!_list.Remove(id))
                return false;
            Save();
            Raise();
            return true;
        }

        public static void Tick(float now)
        {
            if (UnpinAt.Count == 0)
                return;
            string due = null;
            foreach (KeyValuePair<string, float> kv in UnpinAt)
            {
                if (now >= kv.Value)
                {
                    due = kv.Key;
                    break;
                }
            }
            if (due != null)
                Unpin(due);
        }

        private static void OnUnlocked(Achievement a)
        {
            if (PluginConfig.AutoUnpinCompleted.Value && Contains(a.m_id))
                UnpinAt[a.m_id] = Runtime.Now + UnpinDelay;
        }

        private static void Save()
        {
            if (_owner == null)
                return;
            if (_list.Count == 0)
                _owner.m_customData.Remove(Key);
            else
                _owner.m_customData[Key] = _list.Serialize();
        }

        private static void Raise()
        {
            if (PluginConfig.Verbose.Value)
                MilestonesPlugin.Log.LogInfo("Milestones: pins = " + _list.Serialize());
            if (Changed != null)
                Changed();
        }
    }
}
