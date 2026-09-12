using System.Threading;
using Coffee.UIEffects;
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

        [Header("Selected Highlight")]
        [Tooltip("選択中の対象 (or その子) に UIEffect が付いていれば、ColorFilter / Color の指定は変えずに " +
                 "Color Intensity をこの値だけ上げる。0 で無効")]
        [SerializeField, Range(0f, 1f)] private float selectedIntensityShift = 0.25f;

        [Tooltip("選択が別の target へ移ったときに鳴らす SE キー。空なら無音")]
        [SerializeField] private string cursorSeKey = SeKey.Cursor;

        private bool _isVisible;
        private GameObject _lastSelected;
        private bool _lastMatched;

        // 明るくしている UIEffect と、上書き前の colorIntensity (選択が外れたら戻す)
        private UIEffect _highlighted;
        private float _highlightedIntensity;

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
            ClearHighlight();
        }

        private void OnDisable() => ClearHighlight();

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
                // Show() 直後の初回確定（_lastSelected == null）は移動ではないので鳴らさない
                if (matched && _lastSelected != null && current != _lastSelected)
                    Audio.PlaySE(cursorSeKey);

                _lastSelected = current;
                _lastMatched = matched;
                if (selectGroup != null)
                {
                    if (matched) selectGroup.alpha = 1f;
                    else if (hideWhenNoMatch) selectGroup.alpha = 0f;
                }

                // 選択中のものだけ明度を上げる (前の対象は元に戻す)
                if (matched) ApplyHighlight(match);
                else ClearHighlight();
            }
        }

        /// <summary>
        /// 選択された対象に UIEffect が付いていれば、その Color Intensity を selectedIntensityShift ぶん上げる。
        /// ColorFilter / Color の指定 (prefab 側の作り) は変えず、効き具合の数値だけを動かす。
        /// UIEffect が無ければ何もしない (こちらからは付けない)。
        /// </summary>
        private void ApplyHighlight(Selectable target)
        {
            var effect = FindEffect(target);
            if (effect == _highlighted) return;

            ClearHighlight();
            if (effect == null || selectedIntensityShift <= 0f) return;

            _highlighted = effect;
            _highlightedIntensity = effect.colorIntensity;
            effect.colorIntensity = Mathf.Clamp01(_highlightedIntensity + selectedIntensityShift);
        }

        private void ClearHighlight()
        {
            if (_highlighted == null)
            {
                _highlighted = null;
                return;
            }

            _highlighted.colorIntensity = _highlightedIntensity;
            _highlighted = null;
        }

        // 対象自身 → 配下の順に UIEffect を探す
        private static UIEffect FindEffect(Selectable target)
        {
            if (target == null) return null;
            var self = target.GetComponent<UIEffect>();
            return self != null ? self : target.GetComponentInChildren<UIEffect>(true);
        }

        /// <summary>この指示子が追従する対象に含まれているか (組み違いの検出用)。</summary>
        public bool HasTarget(Selectable target)
        {
            if (target == null || targets == null) return false;
            foreach (var t in targets)
                if (t == target) return true;
            return false;
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