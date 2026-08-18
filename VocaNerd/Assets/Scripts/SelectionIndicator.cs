using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaNerd
{
    public class SelectionIndicator : MonoBehaviour
    {
        public enum IndicatorPosition
        {
            Left,
            Right,
            Center,
        }

        [SerializeField] private RectTransform select;
        [SerializeField] private CanvasGroup selectGroup;
        [SerializeField] private Selectable[] targets;
        [SerializeField] private IndicatorPosition position = IndicatorPosition.Left;
        [SerializeField] private float paddingX = 20f;
        [SerializeField] private bool hideWhenNoMatch = true;
        [SerializeField] private CanvasGroupBlinker blinker;
        [SerializeField] private ShinyOutline shinyOutline;

        private bool _isVisible;
        private GameObject _lastSelected;
        private bool _lastMatched;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            if (selectGroup == null && select != null)
                selectGroup = select.GetComponent<CanvasGroup>();
            if (selectGroup != null) selectGroup.alpha = 0f;
            _isVisible = false;
        }

        public void Show()
        {
            if(shinyOutline != null) shinyOutline.Apply();
            _isVisible = true;
            _lastSelected = null;
            _lastMatched = false;
        }

        public void Hide()
        {
            _isVisible = false;
            if (selectGroup != null) selectGroup.alpha = 0f;
            _lastSelected = null;
            _lastMatched = false;
        }

        public UniTask BlinkAsync(float stepDuration, CancellationToken cancellationToken = default)
        {
            if (blinker == null) return UniTask.CompletedTask;
            return blinker.BlinkAsync(stepDuration, cancellationToken);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (select != null) selectGroup = select.GetComponent<CanvasGroup>();
        }
#endif

        private void LateUpdate()
        {
            if (!_isVisible) return;
            if (select == null) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var current = eventSystem.currentSelectedGameObject;
            var match = FindTarget(current);
            var matched = match != null;

            if (matched) MoveSelectTo((RectTransform)match.transform);

            if (current != _lastSelected || matched != _lastMatched)
            {
                _lastSelected = current;
                _lastMatched = matched;
                if (selectGroup != null)
                {
                    if (matched) selectGroup.alpha = 1f;
                    else if (hideWhenNoMatch) selectGroup.alpha = 0f;
                }
            }
        }

        private Selectable FindTarget(GameObject go)
        {
            if (go == null || targets == null) return null;
            foreach (var t in targets)
            {
                if (t == null) continue;
                if (t.gameObject == go) return t;
            }
            return null;
        }

        private void MoveSelectTo(RectTransform target)
        {
            var half = target.rect.width * 0.5f;
            var offsetX = position switch
            {
                IndicatorPosition.Left => -half - paddingX,
                IndicatorPosition.Right => half + paddingX,
                IndicatorPosition.Center => 0f,
                _ => -half - paddingX,
            };
            select.anchoredPosition = target.anchoredPosition + new Vector2(offsetX, 0f);
        }
    }
}