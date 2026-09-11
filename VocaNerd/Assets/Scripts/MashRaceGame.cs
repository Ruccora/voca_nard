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
            Opening,
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
        [Tooltip("残り時間 (num-00〜num-09 / num-colon の画像で表示)")]
        [SerializeField] private SpriteNumber timerNumber;
        [Tooltip("リザルト (勝敗表示 + Play Again) のまとまり。プレイ中は alpha 0")]
        [SerializeField] private CanvasGroup resultGroup;
        [Tooltip("勝敗メッセージのテキスト")]
        [SerializeField] private TMP_Text winnerText;
        [Tooltip("もう一度遊ぶボタン")]
        [SerializeField] private Button playAgainButton;

        [Header("Background (shared full-screen)")]
        [Tooltip("星: 常時 Z 回転し、結果演出の後半でゆっくり縮小する")]
        [SerializeField] private RectTransform starsRect;
        [Tooltip("星の FadeOut に使う CanvasGroup。未設定なら starsRect の Image の alpha を直接いじる")]
        [SerializeField] private CanvasGroup starsGroup;
        [Tooltip("地面: 下起点アンカー。結果演出で 上昇 → 縮小 → 下降 して消える")]
        [SerializeField] private RectTransform groundRect;
        [Tooltip("地球: 結果演出の後半に下から競り上がる")]
        [SerializeField] private RectTransform earthRect;
        [Tooltip("画面を白くする全画面オーバーレイ (結果演出の最後に FadeIn)")]
        [SerializeField] private CanvasGroup whiteFade;

        [Header("Players")]
        [Tooltip("P1 のキャラ。位置 / スケール / 回転は prefab の値を home として記録する")]
        [SerializeField] private RectTransform player1Character;
        [Tooltip("P2 のキャラ。位置 / スケール / 回転は prefab の値を home として記録する")]
        [SerializeField] private RectTransform player2Character;
        [Tooltip("P1 キャラの連番アニメ (0 = A 押下ポーズ / 4 = B 押下ポーズ)")]
        [SerializeField] private SpriteAnimation player1Anim;
        [Tooltip("P2 キャラの連番アニメ (0 = A 押下ポーズ / 4 = B 押下ポーズ)")]
        [SerializeField] private SpriteAnimation player2Anim;

        [Header("Opening View (開始演出)")]
        [Tooltip("開始演出中に出しておく P1 ラベル (Ready の開始時に消える)")]
        [SerializeField] private CanvasGroup player1LabelGroup;
        [Tooltip("開始演出中に出しておく P2 ラベル (Ready の開始時に消える)")]
        [SerializeField] private CanvasGroup player2LabelGroup;
        [Tooltip("P1 の A 画像 (演出中はキャラの 0 フレームと連動。プレイ中は次に押すキーとして常時表示)")]
        [SerializeField] private CanvasGroup player1KeyAGroup;
        [Tooltip("P1 の B 画像 (演出中はキャラの 4 フレームと連動。プレイ中は次に押すキーとして常時表示)")]
        [SerializeField] private CanvasGroup player1KeyBGroup;
        [Tooltip("P2 の A 画像 (演出中はキャラの 0 フレームと連動。プレイ中は次に押すキーとして常時表示)")]
        [SerializeField] private CanvasGroup player2KeyAGroup;
        [Tooltip("P2 の B 画像 (演出中はキャラの 4 フレームと連動。プレイ中は次に押すキーとして常時表示)")]
        [SerializeField] private CanvasGroup player2KeyBGroup;
        [Tooltip("Ready 画像の RectTransform。prefab で設定したスケールが登場時スケールになる")]
        [SerializeField] private RectTransform readyRect;
        [Tooltip("Ready 画像の CanvasGroup (FadeIn に使う)")]
        [SerializeField] private CanvasGroup readyGroup;
        [Tooltip("Go 画像の RectTransform。prefab で設定したスケールが登場時スケールになる")]
        [SerializeField] private RectTransform goRect;
        [Tooltip("Go 画像の CanvasGroup (FadeOut に使う)")]
        [SerializeField] private CanvasGroup goGroup;

        [Header("Opening Timing")]
        [Tooltip("キャラを 0 → 4 で往復させる回数")]
        [SerializeField] private int openingCycles = 4;
        [Tooltip("0 / 4 の 1 フレームあたりの表示秒数")]
        [SerializeField] private float openingStepDuration = 0.18f;
        [Tooltip("キャラ停止 → Ready 登場までの待機秒数")]
        [SerializeField] private float openingLabelHideWait = 0.15f;

        [Header("Opening / Ready (ScaleDown + FadeIn)")]
        [Tooltip("Ready の到達スケール (絶対値)。登場時スケールは prefab の Ready のスケールをそのまま使う")]
        [SerializeField] private float readyEndScale = 1f;
        [Tooltip("ScaleDown の尺 (秒)")]
        [SerializeField] private float readyScaleDuration = 0.35f;
        [Tooltip("FadeIn の尺 (秒)。ScaleDown と同時に開始し、尺だけ別に指定できる")]
        [SerializeField] private float readyFadeInDuration = 0.2f;
        [Tooltip("Ready を出したままの保持秒数")]
        [SerializeField] private float readyHoldDuration = 0.5f;

        [Header("Opening / Go (ScaleUp + FadeOut)")]
        [Tooltip("Go の到達スケール (絶対値)。登場時スケールは prefab の Go のスケールをそのまま使う")]
        [SerializeField] private float goEndScale = 1.6f;
        [Tooltip("ScaleUp の尺 (秒)。FadeOut はこの尺の途中から始まり、残り時間で消える")]
        [SerializeField] private float goScaleDuration = 0.6f;
        [Tooltip("ScaleUp の何割まで進んだら FadeOut を始めるか (0.5 = 半分)")]
        [SerializeField, Range(0f, 1f)] private float goFadeOutStartRatio = 0.5f;

        [Header("Timing")]
        [Tooltip("1 ラウンドの連打時間 (秒)")]
        [SerializeField] private float playDuration = 10f;
        [Tooltip("Play Again からの再戦時、開始演出に入る前に挟む待機秒数 (初回入場時は黒フェード明けを待つので不要)")]
        [SerializeField] private float replayDelay = 1f;

        [Header("Miss (誤入力)")]
        [Tooltip("誤入力時に自キャラを傾ける Z 角度")]
        [SerializeField] private float missTiltAngle = 10f;
        [Tooltip("傾けている秒数。この間はそのプレイヤーの入力を受け付けない")]
        [SerializeField] private float missTiltDuration = 0.15f;
        [Tooltip("押すべきキー画像の明滅間隔 (秒)")]
        [SerializeField] private float missBlinkInterval = 0.05f;

        [Header("Character Fly")]
        [Tooltip("勝者が上がって残る位置 (anchoredPosition.y)")]
        [SerializeField] private float winnerRiseY = -150f;
        [Tooltip("勝者が上がりきるまでの尺 (秒)。敗者もこの尺で同じ位置まで上がる")]
        [SerializeField] private float winnerRiseDuration = 0.6f;
        [Tooltip("敗者の落下先 (anchoredPosition.y)。画面外まで落とす")]
        [SerializeField] private float loserFallY = -1400f;
        [Tooltip("敗者が落ちきるまでの尺 (秒)")]
        [SerializeField] private float loserFallDuration = 0.7f;
        [Tooltip("飛んでいるキャラの到達スケール (絶対値)。星の縮小と同時にここまで縮む")]
        [SerializeField] private float charEndScale = 0.7f;

        [Header("Power (連打 = 演出予算)")]
        // 連打 = パワー。maxPower * (白到達尺 / whiteReachAlternations) 秒ぶん演出を進めて、
        // 使い切ったら演出停止(その画面状態で固定)。whiteReachAlternations 連打で白に到達 (80 → 15秒)。
        // それ未満は途中停止(白に届かない)。敗者は自分のパワーぶんだけ飛んでから落ちる。
        [Tooltip("この連打数で結果演出が白到達まで進む。少ないほど途中で演出が止まる")]
        [SerializeField] private int whiteReachAlternations = 80;

        [Header("Winner Effect (勝利演出)")]
        [Tooltip("勝者キャラの到達スケール (絶対値)。開始は演出が始まった時点のスケール")]
        [SerializeField] private float winnerEffectEndScale = 1.2f;
        [Tooltip("到達スケールまでの尺 (秒)。往復移動とは別尺で、一度だけ実行される")]
        [SerializeField] private float winnerEffectScaleDuration = 0.5f;
        [Tooltip("真横の往復幅 (px)。開始位置を中心に ±この距離で動く")]
        [SerializeField] private float winnerEffectMoveDistance = 200f;
        [Tooltip("真横の往復 1 周の尺 (秒)。ラウンド中ずっと往復し続ける")]
        [SerializeField] private float winnerEffectMoveDuration = 1.2f;
        [Tooltip("敗北演出 (敗者の落下) の開始から勝利演出を始めるまでの最低秒数")]
        [SerializeField] private float winnerEffectMinDelayAfterLoserFall = 1f;
        [Tooltip("勝利演出の開始から Result (勝敗表示 + 入力受付) を出すまでの秒数")]
        [SerializeField] private float resultDelayAfterWinnerEffect = 1.5f;

        [Header("Result Sequence (白到達までの尺)")]
        // 白到達までの時間 = ここの各尺の合計 - 地球の先行時間 (ToWhiteSeconds)。
        // 既定値では 1.0+3.0+1.8+(3.2-1.0)+3.2+1.6+1.6+1.2 = 15.6 秒。
        // 星の最終 FadeOut (starsFadeOutDuration) は地球の演出と並行なので合計には入らない。
        [Tooltip("星の Z 回転速度 (deg/s)。ゲーム進行に関係なく常時回る")]
        [SerializeField] private float starsSpinSpeed = 40f;
        [Tooltip("地面が最初に少し上がる量 (px)")]
        [SerializeField] private float groundRiseHeight = 120f;
        [Tooltip("地面が上がりきるまでの尺 (秒)")]
        [SerializeField] private float groundRiseDuration = 1.0f;
        [Tooltip("地面が縮みきるまでの尺 (秒)")]
        [SerializeField] private float groundShrinkDuration = 3.0f;
        [Tooltip("地面の到達スケール (絶対値)。開始スケールは prefab の Ground のスケール")]
        [SerializeField] private float groundEndScale = 0.2f;
        [Tooltip("地面が下に抜けて消えるまでの尺 (秒)")]
        [SerializeField] private float groundDescendDuration = 1.8f;
        [Tooltip("地面が下に抜ける距離の換算 (px / 連打1回)")]
        [SerializeField] private float groundMoveUnitPerPower = 4f;
        [Tooltip("地面が下に抜ける距離の下限 (px)")]
        [SerializeField] private float groundMoveMin = 400f;
        [Tooltip("地面が下に抜ける距離の上限 (px)")]
        [SerializeField] private float groundMoveMax = 1600f;
        [Tooltip("星が縮みきるまでの尺 (秒)。飛んでいるキャラの縮小も同じ尺")]
        [SerializeField] private float starsShrinkDuration = 3.2f;
        [Tooltip("星の到達スケール (絶対値)。開始スケールは prefab の Stars のスケール")]
        [SerializeField] private float starsEndScale = 0.72f;
        [Tooltip("星の縮小が終わった後、最後に星が縮む先のスケール (絶対値)。同時に alpha 0 まで FadeOut する")]
        [SerializeField] private float starsFadeOutEndScale = 0.2f;
        [Tooltip("星の最終 ScaleDown + FadeOut の尺 (秒)。地球の演出とは独立に指定できる")]
        [SerializeField] private float starsFadeOutDuration = 3.8f;
        [Tooltip("星の縮小が終わる何秒前に地球が競り上がり始めるか (星の縮小尺でクランプ)")]
        [SerializeField] private float earthRiseLeadBeforeStarsEnd = 1f;
        [Tooltip("地球の初期位置 (anchoredPosition.y)。画面下の外")]
        [SerializeField] private float earthStartY = -1600f;
        [Tooltip("地球が競り上がる先 (anchoredPosition.y)")]
        [SerializeField] private float earthRiseY = -200f;
        [Tooltip("地球が競り上がりきるまでの尺 (秒)")]
        [SerializeField] private float earthRiseDuration = 3.2f;
        [Tooltip("地球が上がりきってから縮むまでの尺 (秒)。0 で縮まない")]
        [SerializeField] private float earthShrinkDuration = 1.6f;
        [Tooltip("地球の到達スケール (絶対値)。開始スケールは prefab の Earth のスケール")]
        [SerializeField] private float earthEndScale = 0.6f;
        [Tooltip("地球が縮みきってから白 FadeIn を始めるまでの待機秒数")]
        [SerializeField] private float earthHoldBeforeWhite = 1.6f;
        [Tooltip("画面が白くなりきるまでの尺 (秒)")]
        [SerializeField] private float whiteFadeDuration = 1.2f;
        [Tooltip("白 FadeIn の後、勝利演出を始めるまでの待機秒数。パワーが余って白に到達した場合のみ効く")]
        [SerializeField] private float winnerEffectDelayAfterWhite = 1f;

        // 連打中のキャラ表示フレーム。A/← = 1番目、D/→ = 5番目 で交互に切り替える。
        private const int MashFrameLeft = 0;
        private const int MashFrameRight = 4;

        private Phase _phase;

        public override bool CanAcceptBack => _phase == Phase.Winner || _phase == Phase.WaitForExit;
        private readonly PlayerState _p1 = new PlayerState();
        private readonly PlayerState _p2 = new PlayerState();
        private InputAction _p1Left, _p1Right, _p2Left, _p2Right;
        private ResultInput _resultInput; // リザルトの A=再戦 / B=退出 (1P のみ)
        private CancellationTokenSource _roundCts;
        private CancellationTokenSource _effectCts; // 背景シーケンス専用。破棄=その状態で演出停止 (星の回転は止めない)
        private UniTaskCompletionSource _exitSignal;

        // 星と一緒に縮む「飛んでいるキャラ」。敗者は落下開始で外れる。
        private RectTransform _shrinkCharA;
        private RectTransform _shrinkCharB;

        // 勝利演出 (スケール変更 + 真横の往復) の対象。ラウンド中は止めない。
        private RectTransform _winnerChar;
        private CancellationTokenSource _winnerCts;
        private UniTaskCompletionSource _winnerEffectStarted; // 演出が実際に始まった合図 (Result の基準)
        private bool _p1Won;            // 引き分けなしでランダム決着させた結果もここに入る
        private float _loserFallTime;   // 敗北演出 (落下) の開始時刻。勝利演出はここから最低 1 秒空ける
        private bool _loserFell;

        // 敗者の滞空の打ち切り合図。勝利演出が始まったらパワーが残っていても落下させる。
        private UniTaskCompletionSource _loserFlyCut;

        // prefab で設定された初期 scale/位置/回転 (リセットで復元する)
        private Vector3 _starsHomeScale = Vector3.one;
        private Vector3 _groundHomeScale = Vector3.one;
        private Vector2 _groundHomePos;
        private Vector3 _earthHomeScale = Vector3.one;
        private Graphic _starsGraphic; // starsGroup 未設定時の FadeOut 対象 (Stars の Image)
        private Vector3 _p1HomeScale = Vector3.one;
        private Vector2 _p1HomePos;
        private Quaternion _p1HomeRot = Quaternion.identity;
        private Vector3 _p2HomeScale = Vector3.one;
        private Vector2 _p2HomePos;
        private Quaternion _p2HomeRot = Quaternion.identity;
        private float _readyHomeScale = 1f;
        private float _goHomeScale = 1f;
        private bool _isSetup;

        public override UniTask SetupAsync(CancellationToken token)
        {
            if (_isSetup) return UniTask.CompletedTask;
            _isSetup = true;

            // 画面の A / B 表示に合わせて、パッドも A (buttonSouth) / B (buttonEast) で交互連打。
            // 1P/2P で同じボタンを張っておき、どちらのパッドかは PlayerDevices で振り分ける。
            _p1Left = MakeAction("P1Left", "<Keyboard>/a", "<Gamepad>/buttonSouth");
            _p1Right = MakeAction("P1Right", "<Keyboard>/d", "<Gamepad>/buttonEast");
            _p2Left = MakeAction("P2Left", "<Keyboard>/leftArrow", "<Gamepad>/buttonSouth");
            _p2Right = MakeAction("P2Right", "<Keyboard>/rightArrow", "<Gamepad>/buttonEast");

            _p1Left.performed += ctx => { if (PlayerDevices.IsForPlayer(ctx, 1)) HandlePress(1, -1); };
            _p1Right.performed += ctx => { if (PlayerDevices.IsForPlayer(ctx, 1)) HandlePress(1, +1); };
            _p2Left.performed += ctx => { if (PlayerDevices.IsForPlayer(ctx, 2)) HandlePress(2, -1); };
            _p2Right.performed += ctx => { if (PlayerDevices.IsForPlayer(ctx, 2)) HandlePress(2, +1); };

            _resultInput = new ResultInput(OnResultRetry, OnResultExit);

            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgain);

            CaptureHome();
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

        // 星(回転する背景)はゲームの進行/演出停止に関係なく常に等速で回し続ける。
        private void Update()
        {
            if (starsRect != null)
                starsRect.Rotate(0f, 0f, -starsSpinSpeed * Time.deltaTime);
        }

        private void OnDestroy()
        {
            CancelRound();
            _p1Left?.Dispose();
            _p1Right?.Dispose();
            _p2Left?.Dispose();
            _p2Right?.Dispose();
            _resultInput?.Dispose();
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgain);
        }

        private static InputAction MakeAction(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var binding in bindings) a.AddBinding(binding);
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
            _resultInput?.Disable();
        }

        private void OnPlayAgain()
        {
            if (IsAnimating) return;
            _resultInput?.Disable();
            StartRound(replay: true);
        }

        // リザルト: 1P の A で再戦
        private void OnResultRetry()
        {
            if (IsAnimating) return;
            if (_phase != Phase.WaitForExit) return;
            _resultInput.Disable();
            Audio.PlaySE(SeKey.Decide);
            StartRound(replay: true);
        }

        // リザルト: 1P の B で抜ける (通常の退出シーケンスへ流す)
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
            _winnerCts?.Cancel();
            _winnerCts?.Dispose();
            _winnerCts = null;
            _effectCts?.Cancel();
            _effectCts?.Dispose();
            _effectCts = null;
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

                await PlayOpeningAsync(replay, token);

                // Go が FadeOut しきってからゲーム開始 (操作もタイマー表示もここから)
                await PlayGoAsync(token);
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
        // 黒フェード(遷移の明転)を待つ → キャラを 0/4 で openingCycles 回往復 (A/B ラベルを連動) →
        // 完全停止 → Ready 開始と同時に P1/P2 と A/B を消す → Ready 登場。
        // 「Ready から先はゲーム前」と分かるよう、演出用のラベルはここで一度すべて引っ込める。
        private async UniTask PlayOpeningAsync(bool replay, CancellationToken token)
        {
            _phase = Phase.Opening;

            // 黒フェードで入ってくる (明転自体は ScreenController が持つ)。見えてから演出を始める。
            if (ScreenController.Instance != null)
                await ScreenController.Instance.WaitForTransitionFadeAsync(token);

            // 再戦時は明転待ちが即座に返る (既に見えている) ので、代わりに一拍置いてから始める
            if (replay && replayDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(replayDelay), cancellationToken: token);

            await PlayOpeningCharacterAsync(token);

            if (openingLabelHideWait > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(openingLabelHideWait), cancellationToken: token);

            HideOpeningLabels();
            await PlayReadyAsync(token);
        }

        // キャラを 0 (=A) と 4 (=B) で往復。P1/P2 ラベルはここで出して Ready 開始まで出したまま。
        private async UniTask PlayOpeningCharacterAsync(CancellationToken token)
        {
            SetGroupAlpha(player1LabelGroup, 1f);
            SetGroupAlpha(player2LabelGroup, 1f);

            var cycles = Mathf.Max(0, openingCycles);
            for (var i = 0; i < cycles; i++)
            {
                await ShowOpeningFrameAsync(MashFrameLeft, token);
                await ShowOpeningFrameAsync(MashFrameRight, token);
            }

            // 完全停止: 0 フレームで固定 (SetFrame はアニメーションを止める)
            ResetAnimToFirstFrame();
        }

        // 指定フレームを表示し、A/B ラベルをそれに合わせて切り替えて openingStepDuration 待つ。
        private async UniTask ShowOpeningFrameAsync(int frame, CancellationToken token)
        {
            var isA = frame == MashFrameLeft;
            if (player1Anim != null) player1Anim.SetFrame(frame);
            if (player2Anim != null) player2Anim.SetFrame(frame);
            SetGroupAlpha(player1KeyAGroup, isA ? 1f : 0f);
            SetGroupAlpha(player2KeyAGroup, isA ? 1f : 0f);
            SetGroupAlpha(player1KeyBGroup, isA ? 0f : 1f);
            SetGroupAlpha(player2KeyBGroup, isA ? 0f : 1f);

            if (openingStepDuration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(openingStepDuration), cancellationToken: token);
            else
                await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // Ready: ScaleDown と FadeIn を同時に開始する (それぞれ別の尺)。
        // 開始スケール = prefab で設定した Ready のスケール、到達スケール = readyEndScale (絶対値)。
        private async UniTask PlayReadyAsync(CancellationToken token)
        {
            SetGroupAlpha(goGroup, 0f);
            SetScale(readyRect, _readyHomeScale);
            SetGroupAlpha(readyGroup, 0f);

            await UniTask.WhenAll(
                LerpScaleAsync(readyRect, _readyHomeScale, readyEndScale, readyScaleDuration, EaseOutCubic, token),
                FadeGroupAsync(readyGroup, 0f, 1f, readyFadeInDuration, token));

            if (readyHoldDuration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(readyHoldDuration), cancellationToken: token);
        }

        // Go: ScaleUp しつつ、goFadeOutStartRatio まで進んだところから残り時間で FadeOut。
        // 開始スケール = prefab で設定した Go のスケール、到達スケール = goEndScale (絶対値)。
        // FadeOut が終わった時点でゲーム開始 (呼び出し側が await する)。
        private async UniTask PlayGoAsync(CancellationToken token)
        {
            SetGroupAlpha(readyGroup, 0f);
            SetScale(goRect, _goHomeScale);
            SetGroupAlpha(goGroup, 1f);

            var duration = goScaleDuration;
            if (duration <= 0f)
            {
                SetScale(goRect, goEndScale);
                SetGroupAlpha(goGroup, 0f);
                return;
            }

            var fadeStart = duration * goFadeOutStartRatio;
            var fadeDuration = duration - fadeStart;
            var from = _goHomeScale;
            var to = goEndScale;

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

        // 開始演出のラベル (P1/P2 と A/B) をまとめて非表示にする
        private void HideOpeningLabels()
        {
            HidePlayerLabels();
            HideKeyLabels();
        }

        // P1/P2 ラベル。Ready の開始時に消える。
        private void HidePlayerLabels()
        {
            SetGroupAlpha(player1LabelGroup, 0f);
            SetGroupAlpha(player2LabelGroup, 0f);
        }

        // A/B ラベル。Ready 開始時に消え、ゲーム開始後は「次に押すキー」として出し直す。
        private void HideKeyLabels()
        {
            SetGroupAlpha(player1KeyAGroup, 0f);
            SetGroupAlpha(player1KeyBGroup, 0f);
            SetGroupAlpha(player2KeyAGroup, 0f);
            SetGroupAlpha(player2KeyBGroup, 0f);
        }

        // -------- Stage 3: プレイ中 (連打) --------
        private async UniTask PlayGameAsync(CancellationToken token)
        {
            _phase = Phase.Playing;

            // ゲーム開始 (= Go の FadeOut 後) でタイマーと「次に押すキー」を出す
            SetTimer(playDuration);
            UpdateKeyHint(1);
            UpdateKeyHint(2);

            var elapsed = 0f;
            while (elapsed < playDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var remaining = Mathf.Max(0f, playDuration - elapsed);
                SetTimer(remaining);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetTimer(0f);
            HideKeyLabels(); // プレイ終了 = キー表示の役目も終わり

            var best = Mathf.Max(_p1.alternations, _p2.alternations);
            if (SaveData.TrySetHighScore(SaveData.GameId.MashRace, best))
                Debug.Log($"[MashRace] New high score: {best}");
        }

        // 残り時間を「秒:1/100秒」(09:58 = 9.58 秒) で画像表示する (区切りは num-colon)。
        // 端数は切り上げ (0.001 秒残っていれば 00:01 と表示し、0 になって初めて 00:00)。
        private void SetTimer(float seconds)
        {
            if (timerNumber == null) return;
            var hundredths = Mathf.CeilToInt(Mathf.Max(0f, seconds) * 100f);
            timerNumber.SetText($"{hundredths / 100:00}:{hundredths % 100:00}");
        }

        // -------- Stage 4: 結果演出 (共有背景シーケンス) --------
        private async UniTask PlayResultEffectAsync(CancellationToken token)
        {
            _phase = Phase.Result;

            // 飛び始め: ここから 1番目のフレームから連続アニメーションさせ続ける
            StartFlyAnimation();

            // 飛び始めたらタイマーの役目は終わりなので消す
            if (timerNumber != null) timerNumber.Clear();

            // 勝利演出の開始で敗者の滞空を打ち切るための合図 (ラウンドごとに作り直す)
            _loserFlyCut = new UniTaskCompletionSource();
            _winnerEffectStarted = new UniTaskCompletionSource();

            var maxPower = Mathf.Max(_p1.alternations, _p2.alternations);
            var minPower = Mathf.Min(_p1.alternations, _p2.alternations);

            // 引き分けは作らない。同数ならランダムでどちらかを勝者にする。
            _p1Won = _p1.alternations != _p2.alternations
                ? _p1.alternations > _p2.alternations
                : UnityEngine.Random.value < 0.5f;

            // 1連打 = 何秒ぶんの演出か。勝者(=背景シーケンス)も敗者の飛行時間も同じ換算を使う。
            var secondsPerAlternation = ToWhiteSeconds() / Mathf.Max(1, whiteReachAlternations);

            // 勝敗で「残る/落ちる」を決める
            var winnerChar = _p1Won ? player1Character : player2Character;
            var loserChar = _p1Won ? player2Character : player1Character;
            _shrinkCharA = winnerChar;
            _shrinkCharB = loserChar; // 敗者も飛んでいる間は星と一緒に縮む
            _winnerChar = winnerChar;
            RiseWinnerAsync(winnerChar, token).Forget();
            // 敗者は自分のパワーぶんだけ一緒に飛んで、尽きたら先に落ちる。
            // 演出停止(_effectCts)の影響を受けない別 UniTask。round トークンで走らせる。
            FlyThenFallLoserAsync(loserChar, minPower * secondsPerAlternation, token).Forget();

            // 連打 = パワー = 演出予算。背景シーケンス(白まで15秒)を effectToken で走らせ、
            // maxPower ぶんの時間が経ったら _effectCts を破棄して「その画面状態で停止」する。
            _effectCts?.Dispose();
            _effectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var effectToken = _effectCts.Token;

            // 予算 = maxPower * (白到達尺 / whiteReachAlternations)。
            // 80連打 → 白到達尺ちょうど(=白に到達)。40連打 → その半分で途中停止。
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

            // 予算切れで白まで届かなかった場合もここから回り始める (勝者はラウンド中ずっと回る)
            StartWinnerEffect(token);
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

        // 敗者: 溜まったパワーぶんは勝者と一緒に飛び、尽きたら先に落ちる。演出停止では止めない。
        private async UniTaskVoid FlyThenFallLoserAsync(RectTransform character, float flySeconds, CancellationToken token)
        {
            if (character == null)
            {
                // 敗者キャラ未設定。勝利演出を待たせ続けないよう「落ちた」ことにする。
                _loserFallTime = Time.time;
                _loserFell = true;
                return;
            }
            try
            {
                if (flySeconds > 0f)
                {
                    // 勝者と同じ位置まで上がる。パワーが上昇尺より短ければその範囲で上がりきる。
                    var rise = Mathf.Min(winnerRiseDuration, flySeconds);
                    var from = character.anchoredPosition.y;
                    await LerpAnchoredYAsync(character, from, winnerRiseY, rise, EaseOutCubic, token);

                    // パワーが尽きるまで滞空。ただし勝利演出が始まったらそこで打ち切って落下へ。
                    var remain = flySeconds - rise;
                    if (remain > 0f)
                    {
                        var hold = UniTask.Delay(TimeSpan.FromSeconds(remain), cancellationToken: token);
                        var cut = _loserFlyCut;
                        if (cut != null) await UniTask.WhenAny(hold, cut.Task);
                        else await hold;
                    }
                }

                // パワー切れ (or 勝利演出の予約): 星と一緒の縮小対象から外して落下させる
                if (_shrinkCharA == character) _shrinkCharA = null;
                if (_shrinkCharB == character) _shrinkCharB = null;

                // 勝利演出はこの敗北演出の開始から最低 winnerEffectMinDelayAfterLoserFall 秒空ける
                _loserFallTime = Time.time;
                _loserFell = true;

                var y = character.anchoredPosition.y;
                await LerpAnchoredYAsync(character, y, loserFallY, loserFallDuration, EaseInQuad, token);
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

            // 2) 地面: prefab のスケールから groundEndScale (絶対値) まで縮小
            var gFrom = groundRect != null ? groundRect.localScale.x : 1f;
            await LerpScaleAsync(groundRect, gFrom, groundEndScale, groundShrinkDuration, EaseInOutSine, token);

            // 3) 地面: プレイヤー移動量に合わせて下に移動して消える
            var moveAmount = Mathf.Clamp(maxPower * groundMoveUnitPerPower, groundMoveMin, groundMoveMax);
            if (groundRect != null)
            {
                var y1 = groundRect.anchoredPosition.y;
                await LerpAnchoredYAsync(groundRect, y1, y1 - moveAmount, groundDescendDuration, EaseInCubic, token);
                groundRect.gameObject.SetActive(false);
            }

            // 4) 星 + 飛んでいるキャラ: ゆっくり縮小 → 続けて星だけ最終 ScaleDown + FadeOut。
            //    地球はこの縮小が終わる earthRiseLeadBeforeStarsEnd 秒前から動き出すので、
            //    この一連は並行 (Forget) で走らせ、こちらは地球の開始時刻まで待つ。
            ShrinkStarsThenFadeOutAsync(token).Forget();

            var waitBeforeEarth = starsShrinkDuration - EarthRiseLead;
            if (waitBeforeEarth > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(waitBeforeEarth), cancellationToken: token);

            // 5) 地球: 下から競り上がる (星の縮小完了より EarthRiseLead 秒早い)
            if (earthRect != null)
            {
                earthRect.gameObject.SetActive(true);
                SetAnchoredY(earthRect, earthStartY);
                await LerpAnchoredYAsync(earthRect, earthStartY, earthRiseY, earthRiseDuration, EaseOutCubic, token);
            }

            // 6) 地球: 上がりきってから縮む (白 FadeIn の前)
            if (earthRect != null && earthShrinkDuration > 0f)
            {
                var eFrom = earthRect.localScale.x;
                await LerpScaleAsync(earthRect, eFrom, earthEndScale, earthShrinkDuration, EaseInOutSine, token);
            }

            // 7) 一定秒数経過
            if (earthHoldBeforeWhite > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(earthHoldBeforeWhite), cancellationToken: token);

            // 8) 画面が白くなる fadein
            await FadeGroupAsync(whiteFade, 0f, 1f, whiteFadeDuration, token);

            // 9) 白到達から winnerEffectDelayAfterWhite 秒後に勝利演出を予約する。
            //    演出予算とは無関係にラウンド中ずっと動くので、effectToken ではなく round トークンで走らせる。
            if (winnerEffectDelayAfterWhite > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(winnerEffectDelayAfterWhite), cancellationToken: token);
            StartWinnerEffect(_roundCts?.Token ?? token);
        }

        // 地球の先行時間。星の縮小尺を超えないようにクランプする。
        private float EarthRiseLead => Mathf.Clamp(earthRiseLeadBeforeStarsEnd, 0f, starsShrinkDuration);

        // 白到達までの基準尺(秒)。連打パワーの換算に使う (whiteReachAlternations 連打でこの秒数)。
        // 地球は星の縮小と EarthRiseLead 秒ぶん重なるので、その分を差し引く。
        private float ToWhiteSeconds()
            => groundRiseDuration + groundShrinkDuration + groundDescendDuration
             + starsShrinkDuration - EarthRiseLead
             + earthRiseDuration + earthShrinkDuration
             + earthHoldBeforeWhite + whiteFadeDuration;

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

        // 勝利演出を予約する。まず敗者の滞空を打ち切って敗北演出 (落下) に入らせ、その開始から
        // winnerEffectMinDelayAfterLoserFall 秒空けてから勝者のスケール変更 + 真横の往復を始める。
        // 往復は一度始まったらラウンドが終わる (or 中断される) まで止めない。二重起動はしない。
        private void StartWinnerEffect(CancellationToken token)
        {
            if (_winnerCts != null) return;

            // 先に敗者を落とす。パワーが残っていてもここで敗北演出に入る。
            _loserFlyCut?.TrySetResult();

            // 勝者キャラ未設定なら演出は無いが、Result を待たせ続けないよう合図だけ立てる
            if (_winnerChar == null)
            {
                _winnerEffectStarted?.TrySetResult();
                return;
            }

            _winnerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            RunWinnerEffectAsync(_winnerCts.Token).Forget();
        }

        private async UniTaskVoid RunWinnerEffectAsync(CancellationToken token)
        {
            try
            {
                // 敗北演出の開始を待ってから、そこから最低 1 秒空ける。
                // (落下開始は _loserFlyCut を受けた次フレーム以降なので、立ち上がりも待つ)
                while (!_loserFell)
                {
                    token.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                var wait = winnerEffectMinDelayAfterLoserFall - (Time.time - _loserFallTime);
                if (wait > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);

                // ここが勝利演出の開始。Result はこの時刻を基準に出す。
                PlayCharacterAnim(_winnerChar);
                _winnerEffectStarted?.TrySetResult();

                // スケール変更と真横の往復は別尺。スケールは一度だけ、往復はずっと。
                ScaleWinnerCharAsync(token).Forget();
                await MoveWinnerCharAsync(token);
            }
            catch (OperationCanceledException) { }
        }

        // 勝者を winnerEffectEndScale (絶対値) まで変化させる (開始は今のスケール)
        private async UniTaskVoid ScaleWinnerCharAsync(CancellationToken token)
        {
            if (_winnerChar == null) return;
            try
            {
                var from = _winnerChar.localScale.x;
                await LerpScaleAsync(_winnerChar, from, winnerEffectEndScale,
                    winnerEffectScaleDuration, EaseOutCubic, token);
            }
            catch (OperationCanceledException) { }
        }

        // 勝者を開始位置を中心に真横へ往復させる (1 周 = winnerEffectMoveDuration)
        private async UniTask MoveWinnerCharAsync(CancellationToken token)
        {
            if (_winnerChar == null) return;
            var homeX = _winnerChar.anchoredPosition.x;
            var period = Mathf.Max(0.01f, winnerEffectMoveDuration);

            var elapsed = 0f;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                if (elapsed >= period) elapsed -= period; // 位相を 1 周でループ
                var offset = Mathf.Sin(elapsed / period * 2f * Mathf.PI) * winnerEffectMoveDistance;
                SetAnchoredX(_winnerChar, homeX + offset);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        // 指定キャラの SpriteAnimation を頭から再生する
        private void PlayCharacterAnim(RectTransform character)
        {
            if (character == null) return;
            if (character == player1Character && player1Anim != null) player1Anim.Play();
            else if (character == player2Character && player2Anim != null) player2Anim.Play();
        }

        // 星の縮小 → 続けて星だけ最終 ScaleDown + FadeOut。地球の演出と並行して走る。
        // 演出停止 (予算切れ) でキャンセルされたらその見た目で固定される。
        private async UniTaskVoid ShrinkStarsThenFadeOutAsync(CancellationToken token)
        {
            try
            {
                await ShrinkStarsAndCharsAsync(starsEndScale, charEndScale, starsShrinkDuration, token);
                await FadeOutStarsAsync(starsFadeOutDuration, token);
            }
            catch (OperationCanceledException) { }
        }

        // 星を消す: alpha 0 まで FadeOut しつつ starsFadeOutEndScale まで縮小 (尺は独立指定)。
        private async UniTask FadeOutStarsAsync(float duration, CancellationToken token)
        {
            var fromScale = starsRect != null ? starsRect.localScale.x : 1f;
            var fromAlpha = GetStarsAlpha();

            if (duration <= 0f)
            {
                SetScale(starsRect, starsFadeOutEndScale);
                SetStarsAlpha(0f);
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetScale(starsRect, Mathf.Lerp(fromScale, starsFadeOutEndScale, EaseInOutSine(t)));
                SetStarsAlpha(Mathf.Lerp(fromAlpha, 0f, t));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetScale(starsRect, starsFadeOutEndScale);
            SetStarsAlpha(0f);
        }

        // CanvasGroup があればそれ、無ければ Stars の Graphic の alpha を使う
        private float GetStarsAlpha()
        {
            if (starsGroup != null) return starsGroup.alpha;
            var graphic = StarsGraphic();
            return graphic != null ? graphic.color.a : 1f;
        }

        private void SetStarsAlpha(float alpha)
        {
            if (starsGroup != null)
            {
                starsGroup.alpha = alpha;
                return;
            }
            var graphic = StarsGraphic();
            if (graphic == null) return;
            var c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        private Graphic StarsGraphic()
        {
            if (_starsGraphic == null && starsRect != null)
                _starsGraphic = starsRect.GetComponent<Graphic>();
            return _starsGraphic;
        }

        // 星の縮小と飛んでいるキャラの縮小を同時進行。開始は現在(prefab)の scale、
        // 到達は starsTo / charTo の絶対値。
        private async UniTask ShrinkStarsAndCharsAsync(float starsTo, float charTo, float duration, CancellationToken token)
        {
            var starsFrom = starsRect != null ? starsRect.localScale.x : 1f;
            var aFrom = _shrinkCharA != null ? _shrinkCharA.localScale.x : 1f;
            var bFrom = _shrinkCharB != null ? _shrinkCharB.localScale.x : 1f;

            if (duration <= 0f)
            {
                SetScale(starsRect, starsTo);
                SetScale(_shrinkCharA, charTo);
                SetScale(_shrinkCharB, charTo);
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = EaseInOutSine(Mathf.Clamp01(elapsed / duration));
                SetScale(starsRect, Mathf.Lerp(starsFrom, starsTo, t));
                SetScale(_shrinkCharA, Mathf.Lerp(aFrom, charTo, t));
                SetScale(_shrinkCharB, Mathf.Lerp(bFrom, charTo, t));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetScale(starsRect, starsTo);
            SetScale(_shrinkCharA, charTo);
            SetScale(_shrinkCharB, charTo);
        }

        // -------- Stage 5: 勝敗演出 (Result 表示) --------
        // 勝利演出が始まってから resultDelayAfterWinnerEffect 秒後に出す。
        private async UniTask PlayWinnerEffectAsync(CancellationToken token)
        {
            _phase = Phase.Winner;

            // 勝利演出は敗北演出の後に始まるので、まず開始を待ってから規定秒数を数える
            if (_winnerEffectStarted != null)
                await _winnerEffectStarted.Task.AttachExternalCancellation(token);
            if (resultDelayAfterWinnerEffect > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(resultDelayAfterWinnerEffect), cancellationToken: token);

            var msg = _p1Won ? "Player 1 Wins!" : "Player 2 Wins!";
            if (winnerText != null) winnerText.text = msg;
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

        // -------- 入力処理 --------
        private void HandlePress(int player, int direction)
        {
            if (_phase == Phase.Playing)
            {
                var state = player == 1 ? _p1 : _p2;
                if (state.locked) return;
                ApplyMashFrame(player, direction);
                if (state.lastDirection == 0)
                {
                    state.lastDirection = direction;
                    UpdateKeyHint(player);
                    return;
                }
                if (state.lastDirection != direction)
                {
                    state.alternations++;
                    state.lastDirection = direction;
                    UpdateKeyHint(player);
                }
                else
                {
                    // 同じキーの連続押し = 誤入力。傾け演出の間はこのプレイヤーの入力を止める。
                    state.locked = true;
                    var missToken = _roundCts?.Token ?? default;
                    PlayMissAsync(player, direction, missToken).Forget();
                }
            }

            // リザルトの再戦 / 退出は ResultInput (1P の A / B) が担当するのでここでは扱わない
        }

        // 連打中: 押した向きのフレームで固定 (アニメーションはしない)
        private void ApplyMashFrame(int player, int direction)
        {
            var anim = player == 1 ? player1Anim : player2Anim;
            if (anim == null) return;
            anim.SetFrame(direction < 0 ? MashFrameLeft : MashFrameRight);
        }

        // 飛び始め: 1番目のフレームから連続アニメーション
        private void StartFlyAnimation()
        {
            if (player1Anim != null) player1Anim.Play();
            if (player2Anim != null) player2Anim.Play();
        }

        // 開始時/リセット: 1番目のフレームでアニメーションなし
        private void ResetAnimToFirstFrame()
        {
            if (player1Anim != null) player1Anim.SetFrame(MashFrameLeft);
            if (player2Anim != null) player2Anim.SetFrame(MashFrameLeft);
        }

        // 誤入力: MISS 表示は出さず、押すべきキー画像を明滅させつつ自キャラを Z 方向に傾けて戻す。
        // wrongDirection = 押してしまった向き。押すべきキーはその逆 (A のときに B を押した → A が明滅)。
        private async UniTaskVoid PlayMissAsync(int player, int wrongDirection, CancellationToken token)
        {
            var character = player == 1 ? player1Character : player2Character;
            var homeRot = player == 1 ? _p1HomeRot : _p2HomeRot;
            var hintGroup = GetKeyGroup(player, -wrongDirection);

            if (character != null)
                character.localRotation = homeRot * Quaternion.Euler(0f, 0f, missTiltAngle);

            try
            {
                await BlinkGroupAsync(hintGroup, missTiltDuration, missBlinkInterval, token);
            }
            catch (OperationCanceledException)
            {
            }

            if (character != null) character.localRotation = homeRot;
            var state = player == 1 ? _p1 : _p2;
            state.locked = false;
            UpdateKeyHint(player); // 明滅を終えて「押すべきキー」の常時表示に戻す
        }

        // 指定プレイヤーの A (左) / B (右) キー画像
        private CanvasGroup GetKeyGroup(int player, int direction)
        {
            if (player == 1) return direction < 0 ? player1KeyAGroup : player1KeyBGroup;
            return direction < 0 ? player2KeyAGroup : player2KeyBGroup;
        }

        // プレイ中は「次に押す必要があるキー」だけを常時表示する。
        // まだ一度も押していない (lastDirection = 0) 間は A から始める案内にする。
        private void UpdateKeyHint(int player)
        {
            var state = player == 1 ? _p1 : _p2;
            var required = state.lastDirection == 0 ? -1 : -state.lastDirection;
            SetGroupAlpha(GetKeyGroup(player, required), 1f);
            SetGroupAlpha(GetKeyGroup(player, -required), 0f);
        }

        // duration の間 interval 間隔で明滅させる (表示から始まる)
        private async UniTask BlinkGroupAsync(CanvasGroup group, float duration, float interval, CancellationToken token)
        {
            if (group == null)
            {
                if (duration > 0f) await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
                return;
            }

            var step = Mathf.Max(0.01f, interval);
            var elapsed = 0f;
            var lit = true;
            while (elapsed < duration)
            {
                SetGroupAlpha(group, lit ? 1f : 0f);
                var wait = Mathf.Min(step, duration - elapsed);
                await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
                elapsed += wait;
                lit = !lit;
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
            // タイマーは開始演出中は出さない (Go の FadeOut 後に PlayGameAsync が表示する)
            if (timerNumber != null) timerNumber.Clear();

            // 開始演出の初期化 (ラベルは非表示。Ready/Go は透明のまま開始スケールに戻す)
            HideOpeningLabels();
            SetGroupAlpha(readyGroup, 0f);
            SetGroupAlpha(goGroup, 0f);
            SetScale(readyRect, _readyHomeScale);
            SetScale(goRect, _goHomeScale);

            // 背景初期化 (prefab の初期 scale/位置を復元)
            if (starsRect != null)
            {
                starsRect.gameObject.SetActive(true);
                // localRotation はリセットしない (常時回転を途切れさせないため)
                starsRect.localScale = _starsHomeScale;
                SetStarsAlpha(1f);
            }
            if (groundRect != null)
            {
                groundRect.gameObject.SetActive(true);
                groundRect.localScale = _groundHomeScale;
                groundRect.anchoredPosition = _groundHomePos;
            }
            if (earthRect != null)
            {
                earthRect.localScale = _earthHomeScale;
                SetAnchoredY(earthRect, earthStartY);
                earthRect.gameObject.SetActive(false);
            }
            if (whiteFade != null) whiteFade.alpha = 0f;

            // キャラ初期化 (prefab の初期 scale/位置/回転を復元)
            _shrinkCharA = null;
            _shrinkCharB = null;
            _winnerChar = null;
            _loserFlyCut = null;
            _winnerEffectStarted = null;
            _loserFell = false;
            ResetCharacter(player1Character, _p1HomeScale, _p1HomePos, _p1HomeRot);
            ResetCharacter(player2Character, _p2HomeScale, _p2HomePos, _p2HomeRot);
            ResetAnimToFirstFrame();

            if (winnerText != null) winnerText.text = string.Empty;
            if (resultGroup != null)
            {
                resultGroup.alpha = 0f;
                resultGroup.interactable = false;
                resultGroup.blocksRaycasts = false;
            }
        }

        private void ResetCharacter(RectTransform character, Vector3 homeScale, Vector2 homePos, Quaternion homeRot)
        {
            if (character == null) return;
            character.gameObject.SetActive(true);
            character.localScale = homeScale;
            character.localRotation = homeRot;
            character.anchoredPosition = homePos;
        }

        // prefab で設定された初期 scale/位置/回転を記録し、以後のリセットで復元する。
        private void CaptureHome()
        {
            if (starsRect != null) _starsHomeScale = starsRect.localScale;
            if (groundRect != null)
            {
                _groundHomeScale = groundRect.localScale;
                _groundHomePos = groundRect.anchoredPosition;
            }
            if (earthRect != null) _earthHomeScale = earthRect.localScale;
            if (player1Character != null)
            {
                _p1HomeScale = player1Character.localScale;
                _p1HomePos = player1Character.anchoredPosition;
                _p1HomeRot = player1Character.localRotation;
            }
            if (player2Character != null)
            {
                _p2HomeScale = player2Character.localScale;
                _p2HomePos = player2Character.anchoredPosition;
                _p2HomeRot = player2Character.localRotation;
            }
            if (readyRect != null) _readyHomeScale = readyRect.localScale.x;
            if (goRect != null) _goHomeScale = goRect.localScale.x;
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

        private static void SetAnchoredX(RectTransform rt, float x)
        {
            if (rt == null) return;
            var pos = rt.anchoredPosition;
            pos.x = x;
            rt.anchoredPosition = pos;
        }

        private static void SetAnchoredY(RectTransform rt, float y)
        {
            if (rt == null) return;
            var pos = rt.anchoredPosition;
            pos.y = y;
            rt.anchoredPosition = pos;
        }

        private static void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg == null) return;
            cg.alpha = alpha;
        }

        // XY のみ変更する (Z は prefab の値を残す)
        private static void SetScale(RectTransform rt, float s)
        {
            if (rt == null) return;
            rt.localScale = new Vector3(s, s, rt.localScale.z);
        }
    }
}
