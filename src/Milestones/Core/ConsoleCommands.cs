using Milestones.Core.Model;
using UnityEngine;

namespace Milestones.Core
{
    /// <summary>
    /// "milestones" lists every achievement with its progress; "milestones &lt;id|name&gt;" prints
    /// each objective. Output is mirrored to the BepInEx log for build/logs.sh.
    /// </summary>
    internal static class ConsoleCommands
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("milestones", "Milestones: progress (<id|name> | pin <id> | unpin <id> | ui)",
                delegate (Terminal.ConsoleEventArgs args)
                {
                    if (Achievements.m_instance == null || Game.instance == null)
                    {
                        Say(args.Context, "Milestones: not in a game.");
                        return;
                    }
                    string sub = args.Length > 1 ? args[1] : "";
                    string rest = args.Length > 2 ? string.Join(" ", args.Args, 2, args.Length - 2) : "";
                    switch (sub.ToLowerInvariant())
                    {
                        case "":
                            Summary(args.Context);
                            break;
                        case "ui":
                            DumpUi(args.Context);
                            break;
                        case "pin":
                        case "unpin":
                            PinCommand(args.Context, sub.ToLowerInvariant() == "pin", rest);
                            break;
                        default:
                            Detail(args.Context, string.Join(" ", args.Args, 1, args.Length - 1));
                            break;
                    }
                });
        }

        internal static void Say(Terminal ctx, string line)
        {
            if (ctx != null)
                ctx.AddString(line);
            MilestonesPlugin.Log.LogInfo(line);
        }

        private static void PinCommand(Terminal ctx, bool pin, string query)
        {
            Achievement a = AchievementReader.Find(query);
            if (a == null)
            {
                Say(ctx, "Milestones: no achievement matches \"" + query + "\".");
                return;
            }
            bool ok = pin ? Pins.Pin(a.m_id) : Pins.Unpin(a.m_id);
            Say(ctx, "Milestones: " + (ok ? (pin ? "pinned " : "unpinned ") : "unchanged ") + a.m_id +
                " (" + Pins.Count + "/" + PinList.Max + ")" + (!ok && pin && Pins.IsFull ? " — three pins already" : ""));
        }

        private static void Summary(Terminal ctx)
        {
            Say(ctx, "Milestones: pinned = " + (Pins.Count == 0 ? "(none)" : string.Join(", ", Pins.Ids)));
            string paused = CheatState.PausedReason();
            if (paused != null)
                Say(ctx, "Milestones: achievements paused: " + paused);
            int n = 0;
            foreach (Achievement a in AchievementReader.All())
            {
                AchievementProgress p = ProgressCache.Get(a);
                if (p == null)
                    continue;
                n++;
                Say(ctx, string.Format("Milestones: {0} {1} \"{2}\" {3} ({4}/{5} met)",
                    p.Unlocked ? "[x]" : "[ ]", a.m_id, AchievementReader.Name(a), Labels.OverallText(p), p.MetCount, p.Total));
            }
            Say(ctx, "Milestones: " + n + " achievement(s).");
        }

        private static void Detail(Terminal ctx, string query)
        {
            Achievement a = AchievementReader.Find(query);
            if (a == null)
            {
                Say(ctx, "Milestones: no achievement matches \"" + query + "\".");
                return;
            }
            ProgressCache.Recompute(a);
            AchievementProgress p = ProgressCache.Get(a);
            Say(ctx, string.Format("Milestones: {0} \"{1}\" slot={2} lenient={3} overall={4} {5}",
                a.m_id, AchievementReader.Name(a), a.m_difficultyRequirement, a.m_lenientBuildAchievement,
                Labels.OverallText(p), p.Unlocked ? "UNLOCKED" : ""));
            foreach (Objective o in p.Objectives)
            {
                Say(ctx, string.Format("Milestones:   {0} {1} [{2} {3}] {4} {5}",
                    o.Met ? "[x]" : "[ ]", o.Label, o.Source, o.Key, o.Op, Labels.ProgressText(o)));
            }
        }

        private static void DumpUi(Terminal ctx)
        {
            InventoryGui inv = InventoryGui.instance;
            if (inv == null || inv.m_achievementsPanel == null)
            {
                Say(ctx, "Milestones: no achievements panel.");
                return;
            }
            Dump(ctx, inv.m_achievementsPanel.m_achievementDetails.transform, 0, 3);
            if (inv.m_achievementsPanel.m_achievementDetailsElementPrefab != null)
                Dump(ctx, inv.m_achievementsPanel.m_achievementDetailsElementPrefab.transform, 0, 3);
        }

        private static void Dump(Terminal ctx, Transform t, int depth, int maxDepth)
        {
            var rt = t as RectTransform;
            var comps = new System.Text.StringBuilder();
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c != null && !(c is Transform))
                    comps.Append(c.GetType().Name).Append(' ');
            }
            Say(ctx, string.Format("Milestones: {0}{1} active={2} size={3} anchors={4}-{5} pos={6} [{7}]",
                new string(' ', depth * 2), t.name, t.gameObject.activeSelf,
                rt != null ? rt.rect.size.ToString() : "-", rt != null ? rt.anchorMin.ToString() : "-",
                rt != null ? rt.anchorMax.ToString() : "-", rt != null ? rt.anchoredPosition.ToString() : "-", comps));
            if (depth >= maxDepth)
                return;
            foreach (Transform child in t)
                Dump(ctx, child, depth + 1, maxDepth);
        }
    }
}
