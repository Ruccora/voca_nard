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

        [Header("Colors")]
        [SerializeField] private Color typeAColor = new Color(0.3f, 0.5f, 0.9f);
        [SerializeField] private Color typeBColor = new Color(0.9f, 0.4f, 0.3f);
        [SerializeField] private Color toggleOnColor = new Color(0.3f, 1f, 0.3f, 0.7f);
        [SerializeField] private Color toggleOffColor = new Color(1f, 0.3f, 0.3f, 0.4f);

        private RectTransform _rt;
        private bool _isToggle;

        public RectTransform Rect => _rt != null ? _rt : (_rt = (RectTransform)transform);

        public void Setup(bool isTypeA, bool isToggle)
        {
            _isToggle = isToggle;
            if (background != null) background.color = isTypeA ? typeAColor : typeBColor;
            if (label != null) label.text = isTypeA ? "A" : "B";
            if (toggleMark != null)
            {
                toggleMark.gameObject.SetActive(isToggle);
                if (isToggle) toggleMark.color = toggleOnColor;
            }
        }

        public void SetToggleState(bool on)
        {
            if (!_isToggle || toggleMark == null) return;
            toggleMark.color = on ? toggleOnColor : toggleOffColor;
        }
    }
}
