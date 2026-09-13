using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
            public float visualPosition;  // 描画上の位置。ジャンプ中は position の間を小数で動く
            public bool isMoving;
            public bool isStopped;
            public float idleElapsed;  // 飛んでいない状態が続いている秒数 (待機アニメの判定用)
        }

        [Header("Common View")]
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
        [Tooltip("開始時は表示しておき、Go の演出が終わったら隠すオブジェクト (操作説明など)")]
        [SerializeField] private GameObject player1IntroObject;

        [Header("Player 2 (Bottom)")]
        [SerializeField] private RectTransform player2Track;
        [SerializeField] private RectTransform player2Character;
        [Tooltip("けん (わっか 1 つ) に飛ぶときのアニメーション")]
        [SerializeField] private SpriteAnimation player2KenAnim;
        [Tooltip("ぱ (わっか 2 つ) に飛ぶときのアニメーション")]
        [SerializeField] private SpriteAnimation player2PaAnim;
        [Tooltip("開始時は表示しておき、Go の演出が終わったら隠すオブジェクト (操作説明など)")]
        [SerializeField] private GameObject player2IntroObject;

        [Header("Config")]
        [SerializeField] private HopscotchCell cellPrefab;
        [SerializeField] private int cellCount = 30;
        [SerializeField] private float moveDuration = 0.4f;
        [SerializeField] private float toggleInterval = 1f;
        [SerializeField, Range(0f, 1f)] private float toggleCellChance = 0.2f;
        [SerializeField, Min(0)] private int toggleMinSpacing = 3;
        [Tooltip("けん / ぱ が続いてよい最大数。これを超えないよう次のマスは反対側になる")]
        [SerializeField, Min(1)] private int maxSameTypeRun = 3;
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

        [Header("Goal (ゴール演出)")]
        [Tooltip("ゴール後、足元のマスのアニメーションを何回ループ再生してから Result を出すか")]
        [SerializeField, Min(1)] private int goalAnimPlayCount = 2;
        [Tooltip("Result 表示後、勝者がその場でジャンプする間隔 (秒)")]
        [SerializeField, Min(0.1f)] private float winnerHopInterval = 1f;
        [Tooltip("その場ジャンプ 1 回の秒数")]
        [SerializeField, Min(0.05f)] private float winnerHopDuration = 0.4f;

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

        [Header("Course Layout")]
        [Tooltip("前方に見せるマス数")]
        [SerializeField, Min(1)] private int visibleAhead = 4;
        [Tooltip("後方 (通過済み) に見せるマス数")]
        [SerializeField, Min(0)] private int visibleBehind = 0;
        [Tooltip("足元 (distance 0) のマスの位置。0 でキャラの足元とちょうど重なる。ここからのズラし量を入れる")]
        [SerializeField] private Vector2 nearSlotOffset = new Vector2(0f, 0f);
        [Tooltip("1 マスぶんの位置ズレ。向きで並ぶ角度、長さで間隔が決まる。全マス共通なので等間隔になる")]
        [SerializeField] private Vector2 cellStep = new Vector2(-150f, 75f);
        [Tooltip("3 マス目の明暗 (Multiply)。1 で通常色")]
        [SerializeField, Range(0f, 1f)] private float darkSlot3 = 1f;
        [Tooltip("4 マス目の明暗 (Multiply)。1 で通常色")]
        [SerializeField, Range(0f, 1f)] private float darkSlot4 = 1f;

        [Header("Cell Perspective (遠近感)")]
        [Tooltip("オフ = 全マス同じ大きさ (従来どおり)。オン = 手前ほど大きく、奥ほど小さくする。キャラの足元のマスは常に 1")]
        [SerializeField] private bool useCellPerspective;
        [Tooltip("一番手前 (後方 = distance -visibleBehind) のマスのスケール。1 未満にはならない")]
        [SerializeField, Min(1f)] private float cellScaleMax = 1f;
        [Tooltip("一番奥 (distance = visibleAhead) のマスのスケール。1 より大きくはならない")]
        [SerializeField, Range(0.01f, 1f)] private float cellScaleMin = 0.6f;
        [Tooltip("足元 (1) から Max / Min への変化のしかた。1 = 等速。大きいほど足元側で 1 を保ち、端で一気に変わる")]
        [SerializeField, Range(0.1f, 4f)] private float cellScaleFalloff = 1f;

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
            // 同じパスを共有する都合で PassThrough にしている (理由は PlayerInputAction)。
            _p1A = PlayerInputAction.Make("P1A", "<Keyboard>/a", GamepadButtons.A);
            _p1D = PlayerInputAction.Make("P1D", "<Keyboard>/d", GamepadButtons.B);
            _p2Left = PlayerInputAction.Make("P2Left", "<Keyboard>/leftArrow", GamepadButtons.A);
            _p2Right = PlayerInputAction.Make("P2Right", "<Keyboard>/rightArrow", GamepadButtons.B);

            PlayerInputAction.OnPress(_p1A, 1, () => HandlePress(1, CellType.A));
            PlayerInputAction.OnPress(_p1D, 1, () => HandlePress(1, CellType.B));
            PlayerInputAction.OnPress(_p2Left, 2, () => HandlePress(2, CellType.A));
            PlayerInputAction.OnPress(_p2Right, 2, () => HandlePress(2, CellType.B));

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
            RefreshAllCells();
        }

        // マスの見た目 (位置・表示・点滅) は毎フレームここで state から作り直す。
        // どのフェーズでも必ず通るので「一度消えたまま戻らない」状態が残らない。
        private void RefreshAllCells()
        {
            RefreshCells(_p1Cells, _p1, player1Track, player1Character, _p1CharacterRest);
            RefreshCells(_p2Cells, _p2, player2Track, player2Character, _p2CharacterRest);
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
                RefreshAllCells();   // 生成直後の 1 フレーム、prefab の位置のまま出ないように

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
            // 「前のトグルから何マス空いたか」。最初から置ける状態で始める。
            // int.MaxValue で始めると intro マスでの +1 が桁あふれして int.MinValue になり、
            // 以降 toggleMinSpacing を一生超えられずトグルマスが 1 つも出なくなる。
            var cellsSinceToggle = toggleMinSpacing;
            // 同じ種類 (けん / ぱ) が maxSameTypeRun 個までしか続かないよう、続き数を数えておく。
            // 上限に達したら次のマスは反対側で確定させる。intro の固定パターンもこの数え上げに含める。
            var runType = CellType.A;
            var runLength = 0;
            for (var i = 0; i < cellCount; i++)
            {
                // 頭 4 マスは ぱ・けん・けん・ぱ で固定 (index 0 = スタート、以降が開始演出のデモ)。トグルも置かない。
                var isIntro = i < IntroPattern.Length;
                var canBeToggle = !isIntro && cellsSinceToggle >= toggleMinSpacing;
                var isToggle = canBeToggle && rng.NextDouble() < toggleCellChance;

                CellType type;
                if (isIntro)
                    type = IntroPattern[i];
                else if (runLength >= maxSameTypeRun)
                    type = runType == CellType.A ? CellType.B : CellType.A;
                else
                    type = rng.NextDouble() < 0.5 ? CellType.A : CellType.B;

                _course.Add(new CellData
                {
                    type = type,
                    isToggle = isToggle,
                    spriteIndex = rng.Next(spriteVariants),
                });

                if (runLength == 0 || type != runType) { runType = type; runLength = 1; }
                else runLength++;

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

            // 色はマス単位ではなく「わっか画像」単位で送る。ぱ のマスは 2 枚使うので 2 つ進む。
            // コースは 1P/2P 共通なので、画像の通し番号も両者で同じ (開始色だけが違う)。
            var imageIndex = 0;
            for (var i = 0; i < _course.Count; i++)
            {
                if (player1Track != null) _p1Cells.Add(CreateCell(player1Track, i, _course[i], _p1ColorStart, imageIndex));
                if (player2Track != null) _p2Cells.Add(CreateCell(player2Track, i, _course[i], _p2ColorStart, imageIndex));
                imageIndex += _course[i].type == CellType.A ? 1 : 2;
            }
        }

        private static void ClearCells(List<HopscotchCell> list)
        {
            foreach (var c in list) if (c != null) Destroy(c.gameObject);
            list.Clear();
        }

        // imageIndex = このマスの 1 枚目のわっかが、コース全体で何枚目か。
        // ぱ のマスは 2 枚目に次の色を当てるので、1 マスの中でも色が変わる。
        private HopscotchCell CreateCell(RectTransform parent, int index, CellData data, int colorStart, int imageIndex)
        {
            var cell = Instantiate(cellPrefab, parent);
            cell.name = $"Cell_{index}";
            cell.Setup(data.type == CellType.A, data.isToggle, GetCellSprite(data.spriteIndex),
                GetCellColor(colorStart, imageIndex),
                GetCellColor(colorStart, imageIndex + 1));   // けん のマスでは使われない
            return cell;
        }

        private Sprite GetCellSprite(int spriteIndex)
        {
            if (cellSprites == null || cellSprites.Length == 0) return null;
            return cellSprites[Mathf.Abs(spriteIndex) % cellSprites.Length];
        }

        // colorStart から cellColors 順にループ。隣り合うわっかが同色になることはない。
        private Color GetCellColor(int colorStart, int imageIndex)
        {
            if (cellColors == null || cellColors.Length == 0) return Color.white;
            var len = cellColors.Length;
            return cellColors[(colorStart + imageIndex) % len];
        }

        // -------- Perspective rendering --------
        // キャラの足元 (pivot) を track のローカル座標で返す。
        // cell (anchor 0.5,0.5) と キャラ (anchor 0.5,0 / pivot 足元) は anchoredPosition の
        // 原点が違うので、そのまま混ぜるとトラック高さの半分ぶんズレる。ここで同じ座標系に揃える。
        private static Vector2 ToTrackLocal(RectTransform track, RectTransform character, Vector2 anchoredPos)
        {
            var parent = character != null ? character.parent as RectTransform : null;
            if (parent == null) return anchoredPos;

            // anchor 基準点 (親 pivot 原点から見た位置) + anchoredPosition = pivot のローカル位置
            var anchorCenter = (character.anchorMin + character.anchorMax) * 0.5f;
            var size = parent.rect.size;
            var local = anchoredPos + new Vector2(
                size.x * (anchorCenter.x - parent.pivot.x),
                size.y * (anchorCenter.y - parent.pivot.y));

            if (track == null || parent == track) return local;
            var world = track.InverseTransformPoint(parent.TransformPoint(local));
            return new Vector2(world.x, world.y);
        }

        // マスの大きさ。useCellPerspective がオフなら全マス 1 (従来どおり)。
        // オンなら キャラの足元 (distance 0) = 1 を基準に、
        // 前方 (奥) は visibleAhead で cellScaleMin まで縮み、後方 (手前) は visibleBehind で cellScaleMax まで大きくなる。
        private float GetCellScale(float distance)
        {
            if (!useCellPerspective || Mathf.Approximately(distance, 0f)) return 1f;

            float t, to;
            if (distance > 0f)
            {
                t = Mathf.Clamp01(distance / Mathf.Max(1, visibleAhead));
                to = Mathf.Min(cellScaleMin, 1f);
            }
            else
            {
                t = Mathf.Clamp01(-distance / Mathf.Max(1, visibleBehind));
                to = Mathf.Max(cellScaleMax, 1f);
            }

            if (!Mathf.Approximately(cellScaleFalloff, 1f)) t = Mathf.Pow(t, cellScaleFalloff);
            return Mathf.Lerp(1f, to, t);
        }

        // cells[i] は _course[i] に対応 (cells[0] = スタートマス)
        private void RefreshCells(List<HopscotchCell> cells, PlayerState state,
            RectTransform track, RectTransform character, Vector2 characterRest)
        {
            var currentPosition = state.visualPosition;
            // 足元 = distance 0 のマスが来る場所。nearSlotOffset が 0 ならキャラの真下を必ず通る。
            var foot = ToTrackLocal(track, character, characterRest);
            var near = foot + nearSlotOffset;
            var toggleOn = IsToggleOn();
            // ジャンプ中に点滅を止めるのは「飛び先のマス」だけ (飛んだ先が消えると着地が見えないため)。
            // トラック全体を出しっぱなしにすると、奥の点滅マスまで光ったままになって点滅が崩れる。
            var jumpTarget = state.isMoving ? state.position + 1 : -1;
            // 表示するマスの並びが変わったときだけ描画順を組み直す (毎フレームやると Canvas が再構築される)
            var orderDirty = false;

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                // distance 0 = 足元, 正 = 前方
                var distance = i - currentPosition;

                if (distance < -(visibleBehind + 0.5f) || distance > visibleAhead + 0.5f)
                {
                    if (cell.gameObject.activeSelf) { cell.gameObject.SetActive(false); orderDirty = true; }
                    continue;
                }
                if (!cell.gameObject.activeSelf) { cell.gameObject.SetActive(true); orderDirty = true; }

                // 等間隔に一直線。1 マスごとに cellStep ぶんずらすだけ (位置は遠近で詰めない)。
                // 後方 (distance が負) も同じ式でそのまま反対側に伸びる。
                // anchoredPosition ではなく localPosition。cell 側の anchor 設定に依存させない。
                var p = near + cellStep * distance;
                cell.Rect.localPosition = new Vector3(p.x, p.y, 0f);
                var s = GetCellScale(distance);
                cell.Rect.localScale = new Vector3(s, s, 1f);

                // 点滅マスの表示も毎フレームここで決め直す。cells[i] は _course[i] に対応。
                // 到達済み (足元と通過済み) と 飛び先 のマスだけ点滅を止めて出したまま。
                // 乗っている / 着地しようとしているマスが消えると違和感が出るため。
                var holdOn = i <= state.position || i == jumpTarget;
                cell.SetToggleState(!(i < _course.Count && _course[i].isToggle) || toggleOn || holdOn);

                // 明暗: 1,2マス目=通常 / 3マス目 / 4マス目 を個別に暗くできる (1 で通常色)
                var rank = Mathf.Clamp(Mathf.CeilToInt(distance), 1, 4);
                var darken = rank <= 2 ? 1f : (rank == 3 ? darkSlot3 : darkSlot4);
                cell.SetDarken(darken);
            }

            if (!orderDirty) return;

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
                    SetIntroObjectsActive(false);
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
                // Go が消えきったらここで隠す (次のラウンドの頭で ResetRoundView がまた出す)
                SetIntroObjectsActive(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void SetIntroObjectsActive(bool active)
        {
            if (player1IntroObject != null && player1IntroObject.activeSelf != active)
                player1IntroObject.SetActive(active);
            if (player2IntroObject != null && player2IntroObject.activeSelf != active)
                player2IntroObject.SetActive(active);
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
        // 勝者は足元のマス (けん / ぱ) のアニメーションを goalAnimPlayCount 回ループ再生する。
        // 敗者はその再生を待ってから失敗ジャンプのモーションに入る (Result 表示と同時)。
        private async UniTask PlayGoalEffectAsync(CancellationToken token)
        {
            _phase = Phase.Goal;
            await PlayGoalAnimLoopAsync(_winner, token);

            // 再生し終わったタイミング = Result 表示のタイミング。敗者はここから失敗モーション。
            PlayLoserMissAsync(_winner == 1 ? 2 : 1, token).Forget();
        }

        // 足元のマスのアニメを 1 周ずつ頭から掛け直して goalAnimPlayCount 回ぶん再生する。
        private async UniTask PlayGoalAnimLoopAsync(int player, CancellationToken token)
        {
            var state = player == 1 ? _p1 : _p2;
            var anim = GetCellAnim(player, state.position);

            // アニメが使えない設定 (未設定 / 尺 0) のときは従来どおり goalHoldDuration ぶん止める
            if (anim == null || anim.TotalDuration <= 0f)
            {
                if (goalHoldDuration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(goalHoldDuration), cancellationToken: token);
                return;
            }

            for (var i = 0; i < goalAnimPlayCount; i++)
            {
                ShowCellAnim(player, state.position, play: true);
                await UniTask.Delay(TimeSpan.FromSeconds(anim.TotalDuration), cancellationToken: token);
            }
            ShowCellAnim(player, state.position, play: false);   // 最終フレームで待機
        }

        // 敗者: その場で失敗ジャンプを 1 回。ミス演出が走っている最中なら終わるのを待ってから重ねる。
        private async UniTaskVoid PlayLoserMissAsync(int player, CancellationToken token)
        {
            var state = player == 1 ? _p1 : _p2;
            try
            {
                while (state.isMoving || state.isStopped)
                    await UniTask.Yield(PlayerLoopTiming.Update, token);

                state.isStopped = true;   // 待機のたてゆれを止める
                try
                {
                    await PlayMissJumpAsync(player, missLockDuration, token);
                }
                finally
                {
                    state.isStopped = false;
                }
            }
            catch (OperationCanceledException)
            {
            }
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
            WinnerHopLoopAsync(_winner, token).Forget();
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // Result 表示中、勝者は winnerHopInterval ごとにその場で跳び続ける。
        // 再戦 / 退出でフェーズが変わるか、ラウンドが破棄されたら止まる。
        private async UniTaskVoid WinnerHopLoopAsync(int player, CancellationToken token)
        {
            var state = player == 1 ? _p1 : _p2;
            try
            {
                while (_phase == Phase.Winner || _phase == Phase.WaitForExit)
                {
                    await HopInPlaceAsync(player, state, winnerHopDuration, token);
                    var wait = winnerHopInterval - winnerHopDuration;
                    if (wait > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
                    else
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        // その場ジャンプ。マスは進まないので足元のアニメを頭から掛け直して跳ぶだけ。
        private async UniTask HopInPlaceAsync(int player, PlayerState state, float duration, CancellationToken token)
        {
            ShowCellAnim(player, state.position, play: true);

            var character = player == 1 ? player1Character : player2Character;
            var restPos = player == 1 ? _p1CharacterRest : _p2CharacterRest;
            if (character == null || duration <= 0f) return;

            state.isMoving = true;   // 待機のたてゆれを止める
            try
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    character.anchoredPosition = restPos + new Vector2(0f, jumpHeight * Mathf.Sin(t * Mathf.PI));
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                character.anchoredPosition = restPos;
                state.isMoving = false;
            }
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
            // 最後に流している BGM を止める
            Audio.StopBgm();
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // -------- Toggle visuals --------
        // 点滅マスが今「飛べる」タイミングかどうか。表示の反映は RefreshCells が毎フレーム行う。
        // プレイ中以外は常に true。ゴール後やリザルトで わっかが消えたまま残らないようにする。
        private bool IsToggleOn() =>
            _phase != Phase.Playing || ((int)(_playElapsed / toggleInterval)) % 2 == 0;

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
                    // マスの描画は Update (RefreshAllCells) 側。ここは位置を進めるだけ。
                    state.visualPosition = Mathf.Lerp(startCurrent, endCurrent, EaseOutCubic(t));

                    // Character jump — sine wave over the same duration
                    if (character != null)
                    {
                        var jumpY = jumpHeight * Mathf.Sin(t * Mathf.PI);
                        character.anchoredPosition = restPos + new Vector2(0f, jumpY);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                state.visualPosition = endCurrent;
                if (character != null) character.anchoredPosition = restPos;

                // 入力のロックはここまで (= duration ぴったり)。着地アニメの残りは待たない。
                // 待つと けん / ぱ でロック長が変わり、失敗時 (missLockDuration) との差も大きくなって
                // 飛ぶリズムが崩れるため。アニメは裏で最後まで流れ、次のジャンプで頭から掛け直る。
            }
            catch (OperationCanceledException)
            {
                if (character != null) character.anchoredPosition = restPos;
            }

            state.position = targetIndex;
            state.visualPosition = targetIndex;
            state.isMoving = false;
        }

        // けん / ぱ の SpriteAnimation を切り替える。使う方だけ表示し、もう片方は隠す。
        // play = true で頭から再生 (跳ぶとき)、false なら最終フレームで静止 (着地して待機している状態)。
        // 範囲外 (コース未生成など) は ぱ 扱い。
        private void ShowCellAnim(int player, int courseIndex, bool play)
        {
            var target = GetCellAnim(player, courseIndex);
            var isPa = IsPaCell(courseIndex);
            var other = player == 1
                ? (isPa ? player1KenAnim : player1PaAnim)
                : (isPa ? player2KenAnim : player2PaAnim);

            // もう片方は必ず止めてから隠す。けん / ぱ が同じ GameObject (= 同じ Image) に
            // 載っている構成だと、止めずに切り替えると両方が同じ Image に書き込んで絵が競合する。
            if (other != null)
            {
                other.Stop();
                var sharesObject = target != null && other.gameObject == target.gameObject;
                if (!sharesObject && other.gameObject.activeSelf) other.gameObject.SetActive(false);
            }
            if (target == null) return;
            if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);

            if (play) target.Play();
            else if (target.Length > 0) target.SetFrame(target.Length - 1);
        }

        // 飛び先が ぱ か (けん か)。範囲外 (コース未生成など) は ぱ 扱い。
        private bool IsPaCell(int courseIndex) =>
            courseIndex < 0 || courseIndex >= _course.Count || _course[courseIndex].type == CellType.B;

        private SpriteAnimation GetCellAnim(int player, int courseIndex)
        {
            var isPa = IsPaCell(courseIndex);
            return player == 1
                ? (isPa ? player1PaAnim : player1KenAnim)
                : (isPa ? player2PaAnim : player2KenAnim);
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
            if (winnerLabel != null) winnerLabel.Clear();
            if (resultGroup != null)
            {
                resultGroup.alpha = 0f;
                resultGroup.interactable = false;
                resultGroup.blocksRaycasts = false;
            }

            // 1P/2P の開始時オブジェクトは毎ラウンド出しなおす (Go の演出終わりで隠れる)
            SetIntroObjectsActive(true);

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
            _p1.position = 0; _p1.visualPosition = 0f; _p1.isMoving = false; _p1.isStopped = false; _p1.idleElapsed = 0f;
            _p2.position = 0; _p2.visualPosition = 0f; _p2.isMoving = false; _p2.isStopped = false; _p2.idleElapsed = 0f;
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