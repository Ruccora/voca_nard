using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaNerd
{
    public class SelectPanel : PanelBase
    {
        [SerializeField] private MiniGameData[] miniGames = new MiniGameData[4];
        [SerializeField] private Button[] miniGameButtons = new Button[4];
        [SerializeField] private Image[] miniGameThumbnails = new Image[4];
        [SerializeField] private ExplainPanel explainPanelPrefab;
        [SerializeField] private RectTransform explainRoot;
        [SerializeField] private SelectionIndicator selectionIndicator;
        [SerializeField] private float expandDuration = 0.35f;

        [Header("Animated Rects")]
        [SerializeField] private RectTransform headerRect;
        [SerializeField] private RectTransform[] miniGameButtonRects = new RectTransform[4];

        public RectTransform HeaderRect => headerRect;
        public RectTransform[] MiniGameButtonRects => miniGameButtonRects;

        private ExplainPanel _activeExplain;
        private Vector2 _headerRestingPos;
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

                if (miniGameThumbnails[i] != null && miniGames[i] != null)
                    miniGameThumbnails[i].sprite = miniGames[i].Thumbnail;
            }

            SetupNavigation();

            if (headerRect != null) _headerRestingPos = headerRect.anchoredPosition;
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
            if (index < 0 || index >= miniGames.Length) return;
            var data = miniGames[index];
            if (data == null || explainPanelPrefab == null) return;

            _selectedIndex = index;
            OpenExplainAsync(data).Forget();
        }

        private async UniTaskVoid OpenExplainAsync(MiniGameData data)
        {
            var token = this.GetCancellationTokenOnDestroy();
            ExplainPanel explain = null;
            try
            {
                await PanelPreOutAsync(token);
                SetInteractable(true);

                var parent = explainRoot != null ? explainRoot : (RectTransform)transform;
                explain = Instantiate(explainPanelPrefab, parent);
                _activeExplain = explain;
                explain.Bind(data);
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
            canvasGroup.alpha = 1f;

            var panelRt = (RectTransform)transform;
            var panelW = panelRt.rect.width;
            var panelH = panelRt.rect.height;

            var headerStart = _headerRestingPos;
            if (headerRect != null)
            {
                var slideY = (panelH > 0f ? panelH * 0.5f : 540f)
                             + headerRect.rect.height * 0.5f + 40f;
                headerStart = _headerRestingPos + new Vector2(0f, slideY);
                headerRect.anchoredPosition = headerStart;
            }

            var buttonStarts = new Vector2[miniGameButtonRects.Length];
            for (var i = 0; i < miniGameButtonRects.Length; i++)
            {
                var rt = miniGameButtonRects[i];
                if (rt == null) continue;
                var fromLeft = i % 2 == 0;
                var slideX = (panelW > 0f ? panelW * 0.5f : 960f)
                             + rt.rect.width * 0.5f + 40f;
                buttonStarts[i] = _buttonRestingPos[i] + new Vector2(fromLeft ? -slideX : slideX, 0f);
                rt.anchoredPosition = buttonStarts[i];
            }

            var duration = fadeDuration;
            if (duration > 0f)
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    var eased = 1f - Mathf.Pow(1f - t, 3f);

                    if (headerRect != null)
                        headerRect.anchoredPosition = Vector2.LerpUnclamped(headerStart, _headerRestingPos, eased);

                    for (var i = 0; i < miniGameButtonRects.Length; i++)
                    {
                        var rt = miniGameButtonRects[i];
                        if (rt == null) continue;
                        rt.anchoredPosition = Vector2.LerpUnclamped(buttonStarts[i], _buttonRestingPos[i], eased);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }

            ApplyResting();
            // interactable が有効になる直前に選択を確定させる。フェード中の非インタラクティブな
            // タイミングで選択すると外れて「選択が効かない」ことがあるため末尾で行う。
            FocusDefaultSelected();
            if (selectionIndicator != null) selectionIndicator.Show();
        }

        private void ApplyResting()
        {
            if (headerRect != null) headerRect.anchoredPosition = _headerRestingPos;
            for (var i = 0; i < miniGameButtonRects.Length; i++)
            {
                if (miniGameButtonRects[i] != null)
                    miniGameButtonRects[i].anchoredPosition = _buttonRestingPos[i];
            }
        }
    }
}