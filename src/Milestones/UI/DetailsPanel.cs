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

        private static IEnumerator Populate(AchievementsGui gui, Achievement a, List<int> order)
        {
            int batch = Math.Max(1, gui.m_detailStatsPerFrame);
            int n = 0;
            foreach (int i in order)
            {
                if (Current != a || gui == null || !gui.m_achievementDetails.activeSelf)
                    yield break;
                AchievementProgress p = ProgressCache.Get(a);
                if (i >= p.Total)
                    yield break;
                var row = new ProgressRow(NewRow(gui), i);
                row.Show(p.Objectives[i], ease: false);
                Rows.Add(row);
                if (++n == batch)
                {
                    n = 0;
                    yield return null;
                }
            }
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
        }
    }
}
