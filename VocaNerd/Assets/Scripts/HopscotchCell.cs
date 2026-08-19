using Coffee.UIEffects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    [RequireComponent(typeof(RectTransform))]
    public class HopscotchCell : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image toggleMark;
        [SerializeField] private RectTransform toggleMarkRect;
        [SerializeField] private GameObject secondaryPlatform;
        [SerializeField] private Image secondaryImage;
        [SerializeField] private RectTransform secondaryRect;
        [SerializeField] private Image secondaryToggleMark;

        [Header("Colors")]
        [SerializeField] private Color typeAColor = new Color(0.3f, 0.5f, 0.9f);
        [SerializeField] private Color typeBColor = new Color(0.9f, 0.4f, 0.3f);
        [SerializeField] private Color toggleOnColor = new Color(0.3f, 1f, 0.3f, 0.7f);
        [SerializeField] private Color toggleOffColor = new Color(1f, 0.3f, 0.3f, 0.4f);

        [Header("Layout")]
        [SerializeField] private Vector2 secondaryOffset = new Vector2(110f, 0f);

        private RectTransform _rt;
        private bool _isToggle;

        public RectTransform Rect => _rt != null ? _rt : (_rt = (RectTransform)transform);

        public void Setup(bool isTypeA, bool isToggle)
        {
            _isToggle = isToggle;
            var color = isTypeA ? typeAColor : typeBColor;
            if (background != null) background.color = color;
            if (label != null) label.text = isTypeA ? "A" : "B";
            if (toggleMark != null)
            {
                toggleMark.gameObject.SetActive(isToggle);
                if (isToggle) toggleMark.color = toggleOnColor;
            }
            if (secondaryPlatform != null)
                secondaryPlatform.SetActive(!isTypeA);
            if (secondaryImage != null)
                secondaryImage.color = color;
            if (secondaryRect != null && !isTypeA)
            {
                secondaryRect.anchoredPosition = secondaryOffset;
            }
            if (secondaryToggleMark != null)
            {
                secondaryToggleMark.gameObject.SetActive(isToggle && !isTypeA);
                if (isToggle) secondaryToggleMark.color = toggleOnColor;
            }
        }

        public void SetToggleState(bool on)
        {
            if (!_isToggle) return;
            var color = on ? toggleOnColor : toggleOffColor;
            if (toggleMark != null) toggleMark.color = color;
            if (secondaryToggleMark != null) secondaryToggleMark.color = color;
        }

        private UIEffect[] _depthEffects;

        // 奥行きの明暗。配下の全 Graphic に UIEffect(Multiply) を当てて暗くする。
        // multiplier = 1 で通常、小さいほど暗い。
        public void SetDarken(float multiplier)
        {
            if (_depthEffects == null)
            {
                var graphics = GetComponentsInChildren<Graphic>(true);
                _depthEffects = new UIEffect[graphics.Length];
                for (var i = 0; i < graphics.Length; i++)
                {
                    var fx = graphics[i].GetComponent<UIEffect>();
                    if (fx == null) fx = graphics[i].gameObject.AddComponent<UIEffect>();
                    _depthEffects[i] = fx;
                }
            }

            var m = Mathf.Clamp01(multiplier);
            var darken = m < 0.999f;
            foreach (var fx in _depthEffects)
            {
                if (fx == null) continue;
                if (darken)
                {
                    fx.colorFilter = ColorFilter.Multiply;
                    fx.color = new Color(m, m, m, 1f);
                }
                else
                {
                    fx.colorFilter = ColorFilter.None;
                }
            }
        }
    }
}