using System.Collections.Generic;
using Milestones.Core;
using Milestones.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Milestones.UI
{
    /// <summary>
    /// The pinned-achievement widget. Lives under Hud.m_rootObject, so F3 and photo mode hide it
    /// with the rest of the HUD. Rebuilt only when the pin set or a pinned achievement changes;
    /// positioned every frame (cheap) so it follows the status-effect icons.
    /// </summary>
    internal sealed class TrackerHud : MonoBehaviour
    {
        // Gap under the status-effect icons, in HUD units: clears the effect name under each icon.
        private const float StatusGap = 26f;

        private static TrackerHud _instance;
        private static bool _dirty = true;

        private RectTransform _panel;
        private CanvasGroup _group;
        private Canvas _canvas;
        private readonly Dictionary<string, BlockView> _blocks = new Dictionary<string, BlockView>();
        private TextMeshProUGUI _paused;
        private float _nextPausedCheck;
        private readonly Vector3[] _corners = new Vector3[4];

        private sealed class LineView
        {
            public RectTransform Root;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Value;
            public Bar Bar;
            public int ObjectiveIndex = -1;
        }

        private sealed class BlockView
        {
            public RectTransform Root;
            public Image Icon;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Overall;
            public Bar OverallBar;
            public readonly List<LineView> Lines = new List<LineView>();
            public TextMeshProUGUI More;
        }

        public static void Init()
        {
            Runtime.Ticked += Ensure;
            Pins.Changed += () => _dirty = true;
            ProgressCache.Changed += (a, p) =>
            {
                if (Pins.Contains(a.m_id))
                    _dirty = true;
            };
            Runtime.PlayerSpawned += p => _dirty = true;
            PluginConfig.MaxRowsPerAchievement.SettingChanged += (s, e) => Rebuild();
        }

        private static void Rebuild()
        {
            if (_instance != null)
            {
                foreach (BlockView b in _instance._blocks.Values)
                    Destroy(b.Root.gameObject);
                _instance._blocks.Clear();
            }
            _dirty = true;
        }

        private static void Ensure()
        {
            if (_instance != null)
                return;
            Hud hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return;
            RectTransform holder = UiUtil.Rect("MilestonesTracker", hud.m_rootObject.transform);
            UiUtil.Stretch(holder);
            _instance = holder.gameObject.AddComponent<TrackerHud>();
            _instance.Build();
            _dirty = true;
        }

        private void Build()
        {
            _panel = UiUtil.Rect("panel", transform);
            _panel.pivot = new Vector2(1f, 1f);
            Image bg = _panel.gameObject.AddComponent<Image>();
            bg.sprite = UiUtil.White;
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            bg.raycastTarget = false;
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            VerticalLayoutGroup v = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(8, 8, 6, 6);
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _paused = UiUtil.Text(_panel, "paused", 12f, TextAlignmentOptions.Left);
            _paused.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            _paused.gameObject.SetActive(false);

            _canvas = GetComponentInParent<Canvas>();
        }

        private void LateUpdate()
        {
            bool show = PluginConfig.Enabled.Value && PluginConfig.ShowTracker.Value && Pins.Count > 0
                && Player.m_localPlayer != null
                && !(PluginConfig.HideWhenInventoryOpen.Value && InventoryGui.IsVisible());
            if (_panel.gameObject.activeSelf != show)
                _panel.gameObject.SetActive(show);
            if (!show)
                return;

            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }
            if (Runtime.Now >= _nextPausedCheck)
            {
                _nextPausedCheck = Runtime.Now + 1f;
                string reason = CheatState.PausedReason();
                _paused.gameObject.SetActive(reason != null);
                if (reason != null)
                    _paused.text = "Achievements paused: " + reason;
            }

            _group.alpha = PluginConfig.Opacity.Value;
            _panel.sizeDelta = new Vector2(PluginConfig.Width.Value, _panel.sizeDelta.y);
            _panel.localScale = Vector3.one * PluginConfig.Scale.Value;
            Place();
        }

        private void Place()
        {
            TrackerAnchor anchor = PluginConfig.Anchor.Value;
            var offset = new Vector2(PluginConfig.OffsetX.Value, PluginConfig.OffsetY.Value);
            if (anchor == TrackerAnchor.UnderStatusEffects)
            {
                Hud hud = Hud.instance;
                float k = _canvas != null ? _canvas.rootCanvas.scaleFactor : 1f;
                RectTransform root = hud.m_statusEffectListRoot;
                float right = root.position.x, bottom = root.position.y;
                foreach (RectTransform se in hud.m_statusEffects)
                {
                    if (se == null || !se.gameObject.activeSelf)
                        continue;
                    se.GetWorldCorners(_corners);
                    right = Mathf.Max(right, _corners[2].x);
                    bottom = Mathf.Min(bottom, _corners[0].y);
                }
                _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
                _panel.pivot = new Vector2(1f, 1f);
                _panel.position = new Vector3(right + offset.x * k, bottom - StatusGap * k + offset.y * k, 0f);
                return;
            }

            Vector2 a;
            switch (anchor)
            {
                case TrackerAnchor.TopLeft: a = new Vector2(0f, 1f); break;
                case TrackerAnchor.TopRight: a = new Vector2(1f, 1f); break;
                case TrackerAnchor.LeftMiddle: a = new Vector2(0f, 0.5f); break;
                case TrackerAnchor.RightMiddle: a = new Vector2(1f, 0.5f); break;
                case TrackerAnchor.BottomLeft: a = new Vector2(0f, 0f); break;
                default: a = new Vector2(1f, 0f); break;
            }
            _panel.anchorMin = _panel.anchorMax = a;
            _panel.pivot = a;
            var inset = new Vector2(a.x < 0.5f ? 16f : a.x > 0.5f ? -16f : 0f, a.y < 0.5f ? 16f : a.y > 0.5f ? -16f : 0f);
            _panel.anchoredPosition = inset + offset;
        }

        private void Refresh()
        {
            var keep = new HashSet<string>(Pins.Ids);
            var remove = new List<string>();
            foreach (string id in _blocks.Keys)
            {
                if (!keep.Contains(id))
                    remove.Add(id);
            }
            foreach (string id in remove)
            {
                Destroy(_blocks[id].Root.gameObject);
                _blocks.Remove(id);
            }

            int sibling = 0;
            foreach (string id in Pins.Ids)
            {
                Achievement a = AchievementReader.ById(id);
                if (a == null)
                    continue;
                AchievementProgress p = ProgressCache.Get(a);
                if (p == null)
                    continue;
                BlockView b;
                bool fresh = !_blocks.TryGetValue(id, out b);
                if (fresh)
                {
                    b = NewBlock(a);
                    _blocks[id] = b;
                }
                b.Root.SetSiblingIndex(sibling++);
                Fill(b, a, p, ease: !fresh);
            }
            _paused.transform.SetAsLastSibling();
        }

        private BlockView NewBlock(Achievement a)
        {
            var b = new BlockView();
            b.Root = UiUtil.Rect("block_" + a.m_id, _panel);
            VerticalLayoutGroup v = b.Root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 2f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            RectTransform head = UiUtil.Rect("head", b.Root);
            HorizontalLayoutGroup h = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;

            RectTransform iconRt = UiUtil.Rect("icon", head);
            b.Icon = iconRt.gameObject.AddComponent<Image>();
            b.Icon.raycastTarget = false;
            b.Icon.preserveAspect = true;
            LayoutElement ile = iconRt.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.minWidth = 20f;
            ile.preferredHeight = ile.minHeight = 20f;

            b.Title = UiUtil.Text(head, "title", 15f, TextAlignmentOptions.Left);
            b.Title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            b.Overall = UiUtil.Text(head, "overall", 13f, TextAlignmentOptions.Right);

            b.OverallBar = Bar.CreateInLayout(b.Root, 6f);

            for (int i = 0; i < PluginConfig.MaxRowsPerAchievement.Value; i++)
                b.Lines.Add(NewLine(b.Root, i));

            b.More = UiUtil.Text(b.Root, "more", 11f, TextAlignmentOptions.Left);
            b.More.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            return b;
        }

        private static LineView NewLine(RectTransform parent, int i)
        {
            var l = new LineView();
            l.Root = UiUtil.Rect("line" + i, parent);
            VerticalLayoutGroup v = l.Root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 1f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            RectTransform row = UiUtil.Rect("text", l.Root);
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            l.Name = UiUtil.Text(row, "name", 12f, TextAlignmentOptions.Left);
            l.Name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            l.Value = UiUtil.Text(row, "value", 12f, TextAlignmentOptions.Right);

            l.Bar = Bar.CreateInLayout(l.Root, 3f);
            return l;
        }

        private static void Fill(BlockView b, Achievement a, AchievementProgress p, bool ease)
        {
            b.Icon.sprite = p.Unlocked ? a.m_icon : a.m_iconLocked;
            b.Title.text = AchievementReader.Name(a);
            b.Overall.text = p.Unlocked ? "Done " + Labels.Check : Labels.OverallText(p);
            b.Overall.color = UiUtil.TextColor(p.Unlocked);
            b.OverallBar.Set(p.Overall, UiUtil.BarColor(p.Overall, p.Unlocked), ease);

            TrackerRows rows = p.Unlocked ? new TrackerRows() : ObjectiveOrder.Select(p, b.Lines.Count);
            for (int i = 0; i < b.Lines.Count; i++)
            {
                LineView l = b.Lines[i];
                bool on = i < rows.Indices.Count;
                l.Root.gameObject.SetActive(on);
                if (!on)
                {
                    l.ObjectiveIndex = -1;
                    continue;
                }
                int idx = rows.Indices[i];
                Objective o = p.Objectives[idx];
                l.Name.text = PluginConfig.HideUnmetNames.Value && !o.Met ? "???" : o.Label;
                l.Value.text = o.HasBar ? Labels.Amount(System.Math.Min(o.Current, o.Target)) + " / " + Labels.Amount(o.Target) : (o.Met ? "met" : "not met");
                Color c = UiUtil.TextColor(o.Met);
                l.Name.color = c;
                l.Value.color = c;
                // Ease only when the slot still shows the same objective; a new occupant snaps.
                l.Bar.Set(o.Fraction, UiUtil.BarColor(o), ease && l.ObjectiveIndex == idx);
                l.ObjectiveIndex = idx;
            }
            b.More.gameObject.SetActive(rows.More > 0);
            b.More.text = "+" + rows.More + " more";
        }
    }
}
