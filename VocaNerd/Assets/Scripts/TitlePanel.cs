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
            await base.OnPanelInAsync(token);
            if (selectionIndicator != null) selectionIndicator.Show();
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