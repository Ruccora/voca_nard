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
            Opening,
            Playing,
            Goal,
            Winner,
            WaitForExit,
            Exiting,
        }

        // A = けん (わっか 1 つ) / B = ぱ (わっか 2 つ)
        private enum CellType { A, B }

        // コース頭の固定パターン「ぱ → けん → けん → ぱ」。
        // 先頭 (index 0) はキャラが最初に立つマスで、そこから けん・けん・ぱ を開始演出でデモする。
        private static readonly CellType[] IntroPattern =
            { CellType.B, CellType.A, CellType.A, CellType.B };

        private struct CellData
        {
            public CellType type;
            public bool isToggle;
            public int spriteIndex;   // わっか画像 (cellSprites) の種類。1P/2P で共有。
        }

        private class PlayerState
        {
            public int position;   // 0..cellCount-1 = on cell (0 = スタートマス)
            public bool isMoving;
            public bool isStopped;
            public float idleElapsed;  // 飛んでいない状態が続いている秒数 (待機アニメの判定用)
        }

        [Header("Common View")]
        [SerializeField] private TMP_Text goalText;
        [SerializeField] private CanvasGroup resultGroup;
        [Tooltip("勝者表示 (1P / 2P は画像、残りの文言は TMP)")]
        [SerializeField] private WinnerLabel winnerLabel;

        [Header("Player 1 (Top)")]
        [SerializeField] private RectTransform player1Track;
        [SerializeField] private RectTransform player1Character;
        [Tooltip("けん (わっか 1 つ) に飛ぶときのアニメーション")]
        [SerializeField] private SpriteAnimation player1KenAnim;
        [Tooltip("ぱ (わっか 2 つ) に飛ぶときのアニメーション")]
        [SerializeField] private SpriteAnimation player1PaAnim;

        [Header("Player 2 (Bottom)")]
        [SerializeField] private RectTransform player2Track;
        [SerializeField] private RectTransform player2Character;
        [Tooltip("けん (わっか 1 つ) に飛ぶときのアニメーション")]
        [SerializeField] private SpriteAnimation player2KenAnim;
        [Tooltip("ぱ (わっか 2 つ) に飛ぶときのアニメーション")]
        [SerializeField] private SpriteAnimation player2PaAnim;

        [Header("Config")]
        [SerializeField] private HopscotchCell cellPrefab;
        [SerializeField] private int cellCount = 30;
        [SerializeField] private float moveDuration = 0.4f;
        [SerializeField] private float toggleInterval = 1f;
        [SerializeField, Range(0f, 1f)] private float toggleCellChance = 0.2f;
        [SerializeField, Min(0)] private int toggleMinSpacing = 3;
        [SerializeField] private float goalHoldDuration = 1f;

        [Header("Miss (失敗)")]
        [Tooltip("失敗演出の秒数。この間はそのプレイヤーの入力を受け付けない")]
        [SerializeField] private float missLockDuration = 1f;
        [Tooltip("失敗時に飛び上がる高さ")]
        [SerializeField] private float missJumpHeight = 60f;
        [Tooltip("失敗時に傾ける Z 角度")]
        [SerializeField] private float missTiltAngle = 12f;
        [Tooltip("左右の切り替え回数 (4 = 左右左右)")]
        [SerializeField, Min(1)] private int missTiltCount = 4;

        [Header("Opening (けんけんぱ デモ)")]
        [Tooltip("明転を待ってからデモを始めるまでの待機秒数")]
        [SerializeField] private float openingStartWait = 0.4f;
        [Tooltip("デモ 1 マスぶんのジャンプ秒数")]
        [SerializeField] private float openingJumpDuration = 0.35f;
        [Tooltip("ジャンプとジャンプの間の待機秒数")]
        [SerializeField] private float openingJumpInterval = 0.1f;
        [Tooltip("デモ終了 → Ready 登場までの待機秒数")]
        [SerializeField] private float openingReadyWait = 0.15f;
        [Tooltip("Play Again からの再戦時、開始演出に入る前に挟む待機秒数")]
        [SerializeField] private float replayDelay = 1f;

        [Header("Opening / Ready (ScaleDown + FadeIn)")]
        [SerializeField] private RectTransform readyRect;
        [SerializeField] private CanvasGroup readyGroup;
        [Tooltip("Ready の登場時スケール (prefab のスケールに対する倍率)")]
        [SerializeField] private float readyStartScale = 2f;
        [Tooltip("Ready の到達スケール (prefab のスケールに対する倍率)")]
        [SerializeField] private float readyEndScale = 1f;
        [Tooltip("ScaleDown の尺")]
        [SerializeField] private float readyScaleDuration = 0.35f;
        [Tooltip("FadeIn の尺 (ScaleDown と同時に開始。別尺で指定できる)")]
        [SerializeField] private float readyFadeInDuration = 0.2f;
        [Tooltip("Ready を出したままの保持秒数")]
        [SerializeField] private float readyHoldDuration = 0.5f;

        [Header("Opening / Go (ScaleUp + FadeOut)")]
        [SerializeField] private RectTransform goRect;
        [SerializeField] private CanvasGroup goGroup;
        [Tooltip("Go の登場時スケール (prefab のスケールに対する倍率)")]
        [SerializeField] private float goStartScale = 1f;
        [Tooltip("Go の到達スケール (prefab のスケールに対する倍率)")]
        [SerializeField] private float goEndScale = 1.6f;
        [Tooltip("ScaleUp の尺。FadeOut はこの尺の途中から始まり、残り時間で消える")]
        [SerializeField] private float goScaleDuration = 0.6f;
        [Tooltip("ScaleUp の何割まで進んだら FadeOut を始めるか (0.5 = 半分)")]
        [SerializeField, Range(0f, 1f)] private float goFadeOutStartRatio = 0.5f;

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

        [Header("Idle (待機のたてゆれ)")]
        [Tooltip("飛んでいない状態がこの秒数続いたら待機アニメーションを始める")]
        [SerializeField] private float idleScaleDelay = 0.5f;
        [Tooltip("待機アニメーション 1 往復 (1 → min → 1) の秒数")]
        [SerializeField] private float idleScalePeriod = 0.6f;
        [Tooltip("縮んだときの Y スケール倍率")]
        [SerializeField, Range(0.1f, 1f)] private float idleScaleMinY = 0.9f;

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
        private Quaternion _p1CharacterHomeRot = Quaternion.identity;
        private Quaternion _p2CharacterHomeRot = Quaternion.identity;
        private Vector3 _p1CharacterHomeScale = Vector3.one;
        private Vector3 _p2CharacterHomeScale = Vector3.one;
        private int _p1ColorStart;
        private int _p2ColorStart;
        private float _readyHomeScale = 1f;
        private float _goHomeScale = 1f;
        private float _playElapsed;
        private CancellationTokenSource _roundCts;
        private InputAction _p1A, _p1D, _p2Left, _p2Right;
        private ResultInput _resultInput; // リザルトの A=再戦 / B=退出 (1P のみ)
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

            // けん = パッドの A / ぱ = パッドの B (割り当ては GamepadButtons)。
            // 1P/2P で同じボタンを張っておき、どちらのパッドかは PlayerDevices で振り分ける。
            _p1A = MakeAction("P1A", "<Keyboard>/a", GamepadButtons.A);
            _p1D = MakeAction("P1D", "<Keyboard>/d", GamepadButtons.B);
            _p2Left = MakeAction("P2Left", "<Keyboard>/leftArrow", GamepadButtons.A);
            _p2Right = MakeAction("P2Right", "<Keyboard>/rightArrow", GamepadButtons.B);

            _p1A.performed += ctx => { if (PlayerDevices.IsForPlayer(ctx, 1)) HandlePress(1, CellType.A); };
            _p1D.performed += ctx => { if (PlayerDevices.IsForPlayer(ctx, 1)) HandlePress(1, CellType.B); };
            _p2Left.performed += ctx => { if (PlayerDevices.IsForPlayer(ctx, 2)) HandlePress(2, CellType.A); };
            _p2Right.performed += ctx => { if (PlayerDevices.IsForPlayer(ctx, 2)) HandlePress(2, CellType.B); };

            _resultInput = new ResultInput(OnResultRetry, OnResultExit);

            CaptureHome();
            ResetInitialView();
            return UniTask.CompletedTask;
        }

        // prefab で設定された初期値 (Ready/Go の scale、キャラの回転) を記録し、以後のリセットで復元する。
        private void CaptureHome()
        {
            if (readyRect != null) _readyHomeScale = readyRect.localScale.x;
            if (goRect != null) _goHomeScale = goRect.localScale.x;
            if (player1Character != null)
            {
                _p1CharacterHomeRot = player1Character.localRotation;
                _p1CharacterHomeScale = player1Character.localScale;
            }
            if (player2Character != null)
            {
                _p2CharacterHomeRot = player2Character.localRotation;
                _p2CharacterHomeScale = player2Character.localScale;
            }
        }

        // 待機のたてゆれ。飛んでいない状態が idleScaleDelay 続いたらそこから Y スケールを
        // 1 → idleScaleMinY → 1 で往復させる。飛んでいる間 (移動 / 失敗演出) は 1 に戻す。
        private void Update()
        {
            UpdateIdleScale(_p1, player1Character, _p1CharacterHomeScale);
            UpdateIdleScale(_p2, player2Character, _p2CharacterHomeScale);
        }

        private void UpdateIdleScale(PlayerState state, RectTransform character, Vector3 homeScale)
        {
            if (character == null) return;

            if (state.isMoving || state.isStopped)
            {
                state.idleElapsed = 0f;
                character.localScale = homeScale;
                return;
            }

            state.idleElapsed += Time.deltaTime;
            if (state.idleElapsed < idleScaleDelay)
            {
                character.localScale = homeScale;
                return;
            }

            // cos なので開始時点 (t=0) がちょうど 1 倍。そこから縮んで戻るのを繰り返す。
            var t = (state.idleElapsed - idleScaleDelay) / Mathf.Max(0.01f, idleScalePeriod);
            var k = (1f + Mathf.Cos(t * Mathf.PI * 2f)) * 0.5f;
            var y = Mathf.Lerp(idleScaleMinY, 1f, k);
            character.localScale = new Vector3(homeScale.x, homeScale.y * y, homeScale.z);
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
            _resultInput?.Dispose();
        }

        private static InputAction MakeAction(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var binding in bindings) a.AddBinding(binding);
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
            _resultInput?.Disable();
        }

        // リザルト: 1P の A / Enter で再戦
        private void OnResultRetry()
        {
            if (IsAnimating) return;
            if (_phase != Phase.WaitForExit) return;
            _resultInput.Disable();
            Audio.PlaySE(SeKey.Decide);
            StartRound(replay: true);
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
                ResetPlayerStates();
                GenerateCourse();
                SpawnCells();
                RefreshCells(_p1Cells, _p1.position, _p1CharacterRest, player1Character);
                RefreshCells(_p2Cells, _p2.position, _p2CharacterRest, player2Character);

                await PlayOpeningAsync(token, replay);

                // Go の登場と同時に操作開始。Go の ScaleUp / FadeOut はプレイと並行に走らせる。
                PlayGoAsync(token).Forget();
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
                // 頭 4 マスは ぱ・けん・けん・ぱ で固定 (index 0 = スタート、以降が開始演出のデモ)。トグルも置かない。
                var isIntro = i < IntroPattern.Length;
                var canBeToggle = !isIntro && cellsSinceToggle >= toggleMinSpacing;
                var isToggle = canBeToggle && rng.NextDouble() < toggleCellChance;
                _course.Add(new CellData
                {
                    type = isIntro ? IntroPattern[i] : (rng.NextDouble() < 0.5 ? CellType.A : CellType.B),
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

            for (var i = 0; i < _course.Count; i++)
            {
                if (player1Track != null) _p1Cells.Add(CreateCell(player1Track, i, _course[i], _p1ColorStart));
                if (player2Track != null) _p2Cells.Add(CreateCell(player2Track, i, _course[i], _p2ColorStart));
            }
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
            var len = cellColors.Length;
            return cellColors[(colorStart + courseIndex) % len];
        }

        // -------- Perspective rendering --------
        // cells[i] は _course[i] に対応 (cells[0] = スタートマス)
        private void RefreshCells(List<HopscotchCell> cells, float currentPosition, Vector2 anchor, RectTransform character)
        {
            var near = anchor + nearSlotOffset;
            var vanish = anchor + vanishingOffset;
            var span = Mathf.Max(1, visibleAhead);

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                // distance 0 = 足元, 正 = 前方
                var distance = i - currentPosition;

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
        // 黒フェード(遷移の明転)を待つ → 1P/2P 同時に けん・けん・ぱ を自動で 3 マス飛ぶ
        // → Ready 登場。Go は呼び出し側が並行で回す。デモで進んだぶんはそのまま本番の開始位置になる。
        private async UniTask PlayOpeningAsync(CancellationToken token, bool replay)
        {
            _phase = Phase.Opening;

            // 黒フェードで入ってくる (明転自体は ScreenController が持つ)。見えてから演出を始める。
            if (ScreenController.Instance != null)
                await ScreenController.Instance.WaitForTransitionFadeAsync(token);

            // 再戦時はリザルトから間を置かず始まらないよう一拍待つ (QuickDraw と同じ)
            if (replay && replayDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(replayDelay), cancellationToken: token);

            if (openingStartWait > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(openingStartWait), cancellationToken: token);

            await PlayKenKenPaDemoAsync(token);

            if (openingReadyWait > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(openingReadyWait), cancellationToken: token);

            await PlayReadyAsync(token);
        }

        // コース頭の けん・けん・ぱ を 1P/2P 同時に飛ぶデモ。
        // index 0 はキャラが立っているスタートマスなので、飛ぶのは 1 マス目から。
        private async UniTask PlayKenKenPaDemoAsync(CancellationToken token)
        {
            var count = Mathf.Min(IntroPattern.Length, _course.Count);
            for (var i = 1; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.WhenAll(
                    JumpAsync(1, _p1, i, openingJumpDuration, token),
                    JumpAsync(2, _p2, i, openingJumpDuration, token));

                if (openingJumpInterval > 0f && i < count - 1)
                    await UniTask.Delay(TimeSpan.FromSeconds(openingJumpInterval), cancellationToken: token);
            }
        }

        // Ready: ScaleDown と FadeIn を同時に開始する (それぞれ別の尺)
        private async UniTask PlayReadyAsync(CancellationToken token)
        {
            SetGroupAlpha(goGroup, 0f);
            SetScale(readyRect, _readyHomeScale * readyStartScale);
            SetGroupAlpha(readyGroup, 0f);

            await UniTask.WhenAll(
                LerpScaleAsync(readyRect, _readyHomeScale * readyStartScale, _readyHomeScale * readyEndScale,
                    readyScaleDuration, EaseOutCubic, token),
                FadeGroupAsync(readyGroup, 0f, 1f, readyFadeInDuration, token));

            if (readyHoldDuration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(readyHoldDuration), cancellationToken: token);
        }

        // Go: ScaleUp しつつ、goFadeOutStartRatio まで進んだところから残り時間で FadeOut。
        // 呼び出しと同時に Playing (操作可能) に入るので、この演出はプレイと並行して走る。
        private async UniTaskVoid PlayGoAsync(CancellationToken token)
        {
            SetGroupAlpha(readyGroup, 0f);
            SetScale(goRect, _goHomeScale * goStartScale);
            SetGroupAlpha(goGroup, 1f);

            try
            {
                var duration = goScaleDuration;
                if (duration <= 0f)
                {
                    SetScale(goRect, _goHomeScale * goEndScale);
                    SetGroupAlpha(goGroup, 0f);
                    return;
                }

                var fadeStart = duration * goFadeOutStartRatio;
                var fadeDuration = duration - fadeStart;
                var from = _goHomeScale * goStartScale;
                var to = _goHomeScale * goEndScale;

                var elapsed = 0f;
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    SetScale(goRect, Mathf.Lerp(from, to, EaseOutCubic(t)));
                    var fade = fadeDuration <= 0f ? 1f : Mathf.Clamp01((elapsed - fadeStart) / fadeDuration);
                    SetGroupAlpha(goGroup, 1f - fade);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                SetScale(goRect, to);
                SetGroupAlpha(goGroup, 0f);
            }
            catch (OperationCanceledException)
            {
            }
        }

        // -------- Stage 3: プレイ --------
        private async UniTask PlayGameAsync(CancellationToken token)
        {
            // _playElapsed / _winner は ResetPlayerStates で初期化済み
            _phase = Phase.Playing;
            _goalSignal = new UniTaskCompletionSource();

            while (!_goalSignal.Task.Status.IsCompleted())
            {
                token.ThrowIfCancellationRequested();
                _playElapsed += Time.deltaTime;
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

        // -------- Stage 6: 1P の A (再戦) / B (退出) 待ち --------
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

        // -------- Stage 7: 抜ける演出 --------
        private async UniTask PlayExitEffectAsync(CancellationToken token)
        {
            _phase = Phase.Exiting;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- Toggle visuals --------
        private bool IsToggleOn() => ((int)(_playElapsed / toggleInterval)) % 2 == 0;

        // -------- 入力処理 --------
        private void HandlePress(int player, CellType keyType)
        {
            // リザルトの再戦 / 退出は ResultInput (1P の A / B) が担当するのでここでは扱わない
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
            var token = _roundCts?.Token ?? default;
            await JumpAsync(player, state, targetIndex, moveDuration, token);

            if (state.position >= _course.Count - 1 && _winner == 0)
            {
                _winner = player;
                Debug.Log($"[Hopscotch] Player {player} CLEAR! (position = {state.position}, course = {_course.Count})");
                _goalSignal?.TrySetResult();
            }
        }

        // 1 マスぶんのジャンプ。開始演出のデモと本番の移動で共有する (尺だけ差し替える)。
        private async UniTask JumpAsync(int player, PlayerState state, int targetIndex, float duration, CancellationToken token)
        {
            state.isMoving = true;
            // 飛び先が けん か ぱ かでアニメーションを切り替え、跳んでいる間に再生する
            ShowCellAnim(player, targetIndex, play: true);
            var cells = player == 1 ? _p1Cells : _p2Cells;
            var character = player == 1 ? player1Character : player2Character;
            var restPos = player == 1 ? _p1CharacterRest : _p2CharacterRest;
            var startCurrent = (float)state.position;
            var endCurrent = (float)targetIndex;

            try
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    var currentPos = Mathf.Lerp(startCurrent, endCurrent, EaseOutCubic(t));
                    RefreshCells(cells, currentPos, restPos, character);

                    // Character jump — sine wave over the same duration
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
        }

        // けん / ぱ の SpriteAnimation を切り替える。使う方だけ表示し、もう片方は隠す。
        // play = true で頭から再生 (跳ぶとき)、false なら最終フレームで静止 (着地して待機している状態)。
        // 範囲外 (コース未生成など) は ぱ 扱い。
        private void ShowCellAnim(int player, int courseIndex, bool play)
        {
            var isPa = courseIndex < 0 || courseIndex >= _course.Count
                || _course[courseIndex].type == CellType.B;

            var ken = player == 1 ? player1KenAnim : player2KenAnim;
            var pa = player == 1 ? player1PaAnim : player2PaAnim;
            var target = isPa ? pa : ken;
            var other = isPa ? ken : pa;

            if (other != null && other.gameObject.activeSelf) other.gameObject.SetActive(false);
            if (target == null) return;
            if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);

            if (play) target.Play();
            else if (target.Length > 0) target.SetFrame(target.Length - 1);
        }

        private async UniTaskVoid StopAsync(int player, PlayerState state)
        {
            state.isStopped = true;
            var token = _roundCts?.Token ?? default;

            try
            {
                await PlayMissJumpAsync(player, missLockDuration, token);
            }
            catch (OperationCanceledException) { }
            state.isStopped = false;
        }

        // 失敗: その場で 1 回飛び上がりつつ、左右左右 と交互に傾く (missLockDuration ぶん)。
        // マスは進まないので RefreshCells は触らず、キャラの位置と回転だけ動かす。
        private async UniTask PlayMissJumpAsync(int player, float duration, CancellationToken token)
        {
            var character = player == 1 ? player1Character : player2Character;
            var restPos = player == 1 ? _p1CharacterRest : _p2CharacterRest;
            var homeRot = player == 1 ? _p1CharacterHomeRot : _p2CharacterHomeRot;

            if (character == null)
            {
                if (duration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
                return;
            }

            var steps = Mathf.Max(1, missTiltCount);
            try
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);

                    // 飛び上がり: 尺いっぱいで 1 回の山なり
                    character.anchoredPosition = restPos + new Vector2(0f, missJumpHeight * Mathf.Sin(t * Mathf.PI));

                    // 左右左右: 尺を steps 等分して ±missTiltAngle を交互に当てる (左から始まる)
                    var sign = Mathf.FloorToInt(t * steps) % 2 == 0 ? 1f : -1f;
                    character.localRotation = homeRot * Quaternion.Euler(0f, 0f, missTiltAngle * sign);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                character.anchoredPosition = restPos;
                character.localRotation = homeRot;
            }
        }

        // -------- View reset --------
        private void ResetInitialView()
        {
            _phase = Phase.Idle;
            ResetRoundView();
        }

        // Play Again も初回と同じ状態から始まるよう、ここで「開始時の見た目」を全部作り直す。
        private void ResetRoundView()
        {
            if (goalText != null) goalText.text = string.Empty;
            if (winnerLabel != null) winnerLabel.Clear();
            if (resultGroup != null)
            {
                resultGroup.alpha = 0f;
                resultGroup.interactable = false;
                resultGroup.blocksRaycasts = false;
            }

            // 開始演出の初期化 (Ready/Go は透明のまま開始スケールに戻す)
            SetGroupAlpha(readyGroup, 0f);
            SetGroupAlpha(goGroup, 0f);
            SetScale(readyRect, _readyHomeScale * readyStartScale);
            SetScale(goRect, _goHomeScale * goStartScale);

            // ジャンプ / 失敗演出の途中で打ち切られていても足元・無回転・等倍に戻す
            if (player1Character != null)
            {
                player1Character.anchoredPosition = _p1CharacterRest;
                player1Character.localRotation = _p1CharacterHomeRot;
                player1Character.localScale = _p1CharacterHomeScale;
            }
            if (player2Character != null)
            {
                player2Character.anchoredPosition = _p2CharacterRest;
                player2Character.localRotation = _p2CharacterHomeRot;
                player2Character.localScale = _p2CharacterHomeScale;
            }
        }

        private void ResetPlayerStates()
        {
            _p1.position = 0; _p1.isMoving = false; _p1.isStopped = false; _p1.idleElapsed = 0f;
            _p2.position = 0; _p2.isMoving = false; _p2.isStopped = false; _p2.idleElapsed = 0f;
            _winner = 0;
            _playElapsed = 0f;

            // スタートマス (index 0) は ぱ なので、キャラも ぱ のアニメーションの最終フレームで待機する。
            ShowCellAnim(1, 0, play: false);
            ShowCellAnim(2, 0, play: false);
        }

        // -------- Tween helpers --------
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

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

        private static void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg == null) return;
            cg.alpha = alpha;
        }

        private static void SetScale(RectTransform rt, float s)
        {
            if (rt == null) return;
            rt.localScale = new Vector3(s, s, 1f);
        }
    }
}