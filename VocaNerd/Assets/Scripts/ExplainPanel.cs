using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace VocaNerd
{
    /// <summary>
    /// 動画を流す説明画面。ミニゲームごとに prefab を分けて、どの動画を流すかを
    /// この component 上で指定する。In / Out のスライドと Play / Back の配線は
    /// <see cref="ExplainPanelBase"/> 側。
    ///
    /// 動画の準備 (Prepare) は SetupAsync で先に始めておき、再生は In が終わってから。
    /// </summary>
    public class ExplainPanel : ExplainPanelBase
    {
        private const float VideoPrepareTimeoutSeconds = 3f;

        [Header("Video")]
        [Tooltip("再生に使う VideoPlayer")]
        [SerializeField] private VideoPlayer videoPlayer;
        [Tooltip("VideoPlayer の targetTexture を映す RawImage")]
        [SerializeField] private RawImage videoDisplay;
#if !UNITY_WEBGL || UNITY_EDITOR
        [Tooltip("再生する動画。WebGL では使われず videoFileName 側が使われる")]
        [SerializeField] private VideoClip videoClip;
#endif
        [Tooltip("StreamingAssets から再生するときのファイル名 (WebGL 用 / VideoClip 未設定時のフォールバック)")]
        [SerializeField] private string videoFileName;

        public string VideoFileName => videoFileName;

        private CancellationTokenSource _videoPrepareCts;

        protected override void OnDestroy()
        {
            CancelVideoPrepare();
            if (videoPlayer != null)
                videoPlayer.errorReceived -= OnVideoError;
            base.OnDestroy();
        }

        public override UniTask SetupAsync(CancellationToken token)
        {
            if (videoPlayer == null)
                return UniTask.CompletedTask;

            CancelVideoPrepare();
            videoPlayer.Stop();
            if (!ConfigureVideoSource())
            {
                Debug.LogWarning($"[ExplainPanel] Video is not configured: {name}");
                return UniTask.CompletedTask;
            }

            videoPlayer.isLooping = true;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.errorReceived += OnVideoError;

            if (videoDisplay != null && videoPlayer.targetTexture != null)
                videoDisplay.texture = videoPlayer.targetTexture;

            // 準備だけ先に始めておく。再生開始は In が終わってから (OnAfterPanelInAsync)。
            _videoPrepareCts = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                this.GetCancellationTokenOnDestroy()
            );
            videoPlayer.Prepare();
            return UniTask.CompletedTask;
        }

        protected override async UniTask OnAfterPanelInAsync(CancellationToken token)
        {
            if (videoPlayer == null || !HasVideoSource())
                return;

            var videoLabel = GetVideoLabel();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                _videoPrepareCts?.Token ?? this.GetCancellationTokenOnDestroy()
            );

            try
            {
                var elapsed = 0f;
                while (!videoPlayer.isPrepared && elapsed < VideoPrepareTimeoutSeconds)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cts.Token);
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
            if (videoClip != null)
            {
                videoPlayer.source = VideoSource.VideoClip;
                videoPlayer.url = string.Empty;
                videoPlayer.clip = videoClip;
                return true;
            }

            return ConfigureVideoUrlSource();
#endif
        }

        private bool ConfigureVideoUrlSource()
        {
            var videoUrl = GetVideoUrl(videoFileName);
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
    }
}
