using Milestones.Core;
using Milestones.Core.Model;
using UnityEngine;

namespace Milestones.UI
{
    /// <summary>One row of the vanilla details list (AchievementDetailUnlockCondition) plus our bar.</summary>
    internal sealed class ProgressRow
    {
        public const int Header = -1;

        public readonly AchievementDetailUnlockCondition Row;
        public readonly int ObjectiveIndex;
        private readonly Bar _bar;

        public ProgressRow(AchievementDetailUnlockCondition row, int objectiveIndex)
        {
            Row = row;
            ObjectiveIndex = objectiveIndex;
            _bar = Bar.CreateFloating((RectTransform)row.transform, objectiveIndex == Header ? 6f : 4f, 4f);
        }

        public bool Alive => Row != null;

        public void Show(Objective o, bool ease)
        {
            bool hide = PluginConfig.HideUnmetNames.Value && !o.Met;
            Row.StatName.text = hide ? "???" : o.Label;
            Row.Progress.text = Labels.ProgressText(o);
            Color text = UiUtil.TextColor(o.Met);
            Row.StatName.color = text;
            Row.Progress.color = text;
            _bar.Set(o.Fraction, UiUtil.BarColor(o), ease);
        }

        public void ShowOverall(Achievement a, AchievementProgress p, bool ease)
        {
            Row.StatName.text = AchievementReader.Name(a);
            Row.Progress.text = p.ShowAsCount ? p.MetCount + " / " + p.Total + " objectives" : Labels.Percent(p.Overall);
            Color text = UiUtil.TextColor(p.Unlocked);
            Row.StatName.color = text;
            Row.Progress.color = text;
            _bar.Set(p.Overall, UiUtil.BarColor(p.Overall, p.Unlocked), ease);
        }
    }
}
