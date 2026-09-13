using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    public enum ScreenType
    {
        Title,
        Select,
        MiniGame,
    }

    public class ScreenController : MonoBehaviour
    {
        [Serializable]
        private class ScreenEntry
        {
            public ScreenType type;
            public GameObject prefab;

            [Tooltip("この画面へ遷移したときに流す BGM キー（BgmKey の定数）。空なら BGM を触らない")]
            public string bgmKey;
        }

        [SerializeField] private ScreenEntry[] screens;
        [SerializeField] private RectTransform root;
        [SerializeField] private float bgmFadeDuration = 0.8f;

        [Header("Transition Fade")]
        [Tooltip("暗転に使う全画面の黒 Image。未設定なら実行時に自動生成する")]
        [SerializeField] private Image transitionFadeImage;

        [Tooltip("ShowAsync(fadeToBlack: true) のときの暗転秒数 (画面 → 黒)")]
        [SerializeField] private float fadeToBlackDuration = 0.5f;

        [Tooltip("パネル入れ替え後の明転秒数 (黒 → 画面)")]
        [SerializeField] private float fadeFromBlackDuration = 3f;

        [Tooltip("アプリ全体のフレームレート上限。0 以下なら変更しない")]
        [SerializeField] private int targetFrameRate = 24;

        [Header("Resolution")]
        [Tooltip("解像度を固定する。ウィンドウでもフルスクリーンでもこのサイズで描画する")]
        [SerializeField] private bool lockResolution = true;
        [SerializeField] private int lockedWidth = 1600;
        [SerializeField] private int lockedHeight = 1200;
        [Tooltip("フルスクリーン時のモード。ExclusiveFullScreen はディスプレイ側の解像度を切り替え、" +
                 "FullScreenWindow は固定解像度で描いてディスプレイに引き伸ばす")]
        [SerializeField] private FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;

        private bool _wasFullScreen;
        private GameObject _current;
        private CancellationTokenSource _transitionCts;

        // 明転の完了を待たせるための signal。暗転を始めた時点で作り、明転しきったら完了させる。
        private UniTaskCompletionSource _fadeCompletion;

        public static ScreenController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ApplyTargetFrameRate();
            ApplyLockedResolution();
        }

        private void ApplyTargetFrameRate()
        {
            if (targetFrameRate <= 0)
                return;

            // vSync が有効だとリフレッシュレート基準になり targetFrameRate が無視される
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
        }

        /// <summary>
        /// 描画解像度を lockedWidth x lockedHeight に固定する。
        /// フルスクリーンに切り替えられてもディスプレイ解像度に引きずられないよう、
        /// 切り替わりを検知して掛け直す (<see cref="Update"/>)。
        ///
        /// エディタの Game ビューは解像度をここから変えても意味がないので何もしない。
        /// </summary>
        private void ApplyLockedResolution()
        {
            if (!lockResolution) return;
            if (lockedWidth <= 0 || lockedHeight <= 0) return;
#if UNITY_EDITOR
            return;
#else
            _wasFullScreen = Screen.fullScreen;
            var mode = Screen.fullScreen ? fullScreenMode : FullScreenMode.Windowed;
            if (Screen.width == lockedWidth && Screen.height == lockedHeight && Screen.fullScreenMode == mode)
                return;

            Screen.SetResolution(lockedWidth, lockedHeight, mode);
#endif
        }

        private void Update()
        {
            // フルスクリーン切り替え (Cmd+F など) の直後は解像度がディスプレイ側に戻されるので掛け直す
            if (!lockResolution) return;
            if (Screen.fullScreen == _wasFullScreen) return;
            ApplyLockedResolution();
        }

        private void Start()
        {
            ShowAsync(ScreenType.Title).Forget();
        }

        public async UniTask ShowAsync(ScreenType next, Action<GameObject> onInstantiated = null, bool fadeToBlack = false, CancellationToken cancellationToken = default)
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            _transitionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _transitionCts.Token;

            // 0) 暗転。以降パネルの入れ替えは黒の裏で行う
            var fade = fadeToBlack ? EnsureTransitionFade() : null;
            _fadeCompletion = fade != null ? new UniTaskCompletionSource() : null;
            if (fade != null) await FadeToBlackAsync(fade, token);

            var outgoing = _current;
            var outPanel = outgoing != null ? outgoing.GetComponent<PanelBase>() : null;

            // 1) 新Panel 生成 (旧 Panel はまだ生きている)
            var entry = FindEntry(next);
            var prefab = entry != null ? entry.prefab : null;
            if (prefab == null) throw new InvalidOperationException($"Prefab not registered for screen: {next}");
            var instance = Instantiate(prefab, root != null ? root : (RectTransform)transform);
            _current = instance;

            // 生成した Panel が黒より後ろの兄弟になるよう、黒を最前面に持ち直す
            if (fade != null) fade.rectTransform.SetAsLastSibling();

            onInstantiated?.Invoke(instance);

            // BGM は遷移演出と並行してクロスフェードさせる（待たない）。
            // 空キーなら据え置き = Panel 側 (MiniGamePanel など) が自分で決める。
            Audio.PlayBgm(entry.bgmKey, bgmFadeDuration);

            var inPanel = instance.GetComponent<PanelBase>();
            if (inPanel != null)
            {
                await inPanel.SetupAsync(token);
            }

            // 2) 旧 Panel の Out 前フック
            try
            {
                if (outPanel != null) await outPanel.PanelPreOutAsync(token);
            }
            catch (OperationCanceledException) { }

            // 3) Out と In を並列実行（クロスフェード/クロススライド）
            try
            {
                await UniTask.WhenAll(
                    outPanel != null ? outPanel.PanelOutAsync(token) : UniTask.CompletedTask,
                    inPanel != null ? inPanel.PanelInAsync(token) : UniTask.CompletedTask
                );
            }
            catch (OperationCanceledException) { }

            // 4) 旧 Panel を破棄
            if (outgoing != null) Destroy(outgoing);

            // 5) 明転。待っている画面 (WaitForTransitionFadeAsync) はここが終わってから動き出す。
            if (fade != null)
            {
                try
                {
                    await FadeFromBlackAsync(fade, token);
                }
                finally
                {
                    // 途中でキャンセルされても待ち側を止めたままにしない
                    _fadeCompletion?.TrySetResult();
                }
            }
        }

        /// <summary>
        /// 遷移の明転が終わるまで待つ。暗転を伴わない遷移や、既に明転済みなら即座に返る。
        /// 画面側は「自分が見えている」状態になってから演出を始めたいときにこれを await する。
        /// </summary>
        public UniTask WaitForTransitionFadeAsync(CancellationToken cancellationToken = default)
        {
            var completion = _fadeCompletion;
            return completion != null
                ? completion.Task.AttachExternalCancellation(cancellationToken)
                : UniTask.CompletedTask;
        }

        // -------- Transition fade --------

        private async UniTask FadeToBlackAsync(Image fade, CancellationToken token)
        {
            fade.rectTransform.SetAsLastSibling();
            var color = fade.color;
            color.a = 0f;
            fade.color = color;
            fade.enabled = true;

            var elapsed = 0f;
            while (elapsed < fadeToBlackDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Clamp01(elapsed / fadeToBlackDuration);
                fade.color = color;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            color.a = 1f;
            fade.color = color;
        }

        private async UniTask FadeFromBlackAsync(Image fade, CancellationToken token)
        {
            fade.rectTransform.SetAsLastSibling();
            var color = fade.color;

            try
            {
                var elapsed = 0f;
                while (elapsed < fadeFromBlackDuration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    color.a = 1f - Mathf.Clamp01(elapsed / fadeFromBlackDuration);
                    fade.color = color;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                if (fade != null) fade.enabled = false;
            }
        }

        // 全画面の黒 Image を用意する (未設定なら生成し、最前面に置く)
        private Image EnsureTransitionFade()
        {
            if (transitionFadeImage != null) return transitionFadeImage;
            var parent = root != null ? root : transform as RectTransform;
            if (parent == null) return null;

            var go = new GameObject("TransitionFade");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            transitionFadeImage = go.AddComponent<Image>();
            transitionFadeImage.color = Color.black;
            transitionFadeImage.raycastTarget = true; // 暗転中の誤操作を防ぐ
            transitionFadeImage.enabled = false;
            return transitionFadeImage;
        }

        private ScreenEntry FindEntry(ScreenType type)
        {
            foreach (var entry in screens)
            {
                if (entry.type == type) return entry;
            }
            return null;
        }

        private void OnDestroy()
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
