using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VocaNerd
{
    /// <summary>
    /// ゲーム開始前の説明画面の基底。説明画面はミニゲームごとに prefab を分けて持つ前提で、
    /// ここが共有するのは次の 2 つだけ。
    ///
    ///   1. In / Out のアニメーション
    ///      In  = 画面全体が FadeIn で入り、その後で
    ///            左から / 右から / 下から の 3 object が同じ尺で定位置に入ってくる
    ///      Out = スライドだけ逆再生する (定位置から画面外へ戻る)
    ///   2. A (次へ) でミニゲームへ遷移 / B (戻る) で閉じる という入力
    ///
    /// 操作は「次へ」「戻る」の 2 つだけなので UI の Button やカーソルは持たず、
    /// 1P の入力 (パッドの A/B とキーボード) を直接拾う。
    ///
    /// 説明文・動画・レイアウトなど中身は派生クラスと prefab 側で持つ。
    /// In が終わってから何かを始めたい場合 (動画再生など) は
    /// <see cref="OnAfterPanelInAsync"/> を override する。
    /// </summary>
    public abstract class ExplainPanelBase : PanelBase
    {
        [Header("Launch")]
        [Tooltip("A (次へ) で起動するミニゲームの prefab")]
        [SerializeField] private GameObject miniGamePrefab;
        [Tooltip("このミニゲーム中に流す BGM キー (BgmKey の定数)。空なら直前の BGM を継続")]
        [SerializeField] private string bgmKey;

        [SerializeField] private GameObject black;

        [Header("In / Out Slide")]
        [Tooltip("スライドの移動尺 (秒)。0 で移動なし (いきなり定位置)。FadeIn の尺は fadeDuration 側")]
        [SerializeField] private float slideDuration = 0.5f;
        [Tooltip("左から右へ入ってくる object")]
        [SerializeField] private RectTransform slideFromLeftRect;
        [Tooltip("右から左へ入ってくる object")]
        [SerializeField] private RectTransform slideFromRightRect;
        [Tooltip("下から上へ入ってくる object")]
        [SerializeField] private RectTransform slideFromBottomRect;
        [Tooltip("画面外へ逃がすときに足す余白 (px)")]
        [SerializeField] private float slideMargin = 40f;

        [Header("Presentation")]
        [Tooltip("ルール枠で流す演出。In が終わってから再生し、Out で止める。未設定なら何もしない")]
        [SerializeField] private ExplainPresentation presentation;

        public GameObject MiniGamePrefab => miniGamePrefab;
        public string BgmKey => bgmKey;

        private readonly UniTaskCompletionSource _closedTcs = new UniTaskCompletionSource();

        /// <summary>Back で閉じられるまで待つ。画面ごと破棄された場合はキャンセルになる。</summary>
        public UniTask Closed => _closedTcs.Task;

        private RectTransform[] _slideRects;
        private Vector2[] _slideDirs;    // 画面外へ逃がす向き (単位ベクトル)
        private Vector2[] _slideHome;    // prefab で置かれていた定位置

        private InputAction _nextAction;
        private InputAction _backAction;

        protected override void Awake()
        {
            base.Awake();

            // 操作できるのは 1P だけ。2P のパッドから A/B を押しても無視する (ResultInput と同じ扱い)。
            _nextAction = PlayerInputAction.Make("ExplainNext", GamepadButtons.A, "<Keyboard>/enter", "<Keyboard>/space");
            PlayerInputAction.OnPress(_nextAction, 1, OnNext);
            _backAction = PlayerInputAction.Make("ExplainBack", GamepadButtons.B, "<Keyboard>/escape", "<Keyboard>/backspace");
            PlayerInputAction.OnPress(_backAction, 1, OnBack);

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
            SetInputEnabled(false);
            _nextAction?.Dispose();
            _nextAction = null;
            _backAction?.Dispose();
            _backAction = null;

            // 「戻る」で閉じた場合は CloseAsync が既に完了させている。ここに来るのは
            // 画面遷移などで丸ごと破棄されたケースなので、待ち側には成功ではなく
            // キャンセルを通知する。成功にすると破棄処理の最中に待ち側の続きが
            // 同期実行され、破棄中の GameObject を SetActive してエラーになる。
            _closedTcs.TrySetCanceled();
        }

        // -------- In / Out --------

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            canvasGroup.alpha = 0f;

            // In が始まる前に、In 中に見えている状態を作らせる (動画の巻き戻しなど)。
            OnBeforePanelIn();

            // 選択状態を外しておく。残っていると EventSystem の Submit で裏の
            // SelectPanel のボタンが押せてしまうので、入力はここの A/B に任せる。
            ClearFocus();

            // スライドする object は FadeIn より前に画面外へ逃がしておく。
            // 定位置のまま一緒にフェードインすると「その後に入ってくる」演出にならない。
            var offscreen = CalcOffscreen();
            SetPositions(offscreen);

            black.SetActive(this);
            
            // まず画面全体が FadeIn で入り、その後でスライドが始まる
            await FadeAsync(canvasGroup, 0f, 1f, fadeDuration, token);
            await PlaySlideAsync(inward: true, offscreen, token);

            // ルール枠の演出はスライドが終わってから回し始める
            if (presentation != null) presentation.Play(token);

            await OnAfterPanelInAsync(token);

            await UniTask.DelayFrame(1);

            black.gameObject.SetActive(false);
            // 受付開始は In が終わってから (IsAnimating が下りるタイミングと揃える)
            SetInputEnabled(true);
            
        }

        protected override UniTask OnPanelOutAsync(CancellationToken token)
        {
            SetInputEnabled(false);
            if (presentation != null) presentation.Stop();
            return PlaySlideAsync(inward: false, CalcOffscreen(), token);
        }

        /// <summary>
        /// In のアニメーションが始まる直前に呼ばれる。動画を頭出しして止めておくなど、
        /// In の最中に見えていてほしい状態を作るのはここで。
        /// </summary>
        protected virtual void OnBeforePanelIn() { }

        /// <summary>In のスライドが終わった後に呼ばれる。動画の再生開始などはここで。</summary>
        protected virtual UniTask OnAfterPanelInAsync(CancellationToken token) => UniTask.CompletedTask;

        // 3 つの object を同じ尺 (slideDuration) で動かす。
        // inward = 画面外 → 定位置 / outward = 定位置 → 画面外。
        private async UniTask PlaySlideAsync(bool inward, Vector2[] offscreen, CancellationToken token)
        {
            if (slideDuration > 0f)
            {
                var elapsed = 0f;
                while (elapsed < slideDuration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / slideDuration);
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

            if (!inward)
            {
                await FadeAsync(canvasGroup, 1f, 0f, fadeDuration, token);
            }


            SetPositions(inward ? _slideHome : offscreen);
        }

        // 各 object が画面外に出きる位置 (定位置 + 逃がす向きのオフセット)。
        private Vector2[] CalcOffscreen()
        {
            var offscreen = new Vector2[_slideRects.Length];
            for (var i = 0; i < _slideRects.Length; i++)
            {
                var rt = _slideRects[i];
                if (rt == null) continue;
                offscreen[i] = _slideHome[i] + OffscreenOffset(rt, _slideDirs[i]);
            }
            return offscreen;
        }

        private void SetPositions(Vector2[] positions)
        {
            for (var i = 0; i < _slideRects.Length; i++)
            {
                var rt = _slideRects[i];
                if (rt == null) continue;
                rt.anchoredPosition = positions[i];
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

        // -------- Next / Back --------

        private void SetInputEnabled(bool value)
        {
            if (value)
            {
                _nextAction?.Enable();
                _backAction?.Enable();
            }
            else
            {
                _nextAction?.Disable();
                _backAction?.Disable();
            }
        }

        private void OnNext()
        {
            if (IsAnimating) return;
            if (miniGamePrefab == null)
            {
                Debug.LogWarning($"[{GetType().Name}] miniGamePrefab is not set: {name}");
                return;
            }

            SetInputEnabled(false);   // 遷移が走る前に二重入力を止める
            Audio.PlaySE(SeKey.Decide);

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
            SetInputEnabled(false);
            Audio.PlaySE(SeKey.Cancel);
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