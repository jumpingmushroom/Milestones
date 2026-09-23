using System;
using System.Collections;
using System.Collections.Generic;
using Milestones.Core;
using Milestones.Core.Model;
using UnityEngine;

namespace Milestones.UI
{
    /// <summary>
    /// Builds the details list from the progress model with the game's own row prefab, parent and
    /// rows-per-frame batching (PLAN.md §2.2), and refreshes the rows in place while it is open.
    /// </summary>
    internal static class DetailsPanel
    {
        private static AchievementsGui _gui;
        private static ProgressRow _header;
        private static readonly List<ProgressRow> Rows = new List<ProgressRow>();

        public static Achievement Current { get; private set; }

        /// <summary>True once a row build has failed; the vanilla panel is used for the rest of the session.</summary>
        public static bool Failed { get; private set; }

        public static event Action<AchievementsGui, Achievement> Opened;

        static DetailsPanel()
        {
            ProgressCache.Changed += OnChanged;
        }

        public static void Open(AchievementsGui gui, Achievement a)
        {
            _gui = gui;
            Current = a;
            Rows.Clear();
            gui.m_achievementDetails.SetActive(true);

            ProgressCache.Recompute(a);
            AchievementProgress p = ProgressCache.Get(a);

            _header = new ProgressRow(NewRow(gui), ProgressRow.Header);
            _header.ShowOverall(a, p, ease: false);

            List<int> order;
            if (PluginConfig.SortObjectives.Value)
                order = ObjectiveOrder.Sorted(p);
            else
            {
                order = new List<int>(p.Total);
                for (int i = 0; i < p.Total; i++)
                    order.Add(i);
            }
            gui.StartCoroutine(Populate(gui, a, order));

            if (Opened != null)
                Opened(gui, a);
        }

        /// <summary>Outcome of adding one row, so <see cref="Populate"/> can yield without a try/catch around it.</summary>
        private enum RowResult { Added, Stop, Failed }

        private static IEnumerator Populate(AchievementsGui gui, Achievement a, List<int> order)
        {
            int batch = Math.Max(1, gui.m_detailStatsPerFrame);
            int n = 0;
            foreach (int i in order)
            {
                RowResult result = AddRow(gui, a, i);
                if (result == RowResult.Failed)
                {
                    Fail(gui, a);
                    yield break;
                }
                if (result == RowResult.Stop)
                    yield break;
                if (++n == batch)
                {
                    n = 0;
                    yield return null;
                }
            }
        }

        /// <summary>
        /// One row's worth of work (get progress, bounds check, new row, show, track it), wrapped in
        /// try/catch: C# can't yield inside a try that has a catch, so this can't live in Populate itself.
        /// </summary>
        private static RowResult AddRow(AchievementsGui gui, Achievement a, int i)
        {
            if (Current != a || gui == null || !gui.m_achievementDetails.activeSelf)
                return RowResult.Stop;
            try
            {
                AchievementProgress p = ProgressCache.Get(a);
                if (i >= p.Total)
                    return RowResult.Stop;
                var row = new ProgressRow(NewRow(gui), i);
                row.Show(p.Objectives[i], ease: false);
                Rows.Add(row);
                return RowResult.Added;
            }
            catch (Exception e)
            {
                MilestonesPlugin.WarnOnce("details panel", e);
                return RowResult.Failed;
            }
        }

        /// <summary>A row build failed mid-way: mark it, tear down our half-built panel, and let vanilla rebuild it.</summary>
        private static void Fail(AchievementsGui gui, Achievement a)
        {
            MarkFailed();
            Abort(gui);
            gui.OnOpenAchievementDetails(a, true);
        }

        /// <summary>
        /// The one place that flips <see cref="Failed"/>, so every failure path (the synchronous
        /// open, or a later coroutine resumption) agrees and stops competing with vanilla.
        /// </summary>
        public static void MarkFailed()
        {
            Failed = true;
        }

        private static AchievementDetailUnlockCondition NewRow(AchievementsGui gui)
        {
            AchievementDetailUnlockCondition row = UnityEngine.Object.Instantiate(gui.m_achievementDetailsElementPrefab, gui.m_achievementDetailsListRoot);
            row.gameObject.SetActive(true);
            return row;
        }

        private static void OnChanged(Achievement a, AchievementProgress p)
        {
            if (a != Current || _gui == null || !_gui.m_achievementDetails.activeSelf)
                return;
            if (_header != null && _header.Alive)
                _header.ShowOverall(a, p, ease: true);
            foreach (ProgressRow row in Rows)
            {
                if (row.Alive && row.ObjectiveIndex < p.Total)
                    row.Show(p.Objectives[row.ObjectiveIndex], ease: true);
            }
        }

        /// <summary>After AchievementsGui.CloseDetails destroyed the rows.</summary>
        public static void Closed()
        {
            Current = null;
            _header = null;
            Rows.Clear();
            PinButton.Hide();
        }

        /// <summary>Undo a half-built panel so vanilla can build its own (it bails out if the panel is active).</summary>
        public static void Abort(AchievementsGui gui)
        {
            Current = null;
            foreach (Transform child in gui.m_achievementDetailsListRoot)
                UnityEngine.Object.Destroy(child.gameObject);
            gui.m_achievementDetails.SetActive(false);
            _header = null;
            Rows.Clear();
            PinButton.Hide();
        }
    }
}
