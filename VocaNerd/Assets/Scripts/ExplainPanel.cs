using System;
using System.IO;
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
        private const float VideoPrepareTimeoutSeconds = 3f;

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
        private CancellationTokenSource _videoPrepareCts;
        private Vector2 _descriptionRestingPos;
        private bool _descriptionRestingCaptured;
        private readonly UniTaskCompletionSource _closedTcs = new UniTaskCompletionSource();

        public UniTask Closed => _closedTcs.Task;

        private void OnDestroy()
        {
            CancelVideoPrepare();
            if (videoPlayer != null)
                videoPlayer.errorReceived -= OnVideoError;

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

        public override UniTask SetupAsync(CancellationToken token)
        {
            if (_current == null || videoPlayer == null)
                return UniTask.CompletedTask;

            CancelVideoPrepare();
            videoPlayer.Stop();
            if (!ConfigureVideoSource())
            {
                Debug.LogWarning($"[ExplainPanel] Video is not configured: {_current.name}");
                return UniTask.CompletedTask;
            }

            videoPlayer.isLooping = true;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.errorReceived += OnVideoError;

            if (videoDisplay != null && videoPlayer.targetTexture != null)
                videoDisplay.texture = videoPlayer.targetTexture;

            _videoPrepareCts = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                this.GetCancellationTokenOnDestroy()
            );
            PrepareAndPlayVideoAsync(_videoPrepareCts.Token).Forget();
            return UniTask.CompletedTask;
        }

        private async UniTaskVoid PrepareAndPlayVideoAsync(CancellationToken token)
        {
            if (videoPlayer == null || !HasVideoSource())
                return;

            var videoLabel = GetVideoLabel();

            try
            {
                videoPlayer.Prepare();

                var elapsed = 0f;
                while (!videoPlayer.isPrepared && elapsed < VideoPrepareTimeoutSeconds)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                if (videoPlayer == null)
                    return;

                if (!videoPlayer.isPrepared)
                {
                    Debug.LogWarning($"[ExplainPanel] Video prepare timed out: {videoLabel}");
                    return;
                }

                videoPlayer.Play();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ExplainPanel] Video playback failed: {videoLabel}\n{ex}");
            }
        }

        private void CancelVideoPrepare()
        {
            if (_videoPrepareCts == null)
                return;

            _videoPrepareCts.Cancel();
            _videoPrepareCts.Dispose();
            _videoPrepareCts = null;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            var videoLabel = source != null ? GetVideoLabel(source) : string.Empty;
            Debug.LogWarning($"[ExplainPanel] Video error: {videoLabel} ({message})");
        }

        private bool ConfigureVideoSource()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return ConfigureVideoUrlSource();
#else
            if (_current.VideoClip != null)
            {
                videoPlayer.source = VideoSource.VideoClip;
                videoPlayer.url = string.Empty;
                videoPlayer.clip = _current.VideoClip;
                return true;
            }

            return ConfigureVideoUrlSource();
#endif
        }

        private bool ConfigureVideoUrlSource()
        {
            var videoUrl = GetVideoUrl(_current.VideoFileName);
            if (string.IsNullOrEmpty(videoUrl))
                return false;

            videoPlayer.source = VideoSource.Url;
            videoPlayer.clip = null;
            videoPlayer.url = videoUrl;
            return true;
        }

        private bool HasVideoSource()
        {
            return videoPlayer.source == VideoSource.Url
                ? !string.IsNullOrEmpty(videoPlayer.url)
                : videoPlayer.clip != null;
        }

        private string GetVideoLabel()
        {
            return GetVideoLabel(videoPlayer);
        }

        private static string GetVideoLabel(VideoPlayer source)
        {
            if (source == null)
                return string.Empty;

            return source.source == VideoSource.Url
                ? source.url
                : source.clip != null ? source.clip.name : string.Empty;
        }

        private static string GetVideoUrl(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            fileName = fileName.Trim();
            if (fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
                return null;

            var safeFileName = Path.GetFileName(fileName);
            if (string.IsNullOrEmpty(safeFileName))
                return null;

#if UNITY_EDITOR
            var editorPath = Path.Combine(Application.dataPath, "Video", safeFileName);
            if (File.Exists(editorPath))
                return editorPath;
#endif

            return $"{Application.streamingAssetsPath}/{Uri.EscapeDataString(safeFileName)}";
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
