using Milestones.Core.Model;

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

        private static void Summary(Terminal ctx)
        {
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
    }
}
