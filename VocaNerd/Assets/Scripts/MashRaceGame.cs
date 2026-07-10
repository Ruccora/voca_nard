using System;
using System.Collections.Generic;
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

        [Header("Player 1 (Left)")]
        [SerializeField] private RectTransform player1FlyIcon;
        [SerializeField] private RectTransform player1Background;
        [SerializeField] private RectTransform player1ObjectLayer;
        [SerializeField] private CanvasGroup player1MissGroup;

        [Header("Player 2 (Right)")]
        [SerializeField] private RectTransform player2FlyIcon;
        [SerializeField] private RectTransform player2Background;
        [SerializeField] private RectTransform player2ObjectLayer;
        [SerializeField] private CanvasGroup player2MissGroup;

        [Header("Timing")]
        [SerializeField] private float introDuration = 1.2f;
        [SerializeField] private float countdownStep = 1f;
        [SerializeField] private float playDuration = 10f;
        [SerializeField] private float missLockDuration = 0.2f;

        [Header("Fly Animation")]
        [SerializeField] private float flyStartY = -500f;
        [SerializeField] private float cruiseY = 200f;
        [SerializeField] private float charRiseDuration = 0.5f;
        [SerializeField] private float flyHeightPerAlternation = 60f;
        [SerializeField] private float flyMaxHeight = 2500f;
        [SerializeField] private float bgMaxScroll = 1500f;
        [SerializeField] private float bgScrollSpeed = 800f;
        [SerializeField] private float bgScrollMinSpeed = 100f;
        [SerializeField] private float decelZone = 200f;
        [SerializeField] private float overshootHeight = 50f;
        [SerializeField] private float overshootDuration = 0.3f;
        [SerializeField] private float fallDuration = 0.6f;

        [Header("Object Layer")]
        [SerializeField] private MashRaceFlyObject flyObjectPrefab;
        [SerializeField] private float objectSpacingPx = 120f;
        [SerializeField] private float objectSpawnY = 700f;
        [SerializeField] private float objectDespawnY = -700f;

        private Phase _phase;

        public override bool CanAcceptBack => _phase == Phase.Winner || _phase == Phase.WaitForExit;
        private readonly PlayerState _p1 = new PlayerState();
        private readonly PlayerState _p2 = new PlayerState();
        private readonly List<MashRaceFlyObject> _p1Objects = new List<MashRaceFlyObject>();
        private readonly List<MashRaceFlyObject> _p2Objects = new List<MashRaceFlyObject>();
        private InputAction _p1Left, _p1Right, _p2Left, _p2Right;
        private CancellationTokenSource _roundCts;
        private UniTaskCompletionSource _exitSignal;
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

                // 1) 開始演出
                await PlayIntroEffectAsync(token);
                // 2) カウントダウン
                await PlayCountdownAsync(token);
                // 3) プレイ中
                await PlayGameAsync(token);
                // 4) 結果演出 (カウントアップ)
                await PlayResultEffectAsync(token);
                // 5) 勝敗演出
                await PlayWinnerEffectAsync(token);
                // 6) 任意ボタン入力待ち
                await WaitForExitPressAsync(token);
                // 7) 抜ける演出
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
            // TODO: 開始演出（フェード・スケール・SEなど）
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
                // TODO: 数字ごとのポップ演出
                await UniTask.Delay(TimeSpan.FromSeconds(countdownStep), cancellationToken: token);
            }
            if (countdownText != null) countdownText.text = "GO!";
            await UniTask.Delay(TimeSpan.FromSeconds(countdownStep * 0.5f), cancellationToken: token);
            if (countdownText != null) countdownText.text = string.Empty;
        }

        // -------- Stage 3: プレイ中 --------
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
        }

        // -------- Stage 4: 結果演出（キャラ cruise固定・背景&オブジェクト複合スクロール）--------
        private async UniTask PlayResultEffectAsync(CancellationToken token)
        {
            _phase = Phase.Result;
            var t1 = AnimateFlyAsync(player1FlyIcon, player1Background, player1ObjectLayer, _p1Objects, _p1.alternations, token);
            var t2 = AnimateFlyAsync(player2FlyIcon, player2Background, player2ObjectLayer, _p2Objects, _p2.alternations, token);
            await UniTask.WhenAll(t1, t2);
        }

        private async UniTask AnimateFlyAsync(
            RectTransform icon,
            RectTransform bg,
            RectTransform objectLayer,
            List<MashRaceFlyObject> objects,
            int alternations,
            CancellationToken token)
        {
            if (icon == null) return;

            var totalFly = Mathf.Clamp(alternations * flyHeightPerAlternation, 0f, flyMaxHeight);
            var bgTarget = Mathf.Min(bgMaxScroll, totalFly);
            var objSpawnStart = bgMaxScroll * 0.5f;
            var bgStartY = bg != null ? bg.anchoredPosition.y : 0f;

            ClearObjects(objects);

            // 1) キャラが cruise 位置まで上昇
            await LerpYAsync(icon, icon.anchoredPosition.y, cruiseY, charRiseDuration, EaseOutCubic, token);

            // 2) 統合スクロール: 背景は bgTarget まで、オブジェクトは objSpawnStart 以降ずっと生成
            if (totalFly > 0f)
            {
                var scrolled = 0f;
                var spawnAccum = 0f;
                while (scrolled < totalFly)
                {
                    token.ThrowIfCancellationRequested();
                    var remaining = totalFly - scrolled;
                    var speed = remaining < decelZone
                        ? Mathf.Lerp(bgScrollMinSpeed, bgScrollSpeed, remaining / decelZone)
                        : bgScrollSpeed;
                    var advance = Mathf.Min(speed * Time.deltaTime, remaining);
                    scrolled += advance;

                    // 背景スクロール (bgTarget まで)
                    if (bg != null)
                    {
                        var bgOffset = Mathf.Min(scrolled, bgTarget);
                        SetY(bg, bgStartY - bgOffset);
                    }

                    // オブジェクト生成 (bgMaxScroll/2 以降)
                    if (objectLayer != null && scrolled >= objSpawnStart)
                    {
                        spawnAccum += advance;
                        while (spawnAccum >= objectSpacingPx)
                        {
                            spawnAccum -= objectSpacingPx;
                            SpawnObject(objectLayer, objects);
                        }
                    }

                    // 既存オブジェクト移動 (下方向)
                    MoveObjects(objects, advance);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }

            // スクロール終了 → オブジェクトはその場で揺れる
            StartSwayAll(objects);

            // 3) キャラだけ overshoot 上昇
            await LerpYAsync(icon, cruiseY, cruiseY + overshootHeight, overshootDuration, EaseOutCubic, token);

            // 4) キャラ落下
            await LerpYAsync(icon, cruiseY + overshootHeight, flyStartY, fallDuration, EaseInQuad, token);

            // オブジェクトは次ラウンドの ResetRoundView まで残して揺らし続ける
        }

        // -------- Object spawn helpers --------
        private void SpawnObject(RectTransform layer, List<MashRaceFlyObject> list)
        {
            if (flyObjectPrefab == null) return;
            var flyObj = Instantiate(flyObjectPrefab, layer);
            flyObj.Init(new Vector2(0f, objectSpawnY));
            list.Add(flyObj);
        }

        private void MoveObjects(List<MashRaceFlyObject> list, float distance)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var obj = list[i];
                if (obj == null) { list.RemoveAt(i); continue; }
                obj.MoveDown(distance);
                if (obj.Y < objectDespawnY)
                {
                    Destroy(obj.gameObject);
                    list.RemoveAt(i);
                }
            }
        }

        private void StartSwayAll(List<MashRaceFlyObject> list)
        {
            foreach (var obj in list)
                if (obj != null) obj.StartSway();
        }

        private void ClearObjects(List<MashRaceFlyObject> list)
        {
            foreach (var obj in list) if (obj != null) Destroy(obj.gameObject);
            list.Clear();
        }

        private async UniTask LerpYAsync(RectTransform rt, float from, float to, float duration, System.Func<float, float> easing, CancellationToken token)
        {
            if (rt == null) return;
            if (duration <= 0f) { SetY(rt, to); return; }
            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetY(rt, Mathf.Lerp(from, to, easing(t)));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetY(rt, to);
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInQuad(float t) => t * t;

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
            // TODO: 勝敗演出（スポットライト・エフェクトなど）
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
            // TODO: 抜ける演出
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
            SetY(player1FlyIcon, flyStartY);
            SetY(player2FlyIcon, flyStartY);
            SetY(player1Background, 0f);
            SetY(player2Background, 0f);
            ClearObjects(_p1Objects);
            ClearObjects(_p2Objects);
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

        private void ResetPlayerStates()
        {
            _p1.alternations = 0; _p1.lastDirection = 0; _p1.locked = false;
            _p2.alternations = 0; _p2.lastDirection = 0; _p2.locked = false;
        }

        private static void SetY(RectTransform rt, float y)
        {
            if (rt == null) return;
            var pos = rt.anchoredPosition;
            pos.y = y;
            rt.anchoredPosition = pos;
        }
    }
}
