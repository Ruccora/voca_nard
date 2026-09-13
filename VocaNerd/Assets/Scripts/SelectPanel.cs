using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VocaNerd
{
    public class SelectPanel : PanelBase
    {
        [Tooltip("ミニゲームごとの説明画面 prefab。ボタンと同じ並び順 (0=左上 1=右上 2=左下 3=右下)")]
        [SerializeField] private ExplainPanelBase[] explainPanelPrefabs = new ExplainPanelBase[4];
        [Tooltip("ミニゲームボタン。未設定 (None) のスロットは配線対象から外れる")]
        [SerializeField] private Button[] miniGameButtons = new Button[4];
        [Tooltip("ボタンの並びの列数。2 なら 2xN グリッドとして上下左右を配線する")]
        [SerializeField] private int navigationColumns = 2;
        [SerializeField] private RectTransform explainRoot;
        [SerializeField] private SelectionIndicator selectionIndicator;

        [Header("Intro (FadeIn → 一拍おいて ScaleUp)")]
        [Tooltip("FadeIn の後に拡大する object")]
        [SerializeField] private RectTransform introScaleUpRect;
        [Tooltip("FadeIn が終わってから拡大を始めるまでの待機秒数")]
        [SerializeField] private float introScaleUpDelay = 1f;
        [Tooltip("拡大後のスケール倍率 (prefab のスケールに対する倍率)。拡大したらそのまま維持する")]
        [SerializeField] private float introScaleUpFactor = 1.3f;
        [Tooltip("拡大にかける秒数")]
        [SerializeField] private float introScaleUpDuration = 0.8f;

        private ExplainPanelBase _activeExplain;
        private Vector3 _introScaleUpHome = Vector3.one;
        private int _selectedIndex = -1;
        private InputAction _backAction;
        private bool _leaving;

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

            // B でタイトルに戻る。操作できるのは 1P だけ。
            // 2P のパッドも同じパスに解決されるので PassThrough (理由は PlayerInputAction)。
            _backAction = PlayerInputAction.Make("SelectBack",
                GamepadButtons.B, "<Keyboard>/escape", "<Keyboard>/backspace");
            PlayerInputAction.OnPress(_backAction, 1, OnBack);

            if (introScaleUpRect != null) _introScaleUpHome = introScaleUpRect.localScale;
        }

        private void OnDestroy()
        {
            _backAction?.Dispose();
            _backAction = null;
        }

        // 説明画面が開いている間は、その画面の「戻る」に任せる (二重に効かせない)。
        private void OnBack()
        {
            if (IsAnimating || _leaving) return;
            if (_activeExplain != null) return;

            _leaving = true;
            _backAction?.Disable();
            Audio.PlaySE(SeKey.Cancel);
            ScreenController.Instance.ShowAsync(ScreenType.Title).Forget();
        }

        /// <summary>
        /// ミニゲームボタンを navigationColumns 列のグリッドとみなして上下左右を明示配線する
        /// (既定 2 列 = 0=左上 1=右上 2=左下 3=右下)。既定の Automatic ナビだと左右候補が
        /// 安定して拾えず「上下は動くが左右が動かない」症状になるため。
        ///
        /// 未設定 (null) のスロットは飛ばして「実際に使うボタンだけ」で配線するので、
        /// 3 個など半端な数でも隣に存在しないボタンへ飛んでカーソルを見失うことはない。
        /// </summary>
        private void SetupNavigation()
        {
            var buttons = new List<Button>();
            if (miniGameButtons != null)
            {
                foreach (var b in miniGameButtons)
                    if (b != null) buttons.Add(b);
            }
            if (buttons.Count == 0) return;

            var cols = Mathf.Max(1, navigationColumns);
            for (var i = 0; i < buttons.Count; i++)
            {
                var row = i / cols;
                var col = i % cols;
                var rowLast = Mathf.Min(row * cols + cols, buttons.Count) - 1; // この行の最後の index

                // 真下が無くても、次の行が存在するならその行の最後へ送る (最終行が欠けている場合)
                Button down = null;
                if (i + cols < buttons.Count) down = buttons[i + cols];
                else if ((row + 1) * cols < buttons.Count) down = buttons[buttons.Count - 1];

                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = col > 0 ? buttons[i - 1] : null,
                    selectOnRight = i < rowLast ? buttons[i + 1] : null,
                    selectOnUp = row > 0 ? buttons[i - cols] : null,
                    selectOnDown = down,
                };
            }

#if UNITY_EDITOR
            // カーソル (SelectionIndicator) の targets から漏れているとそのボタンで
            // カーソルが消えるので、組み違いを気づけるようにしておく
            if (selectionIndicator != null)
            {
                foreach (var b in buttons)
                    if (!selectionIndicator.HasTarget(b))
                        Debug.LogWarning($"[SelectPanel] {b.name} が SelectionIndicator.targets に入っていません。" +
                                         "選択が移るとカーソルが消えます", b);
            }
#endif
        }

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

                RestoreSelectedFocus();
            }
            catch (System.OperationCanceledException)
            {
                if (explain != null) Destroy(explain.gameObject);
                _activeExplain = null;
            }
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

        // ボタン個別の演出は持たない。説明画面を開く前にカーソルだけ隠す。
        protected override UniTask OnPanelPreOutAsync(CancellationToken token)
        {
            if (_activeExplain != null) return UniTask.CompletedTask;

            if (selectionIndicator != null) selectionIndicator.Hide();
            return UniTask.CompletedTask;
        }

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            // 1) FadeIn
            canvasGroup.alpha = 0f;
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

            _backAction?.Enable();
        }

        protected override async UniTask OnPanelOutAsync(CancellationToken token)
        {
            _backAction?.Disable();
            await base.OnPanelOutAsync(token);
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
    }
}