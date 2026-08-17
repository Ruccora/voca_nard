using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VocaNerd
{
    public class MashRaceGame : PanelBase
    {
        private enum Phase
        {
            Idle,
            Intro,
            Countdown,
            Playing,
            Result,
            Winner,
            WaitForExit,
            Exiting,
        }

        private class PlayerState
        {
            public int alternations;
            public int lastDirection; // 0 = none, -1 = left, +1 = right
            public bool locked;
        }

        [Header("Common View")]
        [SerializeField] private TMP_Text introText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private CanvasGroup resultGroup;
        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private Button playAgainButton;

        [Header("Background (shared full-screen)")]
        [SerializeField] private RectTransform starsRect;  // 星: Z 回転し続け、後半ゆっくり縮小
        [SerializeField] private RectTransform groundRect;  // 地面: 下起点アンカー。上昇→縮小→下降で消える
        [SerializeField] private RectTransform earthRect;   // 地球: 下から競り上がる
        [SerializeField] private CanvasGroup whiteFade;     // 画面を白くする fadein

        [Header("Players")]
        [SerializeField] private RectTransform player1Character;
        [SerializeField] private RectTransform player2Character;
        [SerializeField] private SpriteAnimation player1Anim;
        [SerializeField] private SpriteAnimation player2Anim;
        [SerializeField] private CanvasGroup player1MissGroup;
        [SerializeField] private CanvasGroup player2MissGroup;

        [Header("Timing")]
        [SerializeField] private float introDuration = 1.2f;
        [SerializeField] private float countdownStep = 1f;
        [SerializeField] private float playDuration = 10f;
        [SerializeField] private float missLockDuration = 0.2f;

        [Header("Character Fly")]
        [SerializeField] private float charRestY = -500f;      // 待機位置
        [SerializeField] private float winnerRiseY = -150f;    // 勝者が上がって残る位置
        [SerializeField] private float winnerRiseDuration = 0.6f;
        [SerializeField] private float loserFallY = -1400f;    // 敗者の落下先 (画面外)
        [SerializeField] private float loserFallDuration = 0.7f;
        [SerializeField, Range(0.3f, 1f)] private float charShrinkScale = 0.7f; // 星と一緒に少し縮む

        [Header("Power (連打 = 演出予算)")]
        // 連打 = パワー。maxPower * (白到達尺 / whiteReachAlternations) 秒ぶん演出を進めて、
        // 使い切ったら演出停止(その画面状態で固定)。whiteReachAlternations 連打で白に到達 (80 → 15秒)。
        // それ未満は途中停止(白に届かない)、超過ぶんは白到達後のキャラ回転に回る。
        [SerializeField] private int whiteReachAlternations = 80;
        [SerializeField] private float postWhiteSpinSpeed = 180f;   // 白到達後のキャラ Z 回転 (deg/s)

        [Header("Result Sequence (白到達まで 15 秒)")]
        // 各尺の合計 = 白到達までの時間 (1.0+3.0+1.8+3.2+3.2+1.6+1.2 = 15.0 秒)
        [SerializeField] private float starsSpinSpeed = 40f;        // 星の Z 回転 (deg/s)
        [SerializeField] private float groundRiseHeight = 120f;     // 地面が少し上がる量
        [SerializeField] private float groundRiseDuration = 1.0f;
        [SerializeField] private float groundShrinkDuration = 3.0f; // 地面 scale 縮小
        [SerializeField, Range(0.05f, 1f)] private float groundMinScale = 0.2f;
        [SerializeField] private float groundDescendDuration = 1.8f; // 下に移動して消える
        [SerializeField] private float groundMoveUnitPerPower = 4f;  // プレイヤー移動量換算 (px / power)
        [SerializeField] private float groundMoveMin = 400f;
        [SerializeField] private float groundMoveMax = 1600f;
        [SerializeField] private float starsShrinkDuration = 3.2f;  // 星ゆっくり縮小
        [SerializeField, Range(0.05f, 1f)] private float starsMinScale = 0.3f;
        [SerializeField] private float earthStartY = -1600f;        // 地球の初期位置 (下)
        [SerializeField] private float earthRiseY = -200f;          // 競り上がる先
        [SerializeField] private float earthRiseDuration = 3.2f;
        [SerializeField] private float earthHoldBeforeWhite = 1.6f; // 一定秒数
        [SerializeField] private float whiteFadeDuration = 1.2f;    // 白 fadein

        private Phase _phase;

        public override bool CanAcceptBack => _phase == Phase.Winner || _phase == Phase.WaitForExit;
        private readonly PlayerState _p1 = new PlayerState();
        private readonly PlayerState _p2 = new PlayerState();
        private InputAction _p1Left, _p1Right, _p2Left, _p2Right;
        private CancellationTokenSource _roundCts;
        private CancellationTokenSource _effectCts; // 背景シーケンス+星回転専用。破棄=その状態で演出停止
        private UniTaskCompletionSource _exitSignal;

        // 星と一緒に縮む「残ったキャラ」。敗者は含めない。
        private RectTransform _shrinkCharA;
        private RectTransform _shrinkCharB;
        private bool _isSetup;

        public override UniTask SetupAsync(CancellationToken token)
        {
            if (_isSetup) return UniTask.CompletedTask;
            _isSetup = true;

            _p1Left = MakeAction("P1Left", "<Keyboard>/a");
            _p1Right = MakeAction("P1Right", "<Keyboard>/d");
            _p2Left = MakeAction("P2Left", "<Keyboard>/leftArrow");
            _p2Right = MakeAction("P2Right", "<Keyboard>/rightArrow");

            _p1Left.performed += _ => HandlePress(1, -1);
            _p1Right.performed += _ => HandlePress(1, +1);
            _p2Left.performed += _ => HandlePress(2, -1);
            _p2Right.performed += _ => HandlePress(2, +1);

            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgain);

            ResetInitialView();
            return UniTask.CompletedTask;
        }

        protected override async UniTask OnPanelInAsync(CancellationToken token)
        {
            EnableInputs();
            await base.OnPanelInAsync(token);
            StartRound();
        }

        protected override async UniTask OnPanelOutAsync(CancellationToken token)
        {
            CancelRound();
            DisableInputs();
            await base.OnPanelOutAsync(token);
        }

        private void OnDestroy()
        {
            CancelRound();
            _p1Left?.Dispose();
            _p1Right?.Dispose();
            _p2Left?.Dispose();
            _p2Right?.Dispose();
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgain);
        }

        private static InputAction MakeAction(string name, string binding)
        {
            var a = new InputAction(name, InputActionType.Button);
            a.AddBinding(binding);
            return a;
        }

        private void EnableInputs()
        {
            _p1Left?.Enable(); _p1Right?.Enable();
            _p2Left?.Enable(); _p2Right?.Enable();
        }

        private void DisableInputs()
        {
            _p1Left?.Disable(); _p1Right?.Disable();
            _p2Left?.Disable(); _p2Right?.Disable();
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
            _effectCts?.Cancel();
            _effectCts?.Dispose();
            _effectCts = null;
            _roundCts?.Cancel();
            _roundCts?.Dispose();
            _roundCts = null;
        }

        private async UniTaskVoid RunRoundAsync(CancellationToken token)
        {
            try
            {
                ResetRoundView();
                ResetPlayerStates();

                await PlayIntroEffectAsync(token);
                await PlayCountdownAsync(token);
                await PlayGameAsync(token);
                await PlayResultEffectAsync(token);
                await PlayWinnerEffectAsync(token);
                await WaitForExitPressAsync(token);
                await PlayExitEffectAsync(token);

                if (ScreenController.Instance != null)
                    ScreenController.Instance.ShowAsync(ScreenType.Select).Forget();
            }
            catch (OperationCanceledException)
            {
            }
        }

        // -------- Stage 1: 開始演出 --------
        private async UniTask PlayIntroEffectAsync(CancellationToken token)
        {
            _phase = Phase.Intro;
            if (introText != null) introText.text = "READY?";
            await UniTask.Delay(TimeSpan.FromSeconds(introDuration), cancellationToken: token);
            if (introText != null) introText.text = string.Empty;
        }

        // -------- Stage 2: カウントダウン --------
        private async UniTask PlayCountdownAsync(CancellationToken token)
        {
            _phase = Phase.Countdown;
            for (var i = 3; i >= 1; i--)
            {
                if (countdownText != null) countdownText.text = i.ToString();
                await UniTask.Delay(TimeSpan.FromSeconds(countdownStep), cancellationToken: token);
            }
            if (countdownText != null) countdownText.text = "GO!";
            await UniTask.Delay(TimeSpan.FromSeconds(countdownStep * 0.5f), cancellationToken: token);
            if (countdownText != null) countdownText.text = string.Empty;
        }

        // -------- Stage 3: プレイ中 (連打) --------
        private async UniTask PlayGameAsync(CancellationToken token)
        {
            _phase = Phase.Playing;
            var elapsed = 0f;
            while (elapsed < playDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var remaining = Mathf.Max(0f, playDuration - elapsed);
                if (timerText != null) timerText.text = $"{remaining:0.0}";
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            if (timerText != null) timerText.text = "0.0";

            var best = Mathf.Max(_p1.alternations, _p2.alternations);
            if (SaveData.TrySetHighScore(SaveData.GameId.MashRace, best))
                Debug.Log($"[MashRace] New high score: {best}");
        }

        // -------- Stage 4: 結果演出 (共有背景シーケンス) --------
        private async UniTask PlayResultEffectAsync(CancellationToken token)
        {
            _phase = Phase.Result;

            var maxPower = Mathf.Max(_p1.alternations, _p2.alternations);
            var draw = _p1.alternations == _p2.alternations;

            // 勝敗で「残る/落ちる」を決める
            _shrinkCharA = null;
            _shrinkCharB = null;
            if (draw)
            {
                // 引き分け: 両者残る (両方が星と一緒に縮む)
                _shrinkCharA = player1Character;
                _shrinkCharB = player2Character;
                RiseWinnerAsync(player1Character, token).Forget();
                RiseWinnerAsync(player2Character, token).Forget();
            }
            else
            {
                var winnerChar = _p1.alternations > _p2.alternations ? player1Character : player2Character;
                var loserChar = _p1.alternations > _p2.alternations ? player2Character : player1Character;
                _shrinkCharA = winnerChar;
                RiseWinnerAsync(winnerChar, token).Forget();
                // 敗者落下は演出停止(_effectCts)の影響を受けない別 UniTask。round トークンで走らせる。
                FallLoserAsync(loserChar, token).Forget();
            }

            // 連打 = パワー = 演出予算。背景シーケンス(白まで15秒)を effectToken で走らせ、
            // maxPower ぶんの時間が経ったら _effectCts を破棄して「その画面状態で停止」する。
            _effectCts?.Dispose();
            _effectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var effectToken = _effectCts.Token;

            SpinStarsAsync(effectToken).Forget();

            // 予算 = maxPower * (白到達尺 / whiteReachAlternations)。
            // 80連打 → 白到達尺ちょうど(=白に到達)。40連打 → その半分で途中停止。
            var secondsPerAlternation = ToWhiteSeconds() / Mathf.Max(1, whiteReachAlternations);
            var budget = maxPower * secondsPerAlternation;
            BudgetStopAsync(budget, _effectCts).Forget();

            try
            {
                await PlayBackgroundSequenceAsync(maxPower, effectToken);
            }
            catch (OperationCanceledException)
            {
                // 予算切れ(=連打パワー消費)でキャンセル → その画面状態で固定
            }

            _effectCts?.Cancel();
            _effectCts?.Dispose();
            _effectCts = null;
        }

        // 星を Z 軸で回し続ける (effect 停止で止まる)
        private async UniTaskVoid SpinStarsAsync(CancellationToken token)
        {
            if (starsRect == null) return;
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    starsRect.Rotate(0f, 0f, -starsSpinSpeed * Time.deltaTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        // 勝者(残る側): 上に少し上がってその場に残る。SpriteAnimation は回り続ける。
        private async UniTaskVoid RiseWinnerAsync(RectTransform character, CancellationToken token)
        {
            if (character == null) return;
            try
            {
                var from = character.anchoredPosition.y;
                await LerpAnchoredYAsync(character, from, winnerRiseY, winnerRiseDuration, EaseOutCubic, token);
            }
            catch (OperationCanceledException) { }
        }

        // 敗者: 素直に落ちるだけ (オーバーシュート無し)。演出停止では止めない。
        private async UniTaskVoid FallLoserAsync(RectTransform character, CancellationToken token)
        {
            if (character == null) return;
            try
            {
                var from = character.anchoredPosition.y;
                await LerpAnchoredYAsync(character, from, loserFallY, loserFallDuration, EaseInQuad, token);
                if (character != null) character.gameObject.SetActive(false);
            }
            catch (OperationCanceledException) { }
        }

        // 背景シーケンス本体: 地面→星→地球→白 fadein
        private async UniTask PlayBackgroundSequenceAsync(int maxPower, CancellationToken token)
        {
            // 1) 地面: 下起点で少し上がる
            if (groundRect != null)
            {
                var y0 = groundRect.anchoredPosition.y;
                await LerpAnchoredYAsync(groundRect, y0, y0 + groundRiseHeight, groundRiseDuration, EaseOutCubic, token);
            }

            // 2) 地面: scale が徐々に最小まで縮小
            await LerpScaleAsync(groundRect, 1f, groundMinScale, groundShrinkDuration, EaseInOutSine, token);

            // 3) 地面: プレイヤー移動量に合わせて下に移動して消える
            var moveAmount = Mathf.Clamp(maxPower * groundMoveUnitPerPower, groundMoveMin, groundMoveMax);
            if (groundRect != null)
            {
                var y1 = groundRect.anchoredPosition.y;
                await LerpAnchoredYAsync(groundRect, y1, y1 - moveAmount, groundDescendDuration, EaseInCubic, token);
                groundRect.gameObject.SetActive(false);
            }

            // 4) 星: ゆっくり縮小 (残ったキャラも一緒に少し縮小)
            await ShrinkStarsAndCharsAsync(starsMinScale, starsShrinkDuration, token);

            // 5) 地球: 下から競り上がる
            if (earthRect != null)
            {
                earthRect.gameObject.SetActive(true);
                SetAnchoredY(earthRect, earthStartY);
                await LerpAnchoredYAsync(earthRect, earthStartY, earthRiseY, earthRiseDuration, EaseOutCubic, token);
            }

            // 6) 一定秒数経過
            if (earthHoldBeforeWhite > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(earthHoldBeforeWhite), cancellationToken: token);

            // 7) 画面が白くなる fadein
            await FadeGroupAsync(whiteFade, 0f, 1f, whiteFadeDuration, token);

            // 8) 白到達後: 残った(=勝った)キャラが回転するだけ。予算(連打パワー)が尽きるまで続ける。
            await SpinRemainCharsAsync(token);
        }

        // 白到達までの基準尺(秒)。連打パワーの換算に使う (whiteReachAlternations 連打でこの秒数)。
        private float ToWhiteSeconds()
            => groundRiseDuration + groundShrinkDuration + groundDescendDuration
             + starsShrinkDuration + earthRiseDuration + earthHoldBeforeWhite + whiteFadeDuration;

        // 予算(連打パワー)ぶんの時間が経ったら演出停止(その画面状態で固定)する監視。
        private async UniTaskVoid BudgetStopAsync(float seconds, CancellationTokenSource cts)
        {
            if (cts == null) return;
            try
            {
                if (seconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
                return; // 既に停止済み
            }
            cts.Cancel();
        }

        // 残ったキャラを Z 回転させ続ける (予算切れのキャンセルで止まる)
        private async UniTask SpinRemainCharsAsync(CancellationToken token)
        {
            if (_shrinkCharA == null && _shrinkCharB == null) return;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var dz = -postWhiteSpinSpeed * Time.deltaTime;
                if (_shrinkCharA != null) _shrinkCharA.Rotate(0f, 0f, dz);
                if (_shrinkCharB != null) _shrinkCharB.Rotate(0f, 0f, dz);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        // 星の縮小と残ったキャラの縮小を同時進行
        private async UniTask ShrinkStarsAndCharsAsync(float starsTo, float duration, CancellationToken token)
        {
            var starsFrom = starsRect != null ? starsRect.localScale.x : 1f;
            if (duration <= 0f)
            {
                SetScale(starsRect, starsTo);
                SetScale(_shrinkCharA, charShrinkScale);
                SetScale(_shrinkCharB, charShrinkScale);
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = EaseInOutSine(Mathf.Clamp01(elapsed / duration));
                SetScale(starsRect, Mathf.Lerp(starsFrom, starsTo, t));
                var charScale = Mathf.Lerp(1f, charShrinkScale, t);
                SetScale(_shrinkCharA, charScale);
                SetScale(_shrinkCharB, charScale);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetScale(starsRect, starsTo);
            SetScale(_shrinkCharA, charShrinkScale);
            SetScale(_shrinkCharB, charShrinkScale);
        }

        // -------- Stage 5: 勝敗演出 --------
        private async UniTask PlayWinnerEffectAsync(CancellationToken token)
        {
            _phase = Phase.Winner;
            string msg;
            if (_p1.alternations > _p2.alternations) msg = "Player 1 Wins!";
            else if (_p2.alternations > _p1.alternations) msg = "Player 2 Wins!";
            else msg = "Draw!";
            if (winnerText != null) winnerText.text = msg;
            if (resultGroup != null)
            {
                resultGroup.alpha = 1f;
                resultGroup.interactable = true;
                resultGroup.blocksRaycasts = true;
            }
            SetFocus(playAgainButton);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- Stage 6: 任意ボタン入力待ち --------
        private async UniTask WaitForExitPressAsync(CancellationToken token)
        {
            _phase = Phase.WaitForExit;
            _exitSignal = new UniTaskCompletionSource();
            await _exitSignal.Task.AttachExternalCancellation(token);
        }

        // -------- Stage 7: 抜ける演出 --------
        private async UniTask PlayExitEffectAsync(CancellationToken token)
        {
            _phase = Phase.Exiting;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- 入力処理 --------
        private void HandlePress(int player, int direction)
        {
            if (_phase == Phase.Playing)
            {
                var state = player == 1 ? _p1 : _p2;
                if (state.locked) return;
                if (state.lastDirection == 0)
                {
                    state.lastDirection = direction;
                    return;
                }
                if (state.lastDirection != direction)
                {
                    state.alternations++;
                    state.lastDirection = direction;
                }
                else
                {
                    state.locked = true;
                    var missToken = _roundCts?.Token ?? default;
                    PlayMissAsync(player, missToken).Forget();
                }
                return;
            }

            if (_phase == Phase.WaitForExit)
            {
                if (_exitSignal == null || _exitSignal.Task.Status.IsCompleted()) return;
                _phase = Phase.Exiting;
                _exitSignal.TrySetResult();
            }
        }

        private async UniTaskVoid PlayMissAsync(int player, CancellationToken token)
        {
            var missGroup = player == 1 ? player1MissGroup : player2MissGroup;
            if (missGroup != null) missGroup.alpha = 1f;
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(missLockDuration), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
            }
            if (missGroup != null) missGroup.alpha = 0f;
            var state = player == 1 ? _p1 : _p2;
            state.locked = false;
        }

        // -------- View reset --------
        private void ResetInitialView()
        {
            _phase = Phase.Idle;
            ResetRoundView();
            if (introText != null) introText.text = string.Empty;
        }

        private void ResetRoundView()
        {
            if (countdownText != null) countdownText.text = string.Empty;
            if (timerText != null) timerText.text = $"{playDuration:0.0}";

            // 背景初期化
            if (starsRect != null)
            {
                starsRect.gameObject.SetActive(true);
                starsRect.localRotation = Quaternion.identity;
                starsRect.localScale = Vector3.one;
            }
            if (groundRect != null)
            {
                groundRect.gameObject.SetActive(true);
                groundRect.localScale = Vector3.one;
                SetAnchoredY(groundRect, 0f);
            }
            if (earthRect != null)
            {
                SetAnchoredY(earthRect, earthStartY);
                earthRect.gameObject.SetActive(false);
            }
            if (whiteFade != null) whiteFade.alpha = 0f;

            // キャラ初期化
            _shrinkCharA = null;
            _shrinkCharB = null;
            ResetCharacter(player1Character);
            ResetCharacter(player2Character);

            if (player1MissGroup != null) player1MissGroup.alpha = 0f;
            if (player2MissGroup != null) player2MissGroup.alpha = 0f;
            if (winnerText != null) winnerText.text = string.Empty;
            if (resultGroup != null)
            {
                resultGroup.alpha = 0f;
                resultGroup.interactable = false;
                resultGroup.blocksRaycasts = false;
            }
        }

        private void ResetCharacter(RectTransform character)
        {
            if (character == null) return;
            character.gameObject.SetActive(true);
            character.localScale = Vector3.one;
            character.localRotation = Quaternion.identity;
            SetAnchoredY(character, charRestY);
        }

        private void ResetPlayerStates()
        {
            _p1.alternations = 0; _p1.lastDirection = 0; _p1.locked = false;
            _p2.alternations = 0; _p2.lastDirection = 0; _p2.locked = false;
        }

        // -------- Tween helpers --------
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInCubic(float t) => t * t * t;
        private static float EaseInQuad(float t) => t * t;
        private static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

        private async UniTask LerpAnchoredYAsync(RectTransform rt, float from, float to, float duration, Func<float, float> easing, CancellationToken token)
        {
            if (rt == null) return;
            if (duration <= 0f) { SetAnchoredY(rt, to); return; }
            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetAnchoredY(rt, Mathf.Lerp(from, to, easing(t)));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetAnchoredY(rt, to);
        }

        private async UniTask LerpScaleAsync(RectTransform rt, float from, float to, float duration, Func<float, float> easing, CancellationToken token)
        {
            if (rt == null) return;
            if (duration <= 0f) { SetScale(rt, to); return; }
            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetScale(rt, Mathf.Lerp(from, to, easing(t)));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetScale(rt, to);
        }

        private async UniTask FadeGroupAsync(CanvasGroup cg, float from, float to, float duration, CancellationToken token)
        {
            if (cg == null) return;
            if (duration <= 0f) { cg.alpha = to; return; }
            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            cg.alpha = to;
        }

        private static void SetAnchoredY(RectTransform rt, float y)
        {
            if (rt == null) return;
            var pos = rt.anchoredPosition;
            pos.y = y;
            rt.anchoredPosition = pos;
        }

        private static void SetScale(RectTransform rt, float s)
        {
            if (rt == null) return;
            rt.localScale = new Vector3(s, s, 1f);
        }
    }
}
