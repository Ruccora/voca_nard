using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd
{
    /// <summary>
    /// ゲーム開始前の説明画面の基底。説明画面はミニゲームごとに prefab を分けて持つ前提で、
    /// ここが共有するのは次の 2 つだけ。
    ///
    ///   1. In / Out のスライドアニメーション
    ///      In  = 左から / 右から / 下から の 3 object が同じ尺で定位置に入ってくる
    ///      Out = その逆 (定位置から画面外へ戻る)
    ///   2. Play でミニゲームへ遷移 / Back で閉じる という配線
    ///
    /// 説明文・動画・レイアウトなど中身は派生クラスと prefab 側で持つ。
    /// In が終わってから何かを始めたい場合 (動画再生など) は
    /// <see cref="OnAfterPanelInAsync"/> を override する。
    /// </summary>
    public abstract class ExplainPanelBase : PanelBase
    {
        [Header("Launch")]
        [Tooltip("Play で起動するミニゲームの prefab")]
        [SerializeField] private GameObject miniGamePrefab;
        [Tooltip("このミニゲーム中に流す BGM キー (BgmKey の定数)。空なら直前の BGM を継続")]
        [SerializeField] private string bgmKey;

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button backButton;
        [SerializeField] private SelectionIndicator selectionIndicator;

        [Header("In / Out Slide")]
        [Tooltip("左から右へ入ってくる object")]
        [SerializeField] private RectTransform slideFromLeftRect;
        [Tooltip("右から左へ入ってくる object")]
        [SerializeField] private RectTransform slideFromRightRect;
        [Tooltip("下から上へ入ってくる object")]
        [SerializeField] private RectTransform slideFromBottomRect;
        [Tooltip("画面外へ逃がすときに足す余白 (px)")]
        [SerializeField] private float slideMargin = 40f;

        public GameObject MiniGamePrefab => miniGamePrefab;
        public string BgmKey => bgmKey;

        private readonly UniTaskCompletionSource _closedTcs = new UniTaskCompletionSource();

        /// <summary>Back で閉じられるまで待つ。画面ごと破棄された場合はキャンセルになる。</summary>
        public UniTask Closed => _closedTcs.Task;

        private RectTransform[] _slideRects;
        private Vector2[] _slideDirs;    // 画面外へ逃がす向き (単位ベクトル)
        private Vector2[] _slideHome;    // prefab で置かれていた定位置

        protected override void Awake()
        {
            base.Awake();
            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (backButton != null) backButton.onClick.AddListener(OnBack);

            _slideRects = new[] { slideFromLeftRect, slideFromRightRect, slideFromBottomRect };
            _slideDirs = new[] { Vector2.left, Vector2.right, Vector2.down };
            _slideHome = new Vector2[_slideRects.Length];
            for (var i = 0; i < _slideRects.Length; i++)
            {
                if (_slideRects[i] != null) _slideHome[i] = _slideRects[i].anchoredPosition;
            }
        }

        protected virtual void OnDestroy()
        {
            // 「戻る」で閉じた場合は CloseAsync が既に完了させている。ここに来るのは
            // 画面遷移などで丸ごと破棄されたケースなので、待ち側には成功ではなく
            // キャンセルを通知する。成功にすると破棄処理の最中に待ち側の続きが
            // 同期実行され、破棄中の GameObject を SetActive してエラーになる。
            _closedTcs.TrySetCanceled();
        }

        // -------- In / Out --------

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            canvasGroup.alpha = 1f;
            await PlaySlideAsync(inward: true, token);

            // interactable が有効になる直前に選択を確定させる (フェード中に選択すると外れることがある)
            FocusDefaultSelected();
            if (selectionIndicator != null) selectionIndicator.Show();

            await OnAfterPanelInAsync(token);
        }

        protected override UniTask OnPanelOutAsync(CancellationToken token)
            => PlaySlideAsync(inward: false, token);

        /// <summary>In のスライドが終わった後に呼ばれる。動画の再生開始などはここで。</summary>
        protected virtual UniTask OnAfterPanelInAsync(CancellationToken token) => UniTask.CompletedTask;

        // 3 つの object を同じ尺で動かす。inward = 画面外 → 定位置 / outward = 定位置 → 画面外。
        private async UniTask PlaySlideAsync(bool inward, CancellationToken token)
        {
            var offscreen = new Vector2[_slideRects.Length];
            for (var i = 0; i < _slideRects.Length; i++)
            {
                var rt = _slideRects[i];
                if (rt == null) continue;
                offscreen[i] = _slideHome[i] + OffscreenOffset(rt, _slideDirs[i]);
                if (inward) rt.anchoredPosition = offscreen[i];
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
                    var eased = inward ? EaseOutCubic(t) : EaseInCubic(t);
                    for (var i = 0; i < _slideRects.Length; i++)
                    {
                        var rt = _slideRects[i];
                        if (rt == null) continue;
                        var from = inward ? offscreen[i] : _slideHome[i];
                        var to = inward ? _slideHome[i] : offscreen[i];
                        rt.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }

            for (var i = 0; i < _slideRects.Length; i++)
            {
                var rt = _slideRects[i];
                if (rt == null) continue;
                rt.anchoredPosition = inward ? _slideHome[i] : offscreen[i];
            }
        }

        // その object が画面外に完全に出るまでの距離を dir 方向に取る。
        private Vector2 OffscreenOffset(RectTransform rt, Vector2 dir)
        {
            var panel = (RectTransform)transform;
            if (Mathf.Abs(dir.y) > Mathf.Abs(dir.x))
            {
                var panelH = panel.rect.height;
                var h = rt.rect.height;
                var dy = panelH > 0f ? panelH * 0.5f + h * 0.5f + slideMargin
                                     : (h > 0f ? h + 400f : 900f);
                return dir * dy;
            }

            var panelW = panel.rect.width;
            var w = rt.rect.width;
            var dx = panelW > 0f ? panelW * 0.5f + w * 0.5f + slideMargin
                                 : (w > 0f ? w + 400f : 1200f);
            return dir * dx;
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInCubic(float t) => t * t * t;

        // -------- Play / Back --------

        private void OnPlay()
        {
            if (IsAnimating) return;
            if (miniGamePrefab == null)
            {
                Debug.LogWarning($"[{GetType().Name}] miniGamePrefab is not set: {name}");
                return;
            }

            var prefab = miniGamePrefab;
            var bgm = bgmKey;
            ScreenController.Instance.ShowAsync(
                ScreenType.MiniGame,
                go => go.GetComponentInChildren<MiniGamePanel>().Bind(prefab, bgm),
                fadeToBlack: true
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
            catch (OperationCanceledException) { }
            _closedTcs.TrySetResult();
            if (this != null) Destroy(gameObject);
        }
    }
}
