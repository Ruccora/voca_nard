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
            public int spriteIndex;   // わっか画像 (cellSprites) の種類。1P/2P で共有。
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

        [Header("Depth (DOOM64-style)")]
        [SerializeField, Min(1)] private int visibleAhead = 4;                    // 前方に見せる障害物数 (敵4体)
        [SerializeField, Min(0)] private int visibleBehind = 0;
        [SerializeField] private Vector2 nearSlotOffset = new Vector2(0f, 0f);    // 最手前(足元)スロットの相対位置
        [SerializeField] private Vector2 vanishingOffset = new Vector2(0f, 480f); // 消失点の相対位置 (奥・画面中央上)
        [SerializeField, Range(0.3f, 1f)] private float depthFalloff = 0.72f;     // 1段奥ごとのスケール
        [SerializeField, Range(0f, 1f)] private float darkSlot3 = 0.7f;           // 3体目の明暗 (Multiply)
        [SerializeField, Range(0f, 1f)] private float darkSlot4 = 0.45f;          // 4体目の明暗 (Multiply)

        [Header("Character Jump")]
        [SerializeField] private float jumpHeight = 80f;

        [Header("Cell Appearance")]
        [SerializeField] private Sprite[] cellSprites;               // わっか画像 5種 (マスごとにランダム)
        [SerializeField] private Color[] cellColors = new[]          // 開始色からこの順でループ
        {
            new Color(0.96470588f, 0.92156863f, 0.41176471f), // #f6eb69 きいろ
            new Color(0.83921569f, 0.28235294f, 0.30588235f), // #d6484e あか
            new Color(0.29411765f, 0.29411765f, 0.92941176f), // #4b4bed あお
        };

        private Phase _phase;

        public override bool CanAcceptBack => _phase == Phase.Winner || _phase == Phase.WaitForExit;
        private readonly List<CellData> _course = new List<CellData>();
        private readonly List<HopscotchCell> _p1Cells = new List<HopscotchCell>();
        private readonly List<HopscotchCell> _p2Cells = new List<HopscotchCell>();
        private readonly PlayerState _p1 = new PlayerState();
        private readonly PlayerState _p2 = new PlayerState();
        private Vector2 _p1CharacterRest;
        private Vector2 _p2CharacterRest;
        private int _p1ColorStart;
        private int _p2ColorStart;
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
                RefreshCells(_p1Cells, _p1.position, _p1CharacterRest, player1Character);
                RefreshCells(_p2Cells, _p2.position, _p2CharacterRest, player2Character);

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
        // コースは 1 本だけランダム生成し、1P/2P の両トラックに同じものを当てる。
        // (マスの A/B・トグル・わっか画像は共有。色だけ開始位置がプレイヤーごとに変わる)
        private void GenerateCourse()
        {
            _course.Clear();
            var rng = new System.Random();
            var spriteVariants = cellSprites != null && cellSprites.Length > 0 ? cellSprites.Length : 1;
            var cellsSinceToggle = int.MaxValue;
            for (var i = 0; i < cellCount; i++)
            {
                var canBeToggle = cellsSinceToggle >= toggleMinSpacing;
                var isToggle = canBeToggle && rng.NextDouble() < toggleCellChance;
                _course.Add(new CellData
                {
                    type = rng.NextDouble() < 0.5 ? CellType.A : CellType.B,
                    isToggle = isToggle,
                    spriteIndex = rng.Next(spriteVariants),
                });
                cellsSinceToggle = isToggle ? 0 : cellsSinceToggle + 1;
            }

            // 色は 3 種。開始色だけをプレイヤーごとにランダムに決め、以降はその順でループさせる。
            // (例: 1P きいろ始まり → きいろ→あか→あお→きいろ…) 常に隣のマスと別の色になる。
            var colorVariants = cellColors != null && cellColors.Length > 0 ? cellColors.Length : 1;
            _p1ColorStart = rng.Next(colorVariants);
            _p2ColorStart = rng.Next(colorVariants);
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
                if (player1Track != null) _p1Cells.Add(CreateCell(player1Track, i, _course[i], _p1ColorStart));
                if (player2Track != null) _p2Cells.Add(CreateCell(player2Track, i, _course[i], _p2ColorStart));
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

        private HopscotchCell CreateCell(RectTransform parent, int index, CellData data, int colorStart)
        {
            var cell = Instantiate(cellPrefab, parent);
            cell.name = $"Cell_{index}";
            cell.Setup(data.type == CellType.A, data.isToggle,
                GetCellSprite(data.spriteIndex), GetCellColor(colorStart, index));
            return cell;
        }

        private Sprite GetCellSprite(int spriteIndex)
        {
            if (cellSprites == null || cellSprites.Length == 0) return null;
            return cellSprites[Mathf.Abs(spriteIndex) % cellSprites.Length];
        }

        // colorStart から cellColors 順にループ。連続する 2 マスが同色になることはない。
        private Color GetCellColor(int colorStart, int courseIndex)
        {
            if (cellColors == null || cellColors.Length == 0) return Color.white;
            return cellColors[Mathf.Abs(colorStart + courseIndex) % cellColors.Length];
        }

        // -------- Perspective rendering --------
        // cells[0] は start cell (virtual course index -1)
        // cells[1..] は _course[0..cellCount-1] に対応
        private void RefreshCells(List<HopscotchCell> cells, float currentPosition, Vector2 anchor, RectTransform character)
        {
            var near = anchor + nearSlotOffset;
            var vanish = anchor + vanishingOffset;
            var span = Mathf.Max(1, visibleAhead);

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                // cells[i] = course index (i - 1)。distance 0 = 足元, 正 = 前方
                var virtualIndex = i - 1;
                var distance = virtualIndex - currentPosition;

                if (distance < -(visibleBehind + 0.5f) || distance > visibleAhead + 0.5f)
                {
                    if (cell.gameObject.activeSelf) cell.gameObject.SetActive(false);
                    continue;
                }
                if (!cell.gameObject.activeSelf) cell.gameObject.SetActive(true);

                // DOOM64 風: 手前→奥で消失点(中央上)へ収束しつつスケール縮小
                var t = Mathf.Clamp01(distance / span);
                cell.Rect.anchoredPosition = Vector2.LerpUnclamped(near, vanish, t);
                var scale = Mathf.Pow(depthFalloff, Mathf.Max(0f, distance));
                cell.Rect.localScale = new Vector3(scale, scale, 1f);

                // 明暗: 1,2体目=通常 / 3体目=少し暗く / 4体目=さらに暗く
                var rank = Mathf.Clamp(Mathf.CeilToInt(distance), 1, 4);
                var darken = rank <= 2 ? 1f : (rank == 3 ? darkSlot3 : darkSlot4);
                cell.SetDarken(darken);
            }

            // 描画順: 手前(index 小)を最前面へ。上から見下ろして近い対象が重なりの上に来る。
            for (var i = cells.Count - 1; i >= 0; i--)
            {
                var cell = cells[i];
                if (cell == null || !cell.gameObject.activeSelf) continue;
                cell.Rect.SetAsLastSibling();
            }
            // 自キャラは最前面
            if (character != null) character.SetAsLastSibling();
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

            if (SaveData.TrySetBestTime(SaveData.GameId.HopscotchRace, _playElapsed))
                Debug.Log($"[Hopscotch] New best time: {_playElapsed:0.00}s");
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
            SetFocus(playAgainButton);
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
                    RefreshCells(cells, currentPos, restPos, character);

                    // Character jump — sine wave over the same moveDuration
                    if (character != null)
                    {
                        var jumpY = jumpHeight * Mathf.Sin(t * Mathf.PI);
                        character.anchoredPosition = restPos + new Vector2(0f, jumpY);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                RefreshCells(cells, endCurrent, restPos, character);
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