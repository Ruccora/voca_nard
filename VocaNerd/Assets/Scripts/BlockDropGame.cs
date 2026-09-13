using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VocaNerd
{
    public class BlockDropGame : PanelBase
    {
        private enum Phase
        {
            Idle,
            Intro,
            Countdown,
            Playing,
            Winner,
            WaitForExit,
            Exiting,
        }

        private enum PlayerSide { Left, Right }

        private class PlayerState
        {
            public int blocksRemaining;
            public PlayerSide side;
            public bool isMoving;
            public bool isLocked;
            public readonly List<BlockDropBlock> blocks = new List<BlockDropBlock>();
        }

        [Header("Common View")]
        [SerializeField] private TMP_Text introText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private CanvasGroup resultGroup;
        [Tooltip("勝者表示 (1P / 2P は画像、残りの文言は TMP)")]
        [SerializeField] private WinnerLabel winnerLabel;

        [Header("Player 1 (Left)")]
        [SerializeField] private RectTransform player1Stack;
        [SerializeField] private RectTransform player1Character;
        [SerializeField] private CanvasGroupBlinker player1CharacterBlinker;

        [Header("Player 2 (Right)")]
        [SerializeField] private RectTransform player2Stack;
        [SerializeField] private RectTransform player2Character;
        [SerializeField] private CanvasGroupBlinker player2CharacterBlinker;

        [Header("Prefab")]
        [SerializeField] private BlockDropBlock blockPrefab;

        [Header("Config")]
        [SerializeField] private int blockCount = 30;
        [SerializeField] private float penaltyDuration = 0.5f;
        [SerializeField] private float knockFlyDuration = 0.4f;
        [SerializeField] private float knockFlyDistance = 900f;
        [SerializeField] private Vector2 blockSize = new Vector2(150f, 30f);
        [SerializeField] private float stackBottomY = -450f;
        [SerializeField] private float characterLeftX = -160f;
        [SerializeField] private float characterRightX = 160f;
        [SerializeField] private float characterY = -430f;
        [SerializeField, Range(0f, 1f)] private float stickBlockChance = 0.35f;

        [Header("Timing")]
        [SerializeField] private float introDuration = 1.2f;
        [SerializeField] private float countdownStep = 1f;

        private Phase _phase;

        public override bool CanAcceptBack => _phase == Phase.Winner || _phase == Phase.WaitForExit;

        private readonly PlayerState _p1 = new PlayerState();
        private readonly PlayerState _p2 = new PlayerState();
        private InputAction _p1Left, _p1Right, _p1Knock, _p1KnockAlt;
        private InputAction _p2Left, _p2Right, _p2Knock, _p2KnockAlt;
        private ResultInput _resultInput; // リザルトの A=再戦 / B=退出 (1P のみ)
        private CancellationTokenSource _roundCts;
        private UniTaskCompletionSource _winnerSignal;
        private UniTaskCompletionSource _exitSignal;
        private int _winner;
        private bool _isSetup;
        private readonly System.Random _rng = new System.Random();

        public override UniTask SetupAsync(CancellationToken token)
        {
            if (_isSetup) return UniTask.CompletedTask;
            _isSetup = true;

            // 左右移動は十字キー / 左スティック、叩くのは A / B (割り当ては GamepadButtons)。
            // 1P/2P で同じボタンを張っておき、どちらのパッドかは PlayerDevices で振り分ける。
            // 同じパスを共有する都合で PassThrough にしている (理由は PlayerInputAction)。
            _p1Left = PlayerInputAction.Make("P1Left", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            _p1Right = PlayerInputAction.Make("P1Right", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            _p1Knock = PlayerInputAction.Make("P1Knock", "<Keyboard>/w", GamepadButtons.A);
            _p1KnockAlt = PlayerInputAction.Make("P1KnockAlt", "<Keyboard>/s", GamepadButtons.B);

            _p2Left = PlayerInputAction.Make("P2Left", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            _p2Right = PlayerInputAction.Make("P2Right", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            _p2Knock = PlayerInputAction.Make("P2Knock", "<Keyboard>/upArrow", GamepadButtons.A);
            _p2KnockAlt = PlayerInputAction.Make("P2KnockAlt", "<Keyboard>/downArrow", GamepadButtons.B);

            PlayerInputAction.OnPress(_p1Left, 1, () => OnMove(1, PlayerSide.Left));
            PlayerInputAction.OnPress(_p1Right, 1, () => OnMove(1, PlayerSide.Right));
            PlayerInputAction.OnPress(_p1Knock, 1, () => OnKnock(1));
            PlayerInputAction.OnPress(_p1KnockAlt, 1, () => OnKnock(1));
            PlayerInputAction.OnPress(_p2Left, 2, () => OnMove(2, PlayerSide.Left));
            PlayerInputAction.OnPress(_p2Right, 2, () => OnMove(2, PlayerSide.Right));
            PlayerInputAction.OnPress(_p2Knock, 2, () => OnKnock(2));
            PlayerInputAction.OnPress(_p2KnockAlt, 2, () => OnKnock(2));

            _resultInput = new ResultInput(OnResultRetry, OnResultExit);

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
            _p1Left?.Dispose(); _p1Right?.Dispose(); _p1Knock?.Dispose(); _p1KnockAlt?.Dispose();
            _p2Left?.Dispose(); _p2Right?.Dispose(); _p2Knock?.Dispose(); _p2KnockAlt?.Dispose();
            _resultInput?.Dispose();
        }

        private void EnableInputs()
        {
            _p1Left?.Enable(); _p1Right?.Enable(); _p1Knock?.Enable(); _p1KnockAlt?.Enable();
            _p2Left?.Enable(); _p2Right?.Enable(); _p2Knock?.Enable(); _p2KnockAlt?.Enable();
        }

        private void DisableInputs()
        {
            _p1Left?.Disable(); _p1Right?.Disable(); _p1Knock?.Disable(); _p1KnockAlt?.Disable();
            _p2Left?.Disable(); _p2Right?.Disable(); _p2Knock?.Disable(); _p2KnockAlt?.Disable();
            _resultInput?.Disable();
        }

        // リザルト: 1P の A / Enter で再戦
        private void OnResultRetry()
        {
            if (IsAnimating) return;
            if (_phase != Phase.WaitForExit) return;
            _resultInput.Disable();
            Audio.PlaySE(SeKey.Decide);
            StartRound();
        }

        // リザルト: 1P の B / X で戻る (通常の退出シーケンスへ流す)
        private void OnResultExit()
        {
            if (IsAnimating) return;
            if (_phase != Phase.WaitForExit) return;
            if (_exitSignal == null || _exitSignal.Task.Status.IsCompleted()) return;
            _resultInput.Disable();
            Audio.PlaySE(SeKey.Cancel);
            _phase = Phase.Exiting;
            _exitSignal.TrySetResult();
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
                SpawnStacks();

                await PlayIntroEffectAsync(token);
                await PlayCountdownAsync(token);
                await PlayGameAsync(token);
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

        // -------- 開始演出 --------
        private async UniTask PlayIntroEffectAsync(CancellationToken token)
        {
            _phase = Phase.Intro;
            if (introText != null) introText.text = "READY?";
            await UniTask.Delay(TimeSpan.FromSeconds(introDuration), cancellationToken: token);
            if (introText != null) introText.text = string.Empty;
        }

        // -------- カウントダウン --------
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

        // -------- プレイ --------
        private async UniTask PlayGameAsync(CancellationToken token)
        {
            _phase = Phase.Playing;
            _winner = 0;
            _winnerSignal = new UniTaskCompletionSource();
            var startTime = Time.time;
            await _winnerSignal.Task.AttachExternalCancellation(token);

            var clearTime = Time.time - startTime;
            if (SaveData.TrySetBestTime(SaveData.GameId.BlockDrop, clearTime))
                Debug.Log($"[BlockDrop] New best time: {clearTime:0.00}s");
        }

        // -------- 勝利演出 --------
        private async UniTask PlayWinnerEffectAsync(CancellationToken token)
        {
            _phase = Phase.Winner;
            if (winnerLabel != null) winnerLabel.Show(_winner);
            if (resultGroup != null)
            {
                resultGroup.alpha = 1f;
                resultGroup.interactable = true;
                resultGroup.blocksRaycasts = true;
            }
            // 選択を残すと EventSystem の Submit (2P のパッドの A でも飛ぶ) で押せてしまうので外す
            ClearFocus();
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- 1P の A (再戦) / B (退出) 待ち --------
        private async UniTask WaitForExitPressAsync(CancellationToken token)
        {
            _phase = Phase.WaitForExit;
            _exitSignal = new UniTaskCompletionSource();
            _resultInput?.Enable();
            try
            {
                await _exitSignal.Task.AttachExternalCancellation(token);
            }
            finally
            {
                _resultInput?.Disable();
            }
        }

        // -------- 抜ける演出 --------
        private async UniTask PlayExitEffectAsync(CancellationToken token)
        {
            _phase = Phase.Exiting;
            // 最後に流している BGM を止める
            Audio.StopBgm();
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- Stack 生成 --------
        private void SpawnStacks()
        {
            ClearStack(_p1);
            ClearStack(_p2);
            if (blockPrefab == null) return;
            SpawnStack(_p1, player1Stack);
            SpawnStack(_p2, player2Stack);
        }

        private void SpawnStack(PlayerState state, RectTransform stackRoot)
        {
            if (stackRoot == null) return;
            for (var i = 0; i < blockCount; i++)
            {
                var block = Instantiate(blockPrefab, stackRoot);
                block.name = $"Block_{i}";
                var rt = block.Rect;
                rt.anchoredPosition = new Vector2(0f, stackBottomY + i * blockSize.y);
                rt.sizeDelta = blockSize;
                block.Setup(RandomBlockType());
                state.blocks.Add(block);
            }
            state.blocksRemaining = blockCount;
        }

        private BlockDropBlock.BlockType RandomBlockType()
        {
            if (_rng.NextDouble() >= stickBlockChance) return BlockDropBlock.BlockType.Normal;
            return _rng.NextDouble() < 0.5
                ? BlockDropBlock.BlockType.StickLeft
                : BlockDropBlock.BlockType.StickRight;
        }

        private void ClearStack(PlayerState state)
        {
            foreach (var b in state.blocks) if (b != null) Destroy(b.gameObject);
            state.blocks.Clear();
        }

        // -------- 入力ハンドラ --------
        private void OnMove(int player, PlayerSide target)
        {
            // リザルトの再戦 / 退出は ResultInput (1P の A / B) が担当するのでここでは扱わない
            if (_phase != Phase.Playing) return;
            var state = player == 1 ? _p1 : _p2;
            if (state.isMoving || state.isLocked) return;
            if (state.side == target) return;
            MoveAsync(player, state, target).Forget();
        }

        // 移動演出は1フレームで移動先に到達させる
        private async UniTaskVoid MoveAsync(int player, PlayerState state, PlayerSide target)
        {
            state.isMoving = true;
            state.side = target;

            var character = player == 1 ? player1Character : player2Character;
            if (character != null)
            {
                var pos = character.anchoredPosition;
                pos.x = target == PlayerSide.Left ? characterLeftX : characterRightX;
                character.anchoredPosition = pos;
            }

            try { await UniTask.Yield(PlayerLoopTiming.Update, _roundCts?.Token ?? default); }
            catch (OperationCanceledException) { }

            state.isMoving = false;
        }

        private void OnKnock(int player)
        {
            // リザルトの再戦 / 退出は ResultInput (1P の A / B) が担当するのでここでは扱わない
            if (_phase != Phase.Playing) return;
            var state = player == 1 ? _p1 : _p2;
            if (state.isMoving || state.isLocked) return;
            if (state.blocks.Count == 0) return;
            KnockAsync(player, state).Forget();
        }

        private async UniTaskVoid KnockAsync(int player, PlayerState state)
        {
            state.isLocked = true;
            var token = _roundCts?.Token ?? default;

            var block = state.blocks[0];
            var nextBottom = state.blocks.Count > 1 ? state.blocks[1] : null;
            state.blocks.RemoveAt(0);
            state.blocksRemaining = state.blocks.Count;

            var penalty = nextBottom != null && (
                (nextBottom.Type == BlockDropBlock.BlockType.StickLeft  && state.side == PlayerSide.Left) ||
                (nextBottom.Type == BlockDropBlock.BlockType.StickRight && state.side == PlayerSide.Right));

            var flyToRight = state.side == PlayerSide.Left;

            try
            {
                // 1) 飛ばす演出
                await block.FlyAwayAsync(flyToRight, knockFlyDistance, knockFlyDuration, token);

                // 2) 残スタック全体を1段落下
                if (state.blocks.Count > 0)
                {
                    var dropTasks = new List<UniTask>(state.blocks.Count);
                    foreach (var b in state.blocks)
                        dropTasks.Add(b.DropAsync(blockSize.y, token));
                    await UniTask.WhenAll(dropTasks);
                }
            }
            catch (OperationCanceledException) { }

            if (block != null) Destroy(block.gameObject);

            if (penalty)
            {
                try { await BlinkCharacterAsync(player, penaltyDuration, token); }
                catch (OperationCanceledException) { }
            }

            state.isLocked = false;

            if (state.blocksRemaining <= 0 && _winner == 0)
            {
                _winner = player;
                Debug.Log($"[BlockDrop] Player {player} CLEAR! (blocks left = {state.blocksRemaining})");
                _winnerSignal?.TrySetResult();
            }
        }

        private UniTask BlinkCharacterAsync(int player, float duration, CancellationToken token)
        {
            var blinker = player == 1 ? player1CharacterBlinker : player2CharacterBlinker;
            if (blinker == null) return UniTask.CompletedTask;
            return blinker.BlinkAsync(duration, token);
        }

        // -------- View reset --------
        private void ResetInitialView()
        {
            _phase = Phase.Idle;
            ResetRoundView();
        }

        private void ResetRoundView()
        {
            if (introText != null) introText.text = string.Empty;
            if (countdownText != null) countdownText.text = string.Empty;
            if (winnerLabel != null) winnerLabel.Clear();
            if (resultGroup != null)
            {
                resultGroup.alpha = 0f;
                resultGroup.interactable = false;
                resultGroup.blocksRaycasts = false;
            }
            if (player1Character != null)
                player1Character.anchoredPosition = new Vector2(characterRightX, characterY);
            if (player2Character != null)
                player2Character.anchoredPosition = new Vector2(characterRightX, characterY);
        }

        private void ResetPlayerStates()
        {
            _p1.blocksRemaining = 0; _p1.side = PlayerSide.Right; _p1.isMoving = false; _p1.isLocked = false;
            _p2.blocksRemaining = 0; _p2.side = PlayerSide.Right; _p2.isMoving = false; _p2.isLocked = false;
        }
    }
}
