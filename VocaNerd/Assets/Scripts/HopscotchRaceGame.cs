using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VocaNerd
{
    public class HopscotchRaceGame : PanelBase
    {
        private enum Phase
        {
            Idle,
            Intro,
            Countdown,
            Playing,
            Goal,
            Winner,
            WaitForExit,
            Exiting,
        }

        private enum CellType { A, B }

        private struct CellData
        {
            public CellType type;
            public bool isToggle;
        }

        private class PlayerState
        {
            public int position;   // -1 = before start, 0..cellCount-1 = on cell
            public bool isMoving;
            public bool isStopped;
        }

        [Header("Common View")]
        [SerializeField] private TMP_Text introText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text goalText;
        [SerializeField] private CanvasGroup resultGroup;
        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private Button playAgainButton;

        [Header("Player 1 (Top)")]
        [SerializeField] private RectTransform player1Track;
        [SerializeField] private RectTransform player1Character;
        [SerializeField] private CanvasGroupBlinker player1CharacterBlinker;

        [Header("Player 2 (Bottom)")]
        [SerializeField] private RectTransform player2Track;
        [SerializeField] private RectTransform player2Character;
        [SerializeField] private CanvasGroupBlinker player2CharacterBlinker;

        [Header("Config")]
        [SerializeField] private HopscotchCell cellPrefab;
        [SerializeField] private HopscotchCell startCellPrefab;
        [SerializeField] private int cellCount = 30;
        [SerializeField] private float moveDuration = 0.4f;
        [SerializeField] private float missLockDuration = 0.3f;
        [SerializeField] private float toggleInterval = 1f;
        [SerializeField, Range(0f, 1f)] private float toggleCellChance = 0.2f;
        [SerializeField, Min(0)] private int toggleMinSpacing = 3;
        [SerializeField] private float introDuration = 1.2f;
        [SerializeField] private float countdownStep = 1f;
        [SerializeField] private float goalHoldDuration = 1f;

        [Header("Perspective")]
        [SerializeField] private Vector2 characterAnchor = new Vector2(-250f, -400f);
        [SerializeField] private Vector2 cellStepOffset = new Vector2(70f, 70f);
        [SerializeField, Range(0.5f, 1f)] private float scaleFalloff = 0.85f;
        [SerializeField, Min(1)] private int maxVisibleAhead = 10;
        [SerializeField, Min(0)] private int visibleBehind = 2;

        [Header("Character Jump")]
        [SerializeField] private float jumpHeight = 80f;
        [SerializeField] private Vector2 feetOffset = new Vector2(0f, -60f);

        private Phase _phase;
        private readonly List<CellData> _course = new List<CellData>();
        private readonly List<HopscotchCell> _p1Cells = new List<HopscotchCell>();
        private readonly List<HopscotchCell> _p2Cells = new List<HopscotchCell>();
        private readonly PlayerState _p1 = new PlayerState();
        private readonly PlayerState _p2 = new PlayerState();
        private Vector2 _p1CharacterRest;
        private Vector2 _p2CharacterRest;
        private float _playElapsed;
        private CancellationTokenSource _roundCts;
        private InputAction _p1A, _p1D, _p2Left, _p2Right;
        private UniTaskCompletionSource _exitSignal;
        private UniTaskCompletionSource _goalSignal;
        private int _winner;
        private bool _isSetup;

        public override UniTask SetupAsync(CancellationToken token)
        {
            if (_isSetup) return UniTask.CompletedTask;
            _isSetup = true;

            if (player1Character != null) _p1CharacterRest = player1Character.anchoredPosition;
            if (player2Character != null) _p2CharacterRest = player2Character.anchoredPosition;

            _p1A = MakeAction("P1A", "<Keyboard>/a");
            _p1D = MakeAction("P1D", "<Keyboard>/d");
            _p2Left = MakeAction("P2Left", "<Keyboard>/leftArrow");
            _p2Right = MakeAction("P2Right", "<Keyboard>/rightArrow");

            _p1A.performed += _ => HandlePress(1, CellType.A);
            _p1D.performed += _ => HandlePress(1, CellType.B);
            _p2Left.performed += _ => HandlePress(2, CellType.A);
            _p2Right.performed += _ => HandlePress(2, CellType.B);

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
            _p1A?.Dispose();
            _p1D?.Dispose();
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
            _p1A?.Enable(); _p1D?.Enable();
            _p2Left?.Enable(); _p2Right?.Enable();
        }

        private void DisableInputs()
        {
            _p1A?.Disable(); _p1D?.Disable();
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
                GenerateCourse();
                SpawnCells();
                RefreshCells(_p1Cells, _p1.position, _p1CharacterRest);
                RefreshCells(_p2Cells, _p2.position, _p2CharacterRest);

                await PlayIntroEffectAsync(token);
                await PlayCountdownAsync(token);
                await PlayGameAsync(token);
                await PlayGoalEffectAsync(token);
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

        // -------- Course generation --------
        private void GenerateCourse()
        {
            _course.Clear();
            var rng = new System.Random();
            var cellsSinceToggle = int.MaxValue;
            for (var i = 0; i < cellCount; i++)
            {
                var canBeToggle = cellsSinceToggle >= toggleMinSpacing;
                var isToggle = canBeToggle && rng.NextDouble() < toggleCellChance;
                _course.Add(new CellData
                {
                    type = rng.NextDouble() < 0.5 ? CellType.A : CellType.B,
                    isToggle = isToggle,
                });
                cellsSinceToggle = isToggle ? 0 : cellsSinceToggle + 1;
            }
        }

        private void SpawnCells()
        {
            ClearCells(_p1Cells);
            ClearCells(_p2Cells);
            if (cellPrefab == null) return;

            // Start cell at index 0 (virtual course index -1)
            if (player1Track != null) _p1Cells.Add(CreateStartCell(player1Track));
            if (player2Track != null) _p2Cells.Add(CreateStartCell(player2Track));

            for (var i = 0; i < _course.Count; i++)
            {
                if (player1Track != null) _p1Cells.Add(CreateCell(player1Track, i, _course[i]));
                if (player2Track != null) _p2Cells.Add(CreateCell(player2Track, i, _course[i]));
            }
        }

        private HopscotchCell CreateStartCell(RectTransform parent)
        {
            var prefab = startCellPrefab != null ? startCellPrefab : cellPrefab;
            var cell = Instantiate(prefab, parent);
            cell.name = "Cell_Start";
            return cell;
        }

        private static void ClearCells(List<HopscotchCell> list)
        {
            foreach (var c in list) if (c != null) Destroy(c.gameObject);
            list.Clear();
        }

        private HopscotchCell CreateCell(RectTransform parent, int index, CellData data)
        {
            var cell = Instantiate(cellPrefab, parent);
            cell.name = $"Cell_{index}";
            cell.Setup(data.type == CellType.A, data.isToggle);
            return cell;
        }

        // -------- Perspective rendering --------
        // cells[0] は start cell (virtual course index -1)
        // cells[1..] は _course[0..cellCount-1] に対応
        private void RefreshCells(List<HopscotchCell> cells, float currentPosition, Vector2 anchor)
        {
            var feetAnchor = anchor + feetOffset;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                // cell at cells[i] represents course index (i - 1)
                // → cell が currentPosition と一致 (足元) のとき distance = 0
                var virtualIndex = i - 1;
                var distance = virtualIndex - currentPosition;

                if (distance < -(visibleBehind + 0.5f) || distance > maxVisibleAhead)
                {
                    if (cell.gameObject.activeSelf) cell.gameObject.SetActive(false);
                    continue;
                }
                if (!cell.gameObject.activeSelf) cell.gameObject.SetActive(true);

                var scale = Mathf.Pow(scaleFalloff, distance);
                cell.Rect.localScale = new Vector3(scale, scale, 1f);
                cell.Rect.anchoredPosition = feetAnchor + cellStepOffset * distance;
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

        // -------- Stage 3: プレイ --------
        private async UniTask PlayGameAsync(CancellationToken token)
        {
            _phase = Phase.Playing;
            _playElapsed = 0f;
            _winner = 0;
            _goalSignal = new UniTaskCompletionSource();

            while (!_goalSignal.Task.Status.IsCompleted())
            {
                token.ThrowIfCancellationRequested();
                _playElapsed += Time.deltaTime;
                UpdateToggleVisuals();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        // -------- Stage 4: ゴール演出 --------
        private async UniTask PlayGoalEffectAsync(CancellationToken token)
        {
            _phase = Phase.Goal;
            if (goalText != null) goalText.text = $"GOAL! P{_winner}";
            await UniTask.Delay(TimeSpan.FromSeconds(goalHoldDuration), cancellationToken: token);
            if (goalText != null) goalText.text = string.Empty;
        }

        // -------- Stage 5: 勝利演出 --------
        private async UniTask PlayWinnerEffectAsync(CancellationToken token)
        {
            _phase = Phase.Winner;
            if (winnerText != null) winnerText.text = $"Player {_winner} Wins!";
            if (resultGroup != null)
            {
                resultGroup.alpha = 1f;
                resultGroup.interactable = true;
                resultGroup.blocksRaycasts = true;
            }
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- Stage 6: 任意ボタン待ち --------
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

        // -------- Toggle visuals --------
        private bool IsToggleOn() => ((int)(_playElapsed / toggleInterval)) % 2 == 0;

        private void UpdateToggleVisuals()
        {
            var on = IsToggleOn();
            UpdateToggleList(_p1Cells, on);
            UpdateToggleList(_p2Cells, on);
        }

        private void UpdateToggleList(List<HopscotchCell> list, bool on)
        {
            // list[0] は start cell。list[i+1] が _course[i] に対応するので +1 オフセット。
            for (var i = 0; i < _course.Count; i++)
            {
                if (!_course[i].isToggle) continue;
                var cellIndex = i + 1;
                if (cellIndex >= list.Count) continue;
                var cell = list[cellIndex];
                if (cell != null) cell.SetToggleState(on);
            }
        }

        // -------- 入力処理 --------
        private void HandlePress(int player, CellType keyType)
        {
            if (_phase == Phase.WaitForExit)
            {
                if (_exitSignal == null || _exitSignal.Task.Status.IsCompleted()) return;
                _phase = Phase.Exiting;
                _exitSignal.TrySetResult();
                return;
            }
            if (_phase != Phase.Playing) return;

            var state = player == 1 ? _p1 : _p2;
            if (state.isMoving || state.isStopped) return;
            if (_winner != 0) return;

            var targetIndex = state.position + 1;
            if (targetIndex >= _course.Count) return;

            var target = _course[targetIndex];
            var correctKey = keyType == target.type;
            var toggleBlocks = target.isToggle && !IsToggleOn();

            if (correctKey && !toggleBlocks)
                MoveAsync(player, state, targetIndex).Forget();
            else
                StopAsync(player, state).Forget();
        }

        private async UniTaskVoid MoveAsync(int player, PlayerState state, int targetIndex)
        {
            state.isMoving = true;
            var cells = player == 1 ? _p1Cells : _p2Cells;
            var character = player == 1 ? player1Character : player2Character;
            var restPos = player == 1 ? _p1CharacterRest : _p2CharacterRest;
            var startCurrent = (float)state.position;
            var endCurrent = (float)targetIndex;
            var token = _roundCts?.Token ?? default;

            try
            {
                var elapsed = 0f;
                while (elapsed < moveDuration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / moveDuration);
                    var eased = 1f - Mathf.Pow(1f - t, 3f);
                    var currentPos = Mathf.Lerp(startCurrent, endCurrent, eased);
                    RefreshCells(cells, currentPos, restPos);

                    // Character jump — sine wave over the same moveDuration
                    if (character != null)
                    {
                        var jumpY = jumpHeight * Mathf.Sin(t * Mathf.PI);
                        character.anchoredPosition = restPos + new Vector2(0f, jumpY);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                RefreshCells(cells, endCurrent, restPos);
                if (character != null) character.anchoredPosition = restPos;
            }
            catch (OperationCanceledException)
            {
                if (character != null) character.anchoredPosition = restPos;
            }

            state.position = targetIndex;
            state.isMoving = false;

            if (state.position >= _course.Count - 1 && _winner == 0)
            {
                _winner = player;
                Debug.Log($"[Hopscotch] Player {player} CLEAR! (position = {state.position}, course = {_course.Count})");
                _goalSignal?.TrySetResult();
            }
        }

        private async UniTaskVoid StopAsync(int player, PlayerState state)
        {
            state.isStopped = true;
            var token = _roundCts?.Token ?? default;

            var blinker = player == 1 ? player1CharacterBlinker : player2CharacterBlinker;
            if (blinker != null)
                blinker.BlinkAsync(missLockDuration, token).Forget();

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(missLockDuration), cancellationToken: token);
            }
            catch (OperationCanceledException) { }
            state.isStopped = false;
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
            if (goalText != null) goalText.text = string.Empty;
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
            _p1.position = -1; _p1.isMoving = false; _p1.isStopped = false;
            _p2.position = -1; _p2.isMoving = false; _p2.isStopped = false;
        }
    }
}