using System.Collections.Generic;
using Milestones.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Milestones.UI
{
    /// <summary>A small amber corner mark on the grid tiles of pinned achievements.</summary>
    internal static class ListMarks
    {
        private const string MarkName = "MilestonesPinMark";

        public static void Init()
        {
            Pins.Changed += () =>
            {
                InventoryGui inv = InventoryGui.instance;
                if (inv != null && inv.IsAchievementsPanelOpen)
                    Apply(inv);
            };
        }

        public static void Apply(InventoryGui inv)
        {
            List<GameObject> tiles = inv.m_achievementsPanel.m_achievementsList;
            List<Achievement> order = AchievementReader.GridOrder();
            if (order.Count != tiles.Count)
                return; // the game changed its grid order; skip the marks rather than mislabel tiles
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null)
                    continue;
                // On the icon's corner: the tile cell is wider than its icon, and a mark on the
                // cell's corner floats in the gap between tiles.
                Transform host = tiles[i].transform.Find("icon") ?? tiles[i].transform;
                Transform mark = host.Find(MarkName);
                bool pinned = Pins.Contains(order[i].m_id);
                if (pinned && mark == null)
                {
                    RectTransform rt = UiUtil.Rect(MarkName, host);
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(-6f, -6f);
                    rt.sizeDelta = new Vector2(14f, 14f);
                    Image img = rt.gameObject.AddComponent<Image>();
                    img.sprite = UiUtil.White;
                    img.color = PluginConfig.ColorInProgress.Value;
                    img.raycastTarget = false;
                }
                else if (!pinned && mark != null)
                {
                    Object.Destroy(mark.gameObject);
                }
            }
        }
    }
}
