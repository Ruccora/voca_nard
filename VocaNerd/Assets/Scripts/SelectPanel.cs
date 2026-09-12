using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaNerd
{
    public class SelectPanel : PanelBase
    {
        [Tooltip("ミニゲームごとの説明画面 prefab。ボタンと同じ並び順 (0=左上 1=右上 2=左下 3=右下)")]
        [SerializeField] private ExplainPanelBase[] explainPanelPrefabs = new ExplainPanelBase[4];
        [SerializeField] private Button[] miniGameButtons = new Button[4];
        [SerializeField] private RectTransform explainRoot;
        [SerializeField] private SelectionIndicator selectionIndicator;
        [SerializeField] private float expandDuration = 0.35f;

        [Header("Animated Rects")]
        [SerializeField] private RectTransform[] miniGameButtonRects = new RectTransform[4];

        [Header("Intro (FadeIn → 一拍おいて ScaleUp)")]
        [Tooltip("FadeIn の後に拡大する object")]
        [SerializeField] private RectTransform introScaleUpRect;
        [Tooltip("FadeIn が終わってから拡大を始めるまでの待機秒数")]
        [SerializeField] private float introScaleUpDelay = 1f;
        [Tooltip("拡大後のスケール倍率 (prefab のスケールに対する倍率)。拡大したらそのまま維持する")]
        [SerializeField] private float introScaleUpFactor = 1.3f;
        [Tooltip("拡大にかける秒数")]
        [SerializeField] private float introScaleUpDuration = 0.8f;

        public RectTransform[] MiniGameButtonRects => miniGameButtonRects;

        private ExplainPanelBase _activeExplain;
        private Vector3 _introScaleUpHome = Vector3.one;
        private Vector2[] _buttonRestingPos;
        private Vector2[] _buttonRestingSize;
        private int _selectedIndex = -1;

        protected override void Awake()
        {
            base.Awake();
            for (var i = 0; i < miniGameButtons.Length; i++)
            {
                var index = i;
                if (miniGameButtons[i] != null)
                    miniGameButtons[i].onClick.AddListener(() => OnSelect(index));
            }

            SetupNavigation();

            if (introScaleUpRect != null) _introScaleUpHome = introScaleUpRect.localScale;
            _buttonRestingPos = new Vector2[miniGameButtonRects.Length];
            _buttonRestingSize = new Vector2[miniGameButtonRects.Length];
            for (var i = 0; i < miniGameButtonRects.Length; i++)
            {
                if (miniGameButtonRects[i] != null)
                {
                    _buttonRestingPos[i] = miniGameButtonRects[i].anchoredPosition;
                    _buttonRestingSize[i] = miniGameButtonRects[i].sizeDelta;
                }
            }
        }

        /// <summary>
        /// 4 つのミニゲームボタンは 2x2 グリッド (0=左上 1=右上 2=左下 3=右下)。
        /// 既定の Automatic ナビだと左右候補が安定して拾えず「上下は動くが左右が動かない」
        /// 症状になるので、明示的に隣接関係を配線する。
        /// </summary>
        private void SetupNavigation()
        {
            if (miniGameButtons == null || miniGameButtons.Length < 4) return;
            LinkNav(0, right: 1, down: 2);
            LinkNav(1, left: 0, down: 3);
            LinkNav(2, right: 3, up: 0);
            LinkNav(3, left: 2, up: 1);
        }

        private void LinkNav(int index, int left = -1, int right = -1, int up = -1, int down = -1)
        {
            var btn = ButtonAt(index);
            if (btn == null) return;
            btn.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = ButtonAt(left),
                selectOnRight = ButtonAt(right),
                selectOnUp = ButtonAt(up),
                selectOnDown = ButtonAt(down),
            };
        }

        private Button ButtonAt(int index)
            => index >= 0 && index < miniGameButtons.Length ? miniGameButtons[index] : null;

        private void OnSelect(int index)
        {
            if (IsAnimating) return;
            if (_activeExplain != null) return;
            if (index < 0 || index >= explainPanelPrefabs.Length) return;
            var prefab = explainPanelPrefabs[index];
            if (prefab == null) return;

            _selectedIndex = index;
            OpenExplainAsync(prefab).Forget();
        }

        private async UniTaskVoid OpenExplainAsync(ExplainPanelBase prefab)
        {
            var token = this.GetCancellationTokenOnDestroy();
            ExplainPanelBase explain = null;
            try
            {
                await PanelPreOutAsync(token);
                SetInteractable(true);

                var parent = explainRoot != null ? explainRoot : (RectTransform)transform;
                explain = Instantiate(prefab, parent);
                _activeExplain = explain;
                await explain.SetupAsync(token);
                await explain.PanelInAsync(token);
                await explain.Closed;
                _activeExplain = null;

                ShowSelected();
                await CollapseSelectedAsync(token);
                RestoreSelectedFocus();
            }
            catch (System.OperationCanceledException)
            {
                if (explain != null) Destroy(explain.gameObject);
                _activeExplain = null;
            }
        }

        private void HideSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= miniGameButtons.Length) return;
            var btn = miniGameButtons[_selectedIndex];
            if (btn != null) btn.gameObject.SetActive(false);
        }

        private void ShowSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= miniGameButtons.Length) return;
            var btn = miniGameButtons[_selectedIndex];
            if (btn != null) btn.gameObject.SetActive(true);
        }

        private async UniTask CollapseSelectedAsync(CancellationToken token)
        {
            if (_selectedIndex < 0 || _selectedIndex >= miniGameButtonRects.Length) return;
            var rt = miniGameButtonRects[_selectedIndex];
            if (rt == null) return;

            var startPos = rt.anchoredPosition;
            var startSize = rt.sizeDelta;
            var targetPos = _buttonRestingPos[_selectedIndex];
            var targetSize = _buttonRestingSize[_selectedIndex];

            if (expandDuration <= 0f)
            {
                rt.anchoredPosition = targetPos;
                rt.sizeDelta = targetSize;
                return;
            }

            var elapsed = 0f;
            while (elapsed < expandDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / expandDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);
                rt.sizeDelta = Vector2.LerpUnclamped(startSize, targetSize, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            rt.anchoredPosition = targetPos;
            rt.sizeDelta = targetSize;
        }

        private void RestoreSelectedFocus()
        {
            if (_selectedIndex < 0 || _selectedIndex >= miniGameButtons.Length) return;
            var btn = miniGameButtons[_selectedIndex];
            if (btn == null) return;
            var es = EventSystem.current;
            if (es == null) return;
            es.SetSelectedGameObject(btn.gameObject);
            if (selectionIndicator != null) selectionIndicator.Show();
        }

        protected override async UniTask OnPanelPreOutAsync(CancellationToken token)
        {
            if (_activeExplain != null) return;

            if (selectionIndicator != null) selectionIndicator.Hide();
            await ExpandSelectedAsync(token);
            HideSelected();
        }

        private async UniTask ExpandSelectedAsync(CancellationToken token)
        {
            if (_selectedIndex < 0 || _selectedIndex >= miniGameButtonRects.Length) return;
            var rt = miniGameButtonRects[_selectedIndex];
            if (rt == null) return;

            rt.SetAsLastSibling();

            var panelRt = (RectTransform)transform;
            var canvasSize = new Vector2(panelRt.rect.width, panelRt.rect.height);
            var startPos = rt.anchoredPosition;
            var startSize = rt.sizeDelta;
            var targetPos = Vector2.zero;
            var targetSize = canvasSize;

            if (expandDuration <= 0f)
            {
                rt.anchoredPosition = targetPos;
                rt.sizeDelta = targetSize;
                return;
            }

            var elapsed = 0f;
            while (elapsed < expandDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / expandDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);
                rt.sizeDelta = Vector2.LerpUnclamped(startSize, targetSize, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            rt.anchoredPosition = targetPos;
            rt.sizeDelta = targetSize;
        }

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            // 1) FadeIn
            canvasGroup.alpha = 0f;
            ApplyResting();
            if (introScaleUpRect != null) introScaleUpRect.localScale = _introScaleUpHome;
            await FadeAsync(canvasGroup, 0f, 1f, fadeDuration, token);

            // 2) 一拍おく
            if (introScaleUpDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(introScaleUpDelay), DelayType.UnscaledDeltaTime, cancellationToken: token);

            // 3) ScaleUp。拡大したら戻さず、そのサイズのまま維持する。
            await ScaleUpIntroAsync(token);

            // interactable が有効になる直前に選択を確定させる。フェード中の非インタラクティブな
            // タイミングで選択すると外れて「選択が効かない」ことがあるため末尾で行う。
            FocusDefaultSelected();
            if (selectionIndicator != null) selectionIndicator.Show();
        }

        private async UniTask ScaleUpIntroAsync(CancellationToken token)
        {
            if (introScaleUpRect == null) return;

            var from = _introScaleUpHome;
            var to = _introScaleUpHome * introScaleUpFactor;
            if (introScaleUpDuration <= 0f)
            {
                introScaleUpRect.localScale = to;
                return;
            }

            var elapsed = 0f;
            while (elapsed < introScaleUpDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / introScaleUpDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                introScaleUpRect.localScale = Vector3.LerpUnclamped(from, to, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            introScaleUpRect.localScale = to;
        }

        private void ApplyResting()
        {
            for (var i = 0; i < miniGameButtonRects.Length; i++)
            {
                if (miniGameButtonRects[i] != null)
                    miniGameButtonRects[i].anchoredPosition = _buttonRestingPos[i];
            }
        }
    }
}