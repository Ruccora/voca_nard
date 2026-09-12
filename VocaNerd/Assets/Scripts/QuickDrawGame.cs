using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private Image targetImage;
        [SerializeField] private CanvasGroup resultGroup;
        [Tooltip("勝者表示 (1P / 2P の画像)")]
        [SerializeField] private WinnerLabel winnerLabel;

        [Header("Players")]
        [SerializeField] private RectTransform player1Character;
        [SerializeField] private RectTransform player2Character;
        [SerializeField] private SpriteAnimation player1WinAnim; // 勝利アニメ (loop=false 推奨)
        [SerializeField] private SpriteAnimation player2WinAnim;
        [SerializeField] private Sprite player1LoseSprite; // 敗北時に差し替えるスプライト
        [SerializeField] private Sprite player2LoseSprite;
        [SerializeField] private float openingHoldDuration = 1f;      // 開始演出の待機 (露出 → はけ の間)

        [Tooltip("開始演出が終わった後、READY? に入る前に挟む待機秒数")]
        [SerializeField] private float openingEndWait = 1f;           // 開始演出の後の待機

        [Header("White Flash")]
        // 勝利モーション直前の白フラッシュ。未設定なら実行時に全画面の白 Image を自動生成する。
        [SerializeField] private Image whiteFlashImage;
        [SerializeField] private int whiteFlashFrames = 1;

        [Header("Opening")]
        // 別 prefab で作った開始演出を子として当てこんで参照する (未設定なら演出なし)
        [SerializeField] private OpeningEffect openingEffect;

        [Header("SE")]
        [Tooltip("開始演出の頭で鳴らす SE キー。空なら無音")]
        [SerializeField] private string openingSeKey = SeKey.QuickDrawOpening;

        [Tooltip("開始演出が終わった直後に鳴らす SE キー。空なら無音")]
        [SerializeField] private string startSeKey = SeKey.QuickDrawStart;

        [Tooltip("合図のマークが出た瞬間に鳴らす SE キー。空なら無音")]
        [SerializeField] private string revealSeKey = SeKey.QuickDrawReveal;

        [Tooltip("勝利アニメの開始と同時に鳴らす SE キー。空なら無音")]
        [SerializeField] private string winnerSeKey = SeKey.QuickDrawWinner;

        [Header("Timing")]
        [SerializeField] private float minWait = 3f;
        [SerializeField] private float maxWait = 5f;
        [SerializeField] private float winnerDelay = 1.5f;
        [SerializeField] private float winnerUiDelay = 1.5f;

        [Tooltip("Play Again からの再戦時、開始演出に入る前に挟む待機秒数")]
        [SerializeField] private float replayDelay = 1f;

        private Phase _phase;

        public override bool CanAcceptBack => _phase == Phase.Winner || _phase == Phase.WaitForExit;
        private InputAction _p1Action;
        private InputAction _p2Action;
        private ResultInput _resultInput; // リザルトの A=再戦 / B=退出 (1P のみ)
        private CancellationTokenSource _roundCts;
        private UniTaskCompletionSource _pressSignal;
        private PressResult _pressResult;
        private readonly System.Random _rng = new System.Random();
        private Vector2 _p1Home;
        private Vector2 _p2Home;
        private Image _p1Image;
        private Image _p2Image;
        private Sprite _p1OrigSprite;
        private Sprite _p2OrigSprite;
        private bool _isSetup;

        // 開始演出の背景暗転で使うマテリアルインスタンス (共有アセットを汚さないよう複製)
        private Material _openingDimMat;
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        public override async UniTask SetupAsync(CancellationToken token)
        {
            if (_isSetup) return;
            _isSetup = true;

            _p1Action = new InputAction("Player1", InputActionType.Button);
            _p1Action.AddBinding("<Keyboard>/a");
            _p1Action.AddBinding(GamepadButtons.A);
            _p1Action.performed += OnP1;

            _p2Action = new InputAction("Player2", InputActionType.Button);
            _p2Action.AddBinding("<Keyboard>/l");
            _p2Action.AddBinding(GamepadButtons.A);
            _p2Action.performed += OnP2;

            _resultInput = new ResultInput(OnResultRetry, OnResultExit);

            // キャラの初期位置（左右）を記録。以後ラウンド毎にここへ戻す。
            if (player1Character != null) _p1Home = player1Character.anchoredPosition;
            if (player2Character != null) _p2Home = player2Character.anchoredPosition;

            // キャラの元画像(勝利アニメ前のスプライト)を記録。Play Again で戻す。
            _p1Image = player1Character != null ? player1Character.GetComponent<Image>() : null;
            _p2Image = player2Character != null ? player2Character.GetComponent<Image>() : null;
            if (_p1Image != null){ _p1OrigSprite = _p1Image.sprite;}
            if (_p2Image != null) _p2OrigSprite = _p2Image.sprite;

            EnsureWhiteFlash();
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
            _resultInput?.Disable();
            await base.OnPanelOutAsync(token);
        }

        private void OnDestroy()
        {
            CancelRound();
            _p1Action?.Dispose();
            _p2Action?.Dispose();
            _resultInput?.Dispose();
            if (_openingDimMat != null) Destroy(_openingDimMat);
        }

        // リザルト: 1P の A / Enter で再戦
        private void OnResultRetry()
        {
            if (IsAnimating) return;
            if (_phase != Phase.WaitForExit) return;
            _resultInput.Disable();
            Audio.StopSE(); // 勝利 SE が長いので、鳴りっぱなしのまま次ラウンドに入らせない
            Audio.PlaySE(SeKey.Decide);
            StartRound(replay: true);
        }

        // リザルト: 1P の B / X で戻る (通常の退出シーケンスへ流す)
        private void OnResultExit()
        {
            if (IsAnimating) return;
            if (_phase != Phase.WaitForExit) return;
            if (_pressSignal == null || _pressSignal.Task.Status.IsCompleted()) return;
            _resultInput.Disable();
            Audio.PlaySE(SeKey.Cancel);
            _phase = Phase.Exiting;
            _pressSignal.TrySetResult();
        }

        private void StartRound(bool replay = false)
        {
            CancelRound();
            _roundCts = new CancellationTokenSource();
            RunRoundAsync(_roundCts.Token, replay).Forget();
        }

        private void CancelRound()
        {
            _roundCts?.Cancel();
            _roundCts?.Dispose();
            _roundCts = null;
        }

        private async UniTaskVoid RunRoundAsync(CancellationToken token, bool replay)
        {
            try
            {
                ResetRoundView();
                _pressResult = default;

                // -1) 遷移の明転が終わる = 画面が見えるまで待つ (明転自体は ScreenController が持つ)
                if (ScreenController.Instance != null)
                    await ScreenController.Instance.WaitForTransitionFadeAsync(token);

                // -0.5) 再戦時はリザルトから間を置かず始まらないよう一拍待つ
                if (replay && replayDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(replayDelay), cancellationToken: token);

                // 0) 開始演出
                await PlayOpeningEffectAsync(token);

                // 2) 3~5 秒待機（この間に押されたらフォール）
                var reachedReveal = await PlayWaitAsync(token);

                if (reachedReveal)
                {
                    // 3) 何らかのボタン表示演出
                    await PlayRevealEffectAsync(token);
                    // 押されるまで待つ
                    await WaitForPressAsync(token);
                }

                // 4) 押下時演出
                await PlayPressEffectAsync(token);

                // 4.2) 画面を 1f だけ真っ白に
                await PlayWhiteFlashAsync(token);

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

            // 開始演出は openingEffect 側に一任する (自キャラの動きも openingEffect が担当)。
            if (openingEffect == null) return;

            Audio.PlaySE(openingSeKey);

            // 1) 斜め線を露出 (表示したまま)
            await openingEffect.PlayAsync(token);

            // 2) 待機 (露出 → はけ の間)
            if (openingHoldDuration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(openingHoldDuration), cancellationToken: token);

            // 3) マスクが左右にはける → READY へ
            await openingEffect.ExitAsync(token);

            // 4) 開始演出の後の待機 (SE / READY? の前)
            if (openingEndWait > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(openingEndWait), cancellationToken: token);

            Audio.PlaySE(startSeKey);
        }

        // -------- Stage 2: 3~5秒待機 (returns true if full wait elapsed, false if foul) --------
        private async UniTask<bool> PlayWaitAsync(CancellationToken token)
        {
            _phase = Phase.Waiting;
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
            if (targetImage != null) targetImage.enabled = true;
            Audio.PlaySE(revealSeKey);
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
        private async UniTask PlayPressEffectAsync(CancellationToken token)
        {
            _phase = Phase.Reaction;
            if (targetImage != null) targetImage.enabled = false;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- Stage 4.2: 白フラッシュ (既定 1 フレームのみ描画) --------
        private async UniTask PlayWhiteFlashAsync(CancellationToken token)
        {
            var flash = EnsureWhiteFlash();
            if (flash == null || whiteFlashFrames <= 0) return;

            // Update 中に有効化 → そのフレームで描画される → 次フレームの Update で無効化。
            flash.enabled = true;
            try
            {
                await UniTask.DelayFrame(whiteFlashFrames, PlayerLoopTiming.Update, token);
            }
            finally
            {
                if (flash != null) flash.enabled = false;
            }
        }

        // 全画面の白 Image を用意する (prefab で未設定なら生成し、最前面に置く)
        private Image EnsureWhiteFlash()
        {
            if (whiteFlashImage != null) return whiteFlashImage;
            var parent = transform as RectTransform;
            if (parent == null) return null;

            var go = new GameObject("WhiteFlash");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            whiteFlashImage = go.AddComponent<Image>();
            whiteFlashImage.color = Color.white;
            whiteFlashImage.raycastTarget = false;
            whiteFlashImage.enabled = false;
            return whiteFlashImage;
        }

        // -------- Stage 4.3: 勝者の勝利アニメ --------
        private async UniTask PlayWinAnimationAsync(PressResult result, CancellationToken token)
        {
            Audio.PlaySE(winnerSeKey);

            // 負けた側 (勝者の反対) のスプライトを敗北用へ差し替える
            ApplyLoseSprite(result.Winner);

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

        // 負けた側のキャラ画像を敗北用スプライトへ差し替える (勝者の反対側)
        private void ApplyLoseSprite(int winner)
        {
            var loser = winner == 1 ? 2
                : winner == 2 ? 1
                : 0;
            if (loser == 0) return;

            var image = loser == 1 ? _p1Image : _p2Image;
            var loseSprite = loser == 1 ? player1LoseSprite : player2LoseSprite;
            if (image == null || loseSprite == null) return;

            image.sprite = loseSprite;
            image.SetNativeSize();
        }

        // -------- Stage 5: 勝利者演出 --------
        private async UniTask PlayWinnerEffectAsync(PressResult result, CancellationToken token)
        {
            _phase = Phase.Winner;
            if (winnerLabel != null) winnerLabel.Show(result.Winner);

            // 勝利者表示のあと、UI(resultGroup)を出す前に一拍待機
            if (winnerUiDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(winnerUiDelay), cancellationToken: token);

            if (resultGroup != null)
            {
                resultGroup.alpha = 1f;
                resultGroup.interactable = true;
                resultGroup.blocksRaycasts = true;
            }
            // 選択を残すと EventSystem の Submit (2P のパッドの A でも飛ぶ) で押せてしまうので外す
            ClearFocus();
            // TODO: 勝利演出（スポットライト・BGM切替・エフェクトなど）
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- Stage 6: 1P の A (再戦) / B (退出) 待ち --------
        private async UniTask WaitForExitPressAsync(CancellationToken token)
        {
            _phase = Phase.WaitForExit;
            _pressSignal = new UniTaskCompletionSource();
            _resultInput?.Enable();
            try
            {
                await _pressSignal.Task.AttachExternalCancellation(token);
            }
            finally
            {
                _resultInput?.Disable();
            }
        }

        // -------- Stage 7: 抜ける演出 --------
        private async UniTask PlayExitEffectAsync(CancellationToken token)
        {
            _phase = Phase.Exiting;
            // 最後に流している BGM を止める
            Audio.StopBgm();
            // TODO: 抜ける演出（フェードアウト・スライド・SEなど）
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- 入力処理 --------
        private void OnP1(InputAction.CallbackContext ctx)
        {
            if (!PlayerDevices.IsForPlayer(ctx, 1)) return;
            HandlePress(1);
        }

        private void OnP2(InputAction.CallbackContext ctx)
        {
            if (!PlayerDevices.IsForPlayer(ctx, 2)) return;
            HandlePress(2);
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
                // リザルトの退出は ResultInput (1P の B) が担当するのでここでは扱わない
            }
        }

        // -------- View reset --------
        private void ResetInitialView()
        {
            _phase = Phase.Idle;
            ResetRoundView();
        }

        private void ResetRoundView()
        {
            if (player1Character != null) player1Character.anchoredPosition = _p1Home;
            if (player2Character != null) player2Character.anchoredPosition = _p2Home;

            // 勝利アニメを止めてキャラ画像を元の状態へ戻す
            if (player1WinAnim != null) player1WinAnim.Stop();
            if (player2WinAnim != null) player2WinAnim.Stop();
            if (_p1Image != null)
            {
                _p1Image.sprite = _p1OrigSprite;
                _p1Image.SetNativeSize();
            }

            if (_p2Image != null)
            {
                _p2Image.sprite = _p2OrigSprite;
                _p2Image.SetNativeSize();
            }

            if (targetImage != null) targetImage.enabled = false;
            if (whiteFlashImage != null) whiteFlashImage.enabled = false;
            if (winnerLabel != null) winnerLabel.Clear();
            if (resultGroup != null)
            {
                resultGroup.alpha = 0f;
                resultGroup.interactable = false;
                resultGroup.blocksRaycasts = false;
            }
            // 背景の明るさを通常へ戻す
            SetOpeningBrightness(1f);
        }
        
        private void SetOpeningBrightness(float value)
        {
            if (_openingDimMat != null) _openingDimMat.SetFloat(BrightnessId, value);
        }

        private async UniTask AnimateOpeningBrightnessAsync(float from, float to, float duration, CancellationToken token)
        {
            if (_openingDimMat == null) return;
            if (duration <= 0f)
            {
                SetOpeningBrightness(to);
                return;
            }

            SetOpeningBrightness(from);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetOpeningBrightness(Mathf.Lerp(from, to, t));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetOpeningBrightness(to);
        }
    }
}