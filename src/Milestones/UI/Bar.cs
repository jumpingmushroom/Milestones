using UnityEngine;
using UnityEngine.UI;

namespace Milestones.UI
{
    /// <summary>A background quad and a horizontally filled quad. Eases toward its target over ~0.3 s.</summary>
    internal sealed class Bar : MonoBehaviour
    {
        private const float FullSweepSeconds = 0.3f;

        private Image _fill;
        private float _target;
        private float _shown;

        /// <summary>Pinned to the bottom edge of a row, outside its layout group.</summary>
        public static Bar CreateFloating(RectTransform row, float height, float inset)
        {
            Bar bar = Build(row, "MilestonesBar");
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-2f * inset, height);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return bar;
        }

        /// <summary>A child of a vertical layout group, full width.</summary>
        public static Bar CreateInLayout(Transform parent, float height)
        {
            Bar bar = Build(parent, "MilestonesBar");
            LayoutElement le = bar.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            return bar;
        }

        private static Bar Build(Transform parent, string name)
        {
            RectTransform rt = UiUtil.Rect(name, parent);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UiUtil.White;
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            RectTransform fillRt = UiUtil.Rect("fill", rt);
            UiUtil.Stretch(fillRt);
            Image fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = UiUtil.White;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            Bar bar = rt.gameObject.AddComponent<Bar>();
            bar._fill = fill;
            return bar;
        }

        public void Set(float fraction, Color color, bool ease)
        {
            _target = Mathf.Clamp01(fraction);
            _fill.color = color;
            if (!ease)
            {
                _shown = _target;
                _fill.fillAmount = _shown;
            }
        }

        private void Update()
        {
            if (_shown == _target)
                return;
            _shown = Mathf.MoveTowards(_shown, _target, Time.unscaledDeltaTime / FullSweepSeconds);
            _fill.fillAmount = _shown;
        }
    }
}
