using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VocaNerd
{
    public class QuickDrawGame : PanelBase
    {
        private enum Phase
        {
            Idle,
            Opening,
            Intro,
            Waiting,
            Ready,
            Reaction,
            Winner,
            WaitForExit,
            Exiting,
        }

        private struct PressResult
        {
            public int Winner;
            public bool Foul;
        }

        [Header("View")]
        [SerializeField] private CanvasGroup introGroup;
        [SerializeField] private TMP_Text introText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image targetImage;
        [SerializeField] private CanvasGroup resultGroup;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button playAgainButton;

        [Header("Players")]
        [SerializeField] private RectTransform player1Character;
        [SerializeField] private RectTransform player2Character;
        [SerializeField] private SpriteAnimation player1WinAnim; // 勝利アニメ (loop=false 推奨)
        [SerializeField] private SpriteAnimation player2WinAnim;
        [SerializeField] private float moveToCenterDuration = 0.5f;
        [SerializeField] private float centerX = 0f;
        [SerializeField] private float characterEnterDuration = 0.1f; // 開始演出後のバン!と登場
        [SerializeField] private float characterEnterOffsetX = 1400f; // 画面外スタートの横距離
        [SerializeField] private float enterOvershoot = 1.7f;         // バン! のオーバーシュート量 (0で無し)
        [SerializeField] private float openingHoldDuration = 1f;      // 登場後の待機
        [SerializeField] private float characterExitDuration = 0.25f; // 外へはける

        [Header("Opening")]
        // 別 prefab で作った開始演出を子として当てこんで参照する (未設定なら演出なし)
        [SerializeField] private OpeningEffect openingEffect;

        [Header("Timing")]
        [SerializeField] private float minWait = 3f;
        [SerializeField] private float maxWait = 5f;
        [SerializeField] private float introDuration = 1.2f;
        [SerializeField] private float winnerDelay = 1.5f;
        [SerializeField] private float winnerUiDelay = 1.5f;

        private Phase _phase;

        public override bool CanAcceptBack => _phase == Phase.Winner || _phase == Phase.WaitForExit;
        private InputAction _p1Action;
        private InputAction _p2Action;
        private CancellationTokenSource _roundCts;
        private UniTaskCompletionSource _pressSignal;
        private PressResult _pressResult;
        private readonly System.Random _rng = new System.Random();
        private Vector2 _p1Home;
        private Vector2 _p2Home;
        private bool _isSetup;

        public override async UniTask SetupAsync(CancellationToken token)
        {
            if (_isSetup) return;
            _isSetup = true;

            _p1Action = new InputAction("Player1", InputActionType.Button);
            _p1Action.AddBinding("<Keyboard>/a");
            _p1Action.AddBinding("<Gamepad>/buttonSouth");
            _p1Action.performed += OnP1;

            _p2Action = new InputAction("Player2", InputActionType.Button);
            _p2Action.AddBinding("<Keyboard>/l");
            _p2Action.AddBinding("<Gamepad>/buttonSouth");
            _p2Action.performed += OnP2;

            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgain);

            // キャラの初期位置（左右）を記録。以後ラウンド毎にここへ戻す。
            if (player1Character != null) _p1Home = player1Character.anchoredPosition;
            if (player2Character != null) _p2Home = player2Character.anchoredPosition;

            ResetInitialView();

            if (openingEffect != null)
                await openingEffect.SetupAsync(token);
        }

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            _p1Action?.Enable();
            _p2Action?.Enable();

            await base.OnPanelInAsync(token);
            StartRound();
        }

        protected override async UniTask OnPanelOutAsync(CancellationToken token)
        {
            CancelRound();
            _p1Action?.Disable();
            _p2Action?.Disable();
            await base.OnPanelOutAsync(token);
        }

        private void OnDestroy()
        {
            CancelRound();
            _p1Action?.Dispose();
            _p2Action?.Dispose();
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgain);
        }

        private void OnPlayAgain()
        {
            if (IsAnimating) return;
            StartRound();
        }

        private void StartRound()
        {
            CancelRound();
            _roundCts = new CancellationTokenSource();
            RunRoundAsync(_roundCts.Token).Forget();
        }

        private void CancelRound()
        {
            _roundCts?.Cancel();
            _roundCts?.Dispose();
            _roundCts = null;
        }

        private async UniTaskVoid RunRoundAsync(CancellationToken token)
        {
            try
            {
                ResetRoundView();
                _pressResult = default;

                // 0) 開始演出 (READY? の前)
                await PlayOpeningEffectAsync(token);

                // 1) 開始演出
                await PlayIntroEffectAsync(token);

                // 2) 3~5 秒待機（この間に押されたらフォール）
                var reachedReveal = await PlayWaitAsync(token);

                if (reachedReveal)
                {
                    // 3) 何らかのボタン表示演出
                    await PlayRevealEffectAsync(token);
                    // 押されるまで待つ
                    await WaitForPressAsync(token);
                }

                // 4) 押下時演出 (勝者が中央へ移動)
                await PlayPressEffectAsync(_pressResult, token);

                // 4.3) 勝者の勝利アニメ (完了で戻る)
                await PlayWinAnimationAsync(_pressResult, token);

                // 4.5) 勝利フェーズへ移行する前に一拍待機
                if (winnerDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(winnerDelay), cancellationToken: token);

                // 5) 勝利者演出
                await PlayWinnerEffectAsync(_pressResult, token);

                // 6) 任意ボタン入力待ち
                await WaitForExitPressAsync(token);

                // 7) 抜ける演出
                await PlayExitEffectAsync(token);

                // 8) メニュー画面へ遷移
                if (ScreenController.Instance != null)
                    ScreenController.Instance.ShowAsync(ScreenType.Select).Forget();
            }
            catch (OperationCanceledException)
            {
            }
        }

        // -------- Stage 0: 開始演出 (READY? の前) --------
        private async UniTask PlayOpeningEffectAsync(CancellationToken token)
        {
            _phase = Phase.Opening;

            // 1) 斜め線を露出 (マスク端→中央)。表示したまま。
            if (openingEffect != null)
                await openingEffect.PlayAsync(token);

            // 2) 1P=左 / 2P=右 から バン! と登場
            await PlayCharacterEnterAsync(token);

            // 3) 1秒待機
            if (openingHoldDuration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(openingHoldDuration), cancellationToken: token);

            // 4) マスクが左右にはける + キャラも外へ (同時) → READY へ
            var maskExit = openingEffect != null ? openingEffect.ExitAsync(token) : UniTask.CompletedTask;
            var charExit = PlayCharacterExitAsync(token);
            await UniTask.WhenAll(maskExit, charExit);
        }

        // 1P=左 / 2P=右 から自ホーム位置へ登場
        private async UniTask PlayCharacterEnterAsync(CancellationToken token)
        {
            var t1 = player1Character != null
                ? MoveAnchoredXAsync(player1Character, _p1Home.x, characterEnterDuration, token, EaseOutBack)
                : UniTask.CompletedTask;
            var t2 = player2Character != null
                ? MoveAnchoredXAsync(player2Character, _p2Home.x, characterEnterDuration, token, EaseOutBack)
                : UniTask.CompletedTask;
            await UniTask.WhenAll(t1, t2);
        }

        // 1P=左 / 2P=右 の画面外へ退場
        private async UniTask PlayCharacterExitAsync(CancellationToken token)
        {
            var t1 = player1Character != null
                ? MoveAnchoredXAsync(player1Character, _p1Home.x - characterEnterOffsetX, characterExitDuration, token)
                : UniTask.CompletedTask;
            var t2 = player2Character != null
                ? MoveAnchoredXAsync(player2Character, _p2Home.x + characterEnterOffsetX, characterExitDuration, token)
                : UniTask.CompletedTask;
            await UniTask.WhenAll(t1, t2);
        }

        // -------- Stage 1: 開始演出 --------
        private async UniTask PlayIntroEffectAsync(CancellationToken token)
        {
            _phase = Phase.Intro;
            // TODO: 差し替え可能な開始演出。現状は READY? を sin フェード
            if (introGroup == null)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(introDuration), cancellationToken: token);
                return;
            }

            var elapsed = 0f;
            while (elapsed < introDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / introDuration);
                introGroup.alpha = Mathf.Sin(t * Mathf.PI);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            introGroup.alpha = 0f;
        }

        // -------- Stage 2: 3~5秒待機 (returns true if full wait elapsed, false if foul) --------
        private async UniTask<bool> PlayWaitAsync(CancellationToken token)
        {
            _phase = Phase.Waiting;
            if (statusText != null) statusText.text = "...";
            _pressSignal = new UniTaskCompletionSource();

            var wait = Mathf.Lerp(minWait, maxWait, (float)_rng.NextDouble());
            var timerTask = UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
            var pressTask = _pressSignal.Task;

            var winIndex = await UniTask.WhenAny(timerTask, pressTask);
            return winIndex == 0;
        }

        // -------- Stage 3: 秒数経過後のボタン表示演出 --------
        private async UniTask PlayRevealEffectAsync(CancellationToken token)
        {
            if (statusText != null) statusText.text = "!!!";
            if (targetImage != null) targetImage.enabled = true;
            // TODO: 表示演出（スケールイン・フラッシュ・SEなど）
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // Ready フェーズ: 押下待ち
        private async UniTask WaitForPressAsync(CancellationToken token)
        {
            _phase = Phase.Ready;
            _pressSignal = new UniTaskCompletionSource();
            await _pressSignal.Task.AttachExternalCancellation(token);
        }

        // -------- Stage 4: 押下時演出 --------
        private async UniTask PlayPressEffectAsync(PressResult result, CancellationToken token)
        {
            _phase = Phase.Reaction;
            if (targetImage != null) targetImage.enabled = false;
            if (statusText != null) statusText.text = string.Empty;

            // 早押しに成功した方(勝者)を中央へ移動
            var winnerChar = result.Winner == 1 ? player1Character
                : result.Winner == 2 ? player2Character
                : null;
            if (winnerChar != null)
                await MoveToCenterAsync(winnerChar, token);
            else
                await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        private UniTask MoveToCenterAsync(RectTransform character, CancellationToken token)
            => MoveAnchoredXAsync(character, centerX, moveToCenterDuration, token);

        // anchoredPosition の X だけを targetX へ (Y は保持)。easing 省略時は EaseOutCubic。
        private async UniTask MoveAnchoredXAsync(RectTransform rt, float targetX, float duration, CancellationToken token, Func<float, float> easing = null)
        {
            if (rt == null) return;
            easing ??= EaseOutCubic;
            var from = rt.anchoredPosition;
            var to = new Vector2(targetX, from.y);
            if (duration <= 0f)
            {
                rt.anchoredPosition = to;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, easing(t)); // LerpUnclamped でオーバーシュート可
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            rt.anchoredPosition = to;
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        // EaseOutBack: 目標を少し行き過ぎてから戻る (バン! の当たり)。enterOvershoot で強さ調整。
        private float EaseOutBack(float t)
        {
            var c1 = enterOvershoot;
            var c3 = c1 + 1f;
            var p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        // -------- Stage 4.3: 勝者の勝利アニメ --------
        private async UniTask PlayWinAnimationAsync(PressResult result, CancellationToken token)
        {
            var anim = result.Winner == 1 ? player1WinAnim
                : result.Winner == 2 ? player2WinAnim
                : null;
            if (anim == null || anim.Length == 0)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                return;
            }

            anim.Play();
            if (anim.TotalDuration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(anim.TotalDuration), cancellationToken: token);
        }

        // -------- Stage 5: 勝利者演出 --------
        private async UniTask PlayWinnerEffectAsync(PressResult result, CancellationToken token)
        {
            _phase = Phase.Winner;
            if (resultText != null)
                resultText.text = result.Foul
                    ? $"FOUL! Player {result.Winner} Wins"
                    : $"Player {result.Winner} Wins!";

            // 勝利者表示のあと、UI(resultGroup/PlayAgain)を出す前に一拍待機
            if (winnerUiDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(winnerUiDelay), cancellationToken: token);

            if (resultGroup != null)
            {
                resultGroup.alpha = 1f;
                resultGroup.interactable = true;
                resultGroup.blocksRaycasts = true;
            }
            SetFocus(playAgainButton);
            // TODO: 勝利演出（スポットライト・BGM切替・エフェクトなど）
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- Stage 6: 任意ボタン入力待ち --------
        private async UniTask WaitForExitPressAsync(CancellationToken token)
        {
            _phase = Phase.WaitForExit;
            _pressSignal = new UniTaskCompletionSource();
            await _pressSignal.Task.AttachExternalCancellation(token);
        }

        // -------- Stage 7: 抜ける演出 --------
        private async UniTask PlayExitEffectAsync(CancellationToken token)
        {
            _phase = Phase.Exiting;
            // TODO: 抜ける演出（フェードアウト・スライド・SEなど）
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- 入力処理 --------
        private void OnP1(InputAction.CallbackContext ctx)
        {
            if (!IsDeviceForPlayer(ctx.control.device, 1)) return;
            HandlePress(1);
        }

        private void OnP2(InputAction.CallbackContext ctx)
        {
            if (!IsDeviceForPlayer(ctx.control.device, 2)) return;
            HandlePress(2);
        }

        // Keyboard は常に有効。Gamepad は接続順で player を割り当て
        private static bool IsDeviceForPlayer(InputDevice device, int player)
        {
            if (device is Keyboard) return true;
            if (device is Gamepad gp)
            {
                var idx = GamepadIndexOf(gp);
                return idx == player - 1;
            }
            return false;
        }

        private static int GamepadIndexOf(Gamepad gp)
        {
            var all = Gamepad.all;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] == gp) return i;
            }
            return -1;
        }

        private void HandlePress(int player)
        {
            if (_pressSignal == null) return;
            if (_pressSignal.Task.Status.IsCompleted()) return;

            switch (_phase)
            {
                case Phase.Waiting:
                    _pressResult = new PressResult
                    {
                        Winner = player == 1 ? 2 : 1,
                        Foul = true,
                    };
                    _phase = Phase.Reaction;
                    _pressSignal.TrySetResult();
                    break;
                case Phase.Ready:
                    _pressResult = new PressResult
                    {
                        Winner = player,
                        Foul = false,
                    };
                    _phase = Phase.Reaction;
                    _pressSignal.TrySetResult();
                    break;
                case Phase.WaitForExit:
                    _phase = Phase.Exiting;
                    _pressSignal.TrySetResult();
                    break;
            }
        }

        // -------- View reset --------
        private void ResetInitialView()
        {
            _phase = Phase.Idle;
            ResetRoundView();
            if (introGroup != null) introGroup.alpha = 0f;
            if (introText != null) introText.text = "READY?";
        }

        private void ResetRoundView()
        {
            // 開始演出で登場させるため、ラウンド開始時は画面外に置く
            if (player1Character != null) player1Character.anchoredPosition = _p1Home + new Vector2(-characterEnterOffsetX, 0f);
            if (player2Character != null) player2Character.anchoredPosition = _p2Home + new Vector2(characterEnterOffsetX, 0f);
            if (targetImage != null) targetImage.enabled = false;
            if (resultGroup != null)
            {
                resultGroup.alpha = 0f;
                resultGroup.interactable = false;
                resultGroup.blocksRaycasts = false;
            }
            if (statusText != null) statusText.text = string.Empty;
        }
    }
}
