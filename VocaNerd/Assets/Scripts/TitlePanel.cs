using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VocaNerd
{
    public class TitlePanel : PanelBase
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private SelectionIndicator selectionIndicator;

        [Header("Animated Rects")]
        [SerializeField] private RectTransform[] titleLabelRects;
        [SerializeField] private RectTransform startButtonRect;
        [SerializeField] private RectTransform exitButtonRect;
        [SerializeField] private CanvasGroupBlinker startBlinker;

        [Header("In Animation")]
        [Tooltip("パネル表示後、タイトルをポップさせるまでの待機秒数")]
        [SerializeField] private float inStartDelay = 1f;
        [Tooltip("起動時にポップ表示するタイトルの scale 0→over→1")]
        [SerializeField] private float titleOvershootScale = 1.2f;
        [SerializeField] private float titlePopUpDuration = 0.25f;
        [SerializeField] private float titleSettleDuration = 0.12f;
        [Tooltip("タイトルが scale 1 になってから START/EXIT/Credit をフェードインする時間")]
        [SerializeField] private float menuFadeDuration = 0.2f;
        [Tooltip("タイトルのポップ後に同時にフェードインするグループ（START/EXIT/Credit など）")]
        [SerializeField] private CanvasGroup[] menuGroups;

        public SelectionIndicator SelectionIndicator => selectionIndicator;

        [Header("Out Animation")]
        [SerializeField] private float outSlideDistance = 2000f;
        [SerializeField] private float outSlideDuration = 0.25f;
        [SerializeField] private float preOutBlinkStep = 0.5f;

        public RectTransform[] TitleLabelRects => titleLabelRects;
        public RectTransform StartButtonRect => startButtonRect;
        public RectTransform ExitButtonRect => exitButtonRect;

        private bool isStart = false;
        
        protected override void Awake()
        {
            base.Awake();
            isStart = true;
            startButton.onClick.AddListener(OnStart);
            exitButton.onClick.AddListener(OnExit);
            SetupNavigation();
        }

        // Start(上) と Exit(下) の上下ナビを明示配線する。Automatic 任せだと
        // スティックのドリフトや候補判定のブレで選択が不安定になるため。
        private void SetupNavigation()
        {
            if (startButton != null)
            {
                startButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnDown = exitButton,
                };
            }
            if (exitButton != null)
            {
                exitButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = startButton,
                };
            }
        }

        private void Update()
        {
            CheckClearSaveDataShortcut();
        }

        // タイトル画面で ⌘(Ctrl) + Ctrl + D を押すとセーブデータを全削除するデバッグ用ショートカット。
        private void CheckClearSaveDataShortcut()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var cmd = kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            var ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (cmd && ctrl && kb.dKey.wasPressedThisFrame)
            {
                SaveData.ClearGame(SaveData.GameId.BlockDrop);
                SaveData.ClearGame(SaveData.GameId.HopscotchRace);
                SaveData.ClearGame(SaveData.GameId.MashRace);
                Debug.Log("[Title] Save data cleared.");
            }
        }

        private void OnStart()
        {
            if (IsAnimating) return;
            ScreenController.Instance.ShowAsync(ScreenType.Select).Forget();
        }

        private void OnExit()
        {
            if (IsAnimating) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            
            // 初期状態: タイトルは scale0、START/EXIT は非表示。パネル自体はタイトルの
            // scale で見せるので alpha は即座に 1 にしてフェードは使わない。
            SetTitleScale(0f);
            SetMenuAlpha(0f);
            canvasGroup.alpha = 1f;

            // ポップ開始前に少し待つ
            if (inStartDelay > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(inStartDelay), DelayType.UnscaledDeltaTime, cancellationToken: token);

            // タイトルを 0 → 1.2 → 1 でポップさせる
            await ScaleTitleAsync(0f, titleOvershootScale, titlePopUpDuration, token);
            await ScaleTitleAsync(titleOvershootScale, 1f, titleSettleDuration, token);

            // scale が 1 になったタイミングで START / EXIT を表示してフォーカス
            FocusDefaultSelected();
            await FadeMenuInAsync(token);
            if (selectionIndicator != null) selectionIndicator.Show();
        }

        private void SetTitleScale(float scale)
        {
            if (titleLabelRects == null) return;
            var s = new Vector3(scale, scale, 1f);
            foreach (var rect in titleLabelRects)
                if (rect != null) rect.localScale = s;
        }

        private async UniTask ScaleTitleAsync(float from, float to, float duration, CancellationToken token)
        {
            if (duration <= 0f)
            {
                SetTitleScale(to);
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                SetTitleScale(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetTitleScale(to);
        }

        private void SetMenuAlpha(float alpha)
        {
            if (menuGroups == null) return;
            foreach (var group in menuGroups)
                if (group != null) group.alpha = alpha;
        }

        private async UniTask FadeMenuInAsync(CancellationToken token)
        {
            if (menuFadeDuration <= 0f)
            {
                SetMenuAlpha(1f);
                return;
            }

            var elapsed = 0f;
            while (elapsed < menuFadeDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                SetMenuAlpha(Mathf.Clamp01(elapsed / menuFadeDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetMenuAlpha(1f);
        }

        protected override async UniTask OnPanelPreOutAsync(CancellationToken token)
        {
            if (selectionIndicator == null) return;
            if (isStart) await startBlinker.BlinkAsync(preOutBlinkStep, token);
        }

        protected override async UniTask OnPanelOutAsync(CancellationToken token)
        {
            await UniTask.WhenAll(
                SlideTitleLabelsOutAsync(token),
                base.OnPanelOutAsync(token)
            );
        }

        private async UniTask SlideTitleLabelsOutAsync(CancellationToken token)
        {
            if (titleLabelRects == null || titleLabelRects.Length == 0) return;

            var count = titleLabelRects.Length;
            var starts = new Vector2[count];
            var targets = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                if (titleLabelRects[i] == null) continue;
                starts[i] = titleLabelRects[i].anchoredPosition;
                var dir = i == 0 ? -1f : (i == 1 ? 1f : 0f);
                targets[i] = starts[i] + new Vector2(outSlideDistance * dir, 0f);
            }

            if (outSlideDuration <= 0f)
            {
                for (var i = 0; i < count; i++)
                    if (titleLabelRects[i] != null)
                        titleLabelRects[i].anchoredPosition = targets[i];
                return;
            }

            var elapsed = 0f;
            while (elapsed < outSlideDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / outSlideDuration);
                for (var i = 0; i < count; i++)
                    if (titleLabelRects[i] != null)
                        titleLabelRects[i].anchoredPosition = Vector2.Lerp(starts[i], targets[i], t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            for (var i = 0; i < count; i++)
                if (titleLabelRects[i] != null)
                    titleLabelRects[i].anchoredPosition = targets[i];
        }
    }
}