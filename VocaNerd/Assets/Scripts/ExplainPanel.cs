using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace VocaNerd
{
    public class ExplainPanel : PanelBase
    {
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoDisplay;
        [SerializeField] private Button playButton;
        [SerializeField] private Button backButton;
        [SerializeField] private SelectionIndicator selectionIndicator;

        [Header("Animated Rects")]
        [SerializeField] private RectTransform descriptionTextRect;
        [SerializeField] private RectTransform videoDisplayRect;
        [SerializeField] private RectTransform playButtonRect;
        [SerializeField] private RectTransform backButtonRect;

        public RectTransform DescriptionTextRect => descriptionTextRect;
        public RectTransform VideoDisplayRect => videoDisplayRect;
        public RectTransform PlayButtonRect => playButtonRect;
        public RectTransform BackButtonRect => backButtonRect;

        private MiniGameData _current;
        private Vector2 _descriptionRestingPos;
        private bool _descriptionRestingCaptured;
        private readonly UniTaskCompletionSource _closedTcs = new UniTaskCompletionSource();

        public UniTask Closed => _closedTcs.Task;

        private void OnDestroy()
        {
            _closedTcs.TrySetResult();
        }

        protected override void Awake()
        {
            base.Awake();
            playButton.onClick.AddListener(OnPlay);
            backButton.onClick.AddListener(OnBack);
            if (descriptionTextRect != null)
            {
                _descriptionRestingPos = descriptionTextRect.anchoredPosition;
                _descriptionRestingCaptured = true;
            }
        }

        public void Bind(MiniGameData data)
        {
            _current = data;
            descriptionText.text = data.Description;
        }

        public override async UniTask SetupAsync(CancellationToken token)
        {
            if (_current == null || videoPlayer == null) return;

            videoPlayer.Stop();
            videoPlayer.clip = _current.VideoClip;
            videoPlayer.isLooping = true;
            videoPlayer.Prepare();
            await UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: token);

            if (videoDisplay != null && videoPlayer.targetTexture != null)
                videoDisplay.texture = videoPlayer.targetTexture;

            videoPlayer.Play();
        }

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            FocusDefaultSelected();
            canvasGroup.alpha = 1f;
            if (selectionIndicator != null) selectionIndicator.Show();

            if (descriptionTextRect == null) return;

            var target = _descriptionRestingCaptured
                ? _descriptionRestingPos
                : descriptionTextRect.anchoredPosition;
            var offscreen = target + new Vector2(GetOffscreenSlideX(), 0f);
            descriptionTextRect.anchoredPosition = offscreen;

            var duration = fadeDuration;
            if (duration <= 0f)
            {
                descriptionTextRect.anchoredPosition = target;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                descriptionTextRect.anchoredPosition = Vector2.LerpUnclamped(offscreen, target, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            descriptionTextRect.anchoredPosition = target;
        }

        protected override async UniTask OnPanelOutAsync(CancellationToken token)
        {
            if (descriptionTextRect == null) return;

            var start = descriptionTextRect.anchoredPosition;
            var target = start + new Vector2(GetOffscreenSlideX(), 0f);

            var duration = fadeDuration;
            if (duration <= 0f)
            {
                descriptionTextRect.anchoredPosition = target;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = t * t * t;
                descriptionTextRect.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            descriptionTextRect.anchoredPosition = target;
        }

        private float GetOffscreenSlideX()
        {
            var panelRt = (RectTransform)transform;
            var panelWidth = panelRt.rect.width;
            var descWidth = descriptionTextRect.rect.width;
            if (panelWidth <= 0f) return descWidth > 0f ? descWidth + 400f : 1200f;
            return (panelWidth * 0.5f) + (descWidth * 0.5f) + 40f;
        }

        private void OnPlay()
        {
            if (IsAnimating) return;
            if (_current == null) return;
            var data = _current;
            ScreenController.Instance.ShowAsync(
                ScreenType.MiniGame,
                go => go.GetComponentInChildren<MiniGamePanel>().Bind(data)
            ).Forget();
        }

        private void OnBack()
        {
            if (IsAnimating) return;
            CloseAsync().Forget();
        }

        private async UniTaskVoid CloseAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                await PanelOutAsync(token);
            }
            catch (System.OperationCanceledException) { }
            if (this != null) Destroy(gameObject);
        }
    }
}
