using Milestones.Core;
using Milestones.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Milestones.UI
{
    /// <summary>
    /// "Pin (1/3)" / "Unpin" beside the details panel's close button, cloned from it so it matches
    /// the game's style. Gamepad: Y while the details panel is open.
    /// </summary>
    internal static class PinButton
    {
        private static Button _button;
        private static TMP_Text _label;
        private static Achievement _for;

        public static void Init()
        {
            DetailsPanel.Opened += Attach;
            Pins.Changed += Refresh;
            Runtime.Ticked += PollGamepad;
        }

        private static void Attach(AchievementsGui gui, Achievement a)
        {
            _for = a;
            if (_button == null)
                Create(gui);
            Refresh();
        }

        private static Button FindCloseButton(AchievementsGui gui)
        {
            Button first = null;
            foreach (Button b in gui.m_achievementDetails.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "MilestonesPin")
                    continue;
                if (first == null)
                    first = b;
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentMethodName(i) == nameof(AchievementsGui.OnCloseAchievementDetails))
                        return b;
                }
            }
            return first;
        }

        private static void Create(AchievementsGui gui)
        {
            Button template = FindCloseButton(gui);
            if (template == null)
            {
                MilestonesPlugin.Log.LogWarning("Milestones: no button in the details panel to clone; pin with 'milestones pin <id>'.");
                return;
            }
            GameObject go = Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = "MilestonesPin";
            // The clone must not answer the close button's gamepad key or re-localize our label.
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c != null && (c is UIGamePad || c.GetType().Name == "Localize"))
                    Object.Destroy(c);
            }
            _button = go.GetComponent<Button>();
            _button.onClick = new Button.ButtonClickedEvent();
            _button.onClick.AddListener(OnClick);
            _label = go.GetComponentInChildren<TMP_Text>(true);

            var rt = (RectTransform)go.transform;
            var trt = (RectTransform)template.transform;
            rt.anchoredPosition = trt.anchoredPosition - new Vector2(trt.rect.width + 12f, 0f);
        }

        private static void OnClick()
        {
            if (_for != null)
                Pins.Toggle(_for);
        }

        private static void Refresh()
        {
            if (_button == null || _for == null || _label == null)
                return;
            bool pinned = Pins.Contains(_for.m_id);
            _label.text = pinned ? "Unpin" : Pins.IsFull ? "Unpin one first (3/3)" : "Pin (" + Pins.Count + "/" + PinList.Max + ")";
            _button.interactable = pinned || !Pins.IsFull;
        }

        private static void PollGamepad()
        {
            if (DetailsPanel.Current == null || _for != DetailsPanel.Current)
                return;
            if (ZInput.IsGamepadActive() && ZInput.GetButtonDown("JoyButtonY") && _button != null && _button.interactable)
                OnClick();
        }
    }
}
