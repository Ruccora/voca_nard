using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VocaNerd
{
    /// <summary>
    /// 格闘ゲーム / カービィのバトル開始風「上下カットイン」演出。
    ///
    /// 単体で使う場合は StartCoroutine(director.Play()) を呼ぶだけで全シーケンスが走る。
    /// OpeningEffect を継承しているので、ミニゲーム側 (QuickDrawGame 等) の openingEffect に
    /// そのまま挿すこともできる。その場合は
    ///   PlayAsync = Phase1〜4 (帯侵入 → カットイン → 保持)
    ///   ExitAsync = Phase5〜6 (一斉消滅 → 暗転復帰)
    /// に分割されるので、保持中に呼び出し側の待ち時間を挟める。
    ///
    /// 各要素は Inspector で差し替えられる。未設定なら BuildHierarchy() で自動生成する
    /// (エディタの右クリックメニュー "Build Hierarchy" からも実行可能)。
    /// </summary>
    public class BattleIntroDirector : OpeningEffect
    {
        [Serializable]
        public struct CutInConfig
        {
            public Sprite portrait;
            public bool fromLeft; // true=左から侵入 / false=右から侵入
        }

        [Header("Elements (未設定なら自動生成)")]
        [SerializeField] private RectTransform canvasRect; // 比率計算の基準。未設定なら親 Canvas
        [SerializeField] private Image dimPanel;
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform bottomBar;
        [SerializeField] private RectTransform topLine;
        [SerializeField] private RectTransform bottomLine;
        [SerializeField] private RectTransform topPortrait;
        [SerializeField] private RectTransform bottomPortrait;
        [SerializeField] private RectTransform topFlash;
        [SerializeField] private RectTransform bottomFlash;
        [SerializeField] private Image whiteFlash;
        [SerializeField] private RectTransform shakeTarget; // 未設定なら自分自身

        [Header("Cut-In")]
        public CutInConfig topCutIn = new CutInConfig { fromLeft = true };
        public CutInConfig bottomCutIn = new CutInConfig { fromLeft = false };

        [Header("Size (px 指定。0 以下なら下の Ratio を使う)")]
        // キャラ画像の高さに帯を合わせたい場合など、Canvas 単位で直接指定する。
        // Canvas は Reference Resolution 基準でスケールされるので px 指定でも解像度非依存。
        [SerializeField] private float barHeightPixels = 380f;

        [Header("Ratios (画面サイズ比 = 解像度非依存)")]
        [SerializeField] private float barHeightRatio = 0.22f;        // 帯の高さ / 画面高 (px 指定が 0 以下のとき)
        [SerializeField] private float lineThicknessRatio = 0.02f;    // 白ラインの太さ / 画面高
        [SerializeField] private float topLineOffsetRatio = 0.12f;    // 画面上端からの白ライン位置
        [SerializeField] private float bottomLineOffsetRatio = 0.11f; // 画面下端からの白ライン位置
        [SerializeField] private float portraitHeightScale = 1.1f;    // カットイン高 / 帯高
        [SerializeField] private float portraitStartRatio = 0.4f;     // 侵入開始 X (画面幅比・画面外)
        [SerializeField] private float portraitStopRatio = 0.33f;     // 停止 X (侵入側の端から画面幅比)
        [SerializeField] private float flashWidthRatio = 0.05f;       // 閃光の幅 / 画面高
        [SerializeField] private float shakeAmplitudeRatio = 0.01f;   // シェイク振幅 / 画面高
        [SerializeField] private float holdWobblePixels = 2f;         // 保持中の微細な上下揺れ
        [SerializeField] private float holdWobbleSpeed = 7f;

        [Header("Timeline (秒・すべて開始からの絶対時刻)")]
        [SerializeField] private float dimDuration = 0.55f;      // Phase1 暗転
        [SerializeField] private float barSlideDuration = 0.55f; // Phase1 帯の侵入
        [SerializeField] private float lineStartTime = 0.35f;    // Phase2 白ライン
        [SerializeField] private float lineDuration = 0.20f;
        [SerializeField] private float cutInStartTime = 0.55f;   // Phase3 突入
        [SerializeField] private float cutInDuration = 0.30f;
        [SerializeField] private float shakeDuration = 0.06f;
        [SerializeField] private float whiteFlashDuration = 0.05f;
        [SerializeField] private float flashFadeDuration = 0.15f;
        [SerializeField] private float releaseTime = 1.85f;       // Phase5 一斉消滅
        [SerializeField] private float restoreDuration = 0.40f;   // Phase6 復帰

        [Header("Look")]
        [SerializeField] private float dimAlpha = 0.72f;
        [SerializeField] private float whiteFlashAlpha = 0.6f;
        [SerializeField] private Color barColor = Color.black;
        [SerializeField] private Color lineColor = Color.white;

        [Header("SE Trigger")]
        public UnityEvent onBarsClose = new UnityEvent();   // 0.00s 帯の侵入
        public UnityEvent onCutInImpact = new UnityEvent(); // 0.55s カットイン突入
        public UnityEvent onRelease = new UnityEvent();     // 1.85s 一斉消滅

        private bool _impactFired;
        private Vector2 _shakeHome;
        private bool _shakeHomeCaptured;

        // レイアウト計算のキャッシュ (Reset 時に更新)
        private float _screenW;
        private float _screenH;
        private float _barH;
        private Vector2 _topPortraitRange;    // x: start, y: stop
        private Vector2 _bottomPortraitRange;
        private float _topPortraitHalfW;
        private float _bottomPortraitHalfW;

        private float CutInEndTime => cutInStartTime + cutInDuration;

        // ---------------- Public API ----------------

        /// <summary>全シーケンス (Phase1〜6) を再生する。StartCoroutine で回す。</summary>
        public IEnumerator Play()
        {
            var main = MainRoutine();
            while (main.MoveNext()) yield return null;

            var release = ReleaseRoutine();
            while (release.MoveNext()) yield return null;
        }

        // ---------------- OpeningEffect ----------------

        public override UniTask SetupAsync(CancellationToken token)
        {
            EnsureElements();
            return UniTask.CompletedTask;
        }

        /// <summary>Phase1〜4。カットインが出たまま (保持しきった状態で) 戻る。</summary>
        public override UniTask PlayAsync(CancellationToken token) => DriveAsync(MainRoutine(), token);

        /// <summary>Phase5〜6。一斉消滅 → 暗転復帰。</summary>
        public override UniTask ExitAsync(CancellationToken token) => DriveAsync(ReleaseRoutine(), token);

        // yield return null しか含まないコルーチンを UniTask として回す
        private static async UniTask DriveAsync(IEnumerator routine, CancellationToken token)
        {
            while (routine.MoveNext())
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        // ---------------- Phase 1〜4 ----------------

        private IEnumerator MainRoutine()
        {
            ResetToInitial();
            onBarsClose?.Invoke();

            var t = 0f;
            while (t < releaseTime)
            {
                ApplyTimeline(t);
                yield return null;
                t += Time.unscaledDeltaTime; // timeScale=0 でも動く
            }
            ApplyTimeline(releaseTime);
        }

        // ---------------- Phase 5〜6 ----------------

        private IEnumerator ReleaseRoutine()
        {
            onRelease?.Invoke();

            // Phase5: フェードなしで即座に非表示 (1フレームで消す)。ここは切れ味が命なので絶対に補間しない。
            SetPieceActive(false);
            ApplyShake(0f);

            // Phase6: 暗転だけ元に戻す
            var t = 0f;
            while (t < restoreDuration)
            {
                var k = 1f - EaseInQuad(restoreDuration <= 0f ? 1f : Mathf.Clamp01(t / restoreDuration));
                SetDim(dimAlpha * k);
                yield return null;
                t += Time.unscaledDeltaTime;
            }

            SetDim(0f);
            if (this != null) gameObject.SetActive(false);
        }

        // ---------------- Timeline ----------------

        // 全トラックを「開始からの絶対時刻」で評価する。再入しても t から一意に状態が決まる。
        private void ApplyTimeline(float t)
        {
            // Phase1: 暗転
            SetDim(dimAlpha * EaseOutQuad(Ratio(t, 0f, dimDuration)));

            // Phase1: 帯の侵入
            var barK = EaseOutCubic(Ratio(t, 0f, barSlideDuration));
            if (topBar != null) topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(_barH, 0f, barK));
            if (bottomBar != null) bottomBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(-_barH, 0f, barK));

            // Phase2: 白ラインを外側の端から伸ばす
            var lineK = EaseOutQuart(Ratio(t, lineStartTime, lineDuration));
            if (topLine != null) topLine.localScale = new Vector3(lineK, 1f, 1f);
            if (bottomLine != null) bottomLine.localScale = new Vector3(lineK, 1f, 1f);

            // Phase3〜4: カットイン + 先端の閃光
            var cutK = EaseOutExpo(Ratio(t, cutInStartTime, cutInDuration));
            var raw = Ratio(t, cutInStartTime, cutInDuration);
            ApplyCutIn(topPortrait, topFlash, topCutIn, _topPortraitRange, _topPortraitHalfW, cutK, raw, t);
            ApplyCutIn(bottomPortrait, bottomFlash, bottomCutIn, _bottomPortraitRange, _bottomPortraitHalfW, cutK, raw, t);

            // Phase3: 突入と同時のシェイク
            ApplyShake(t);

            // Phase3: 突入完了フレームの白フラッシュ
            if (whiteFlash != null)
            {
                var f = 1f - Ratio(t, CutInEndTime, whiteFlashDuration);
                SetAlpha(whiteFlash, t < CutInEndTime ? 0f : whiteFlashAlpha * f);
            }

            if (!_impactFired && t >= cutInStartTime)
            {
                _impactFired = true;
                onCutInImpact?.Invoke();
            }
        }

        private void ApplyCutIn(RectTransform portrait, RectTransform flash, CutInConfig config,
            Vector2 range, float halfW, float eased, float raw, float t)
        {
            if (portrait == null) return;

            var x = Mathf.Lerp(range.x, range.y, eased);

            // Phase4: 静止させず ±2px の微細な上下揺れを入れて「生きた感じ」を残す
            var wobble = t > CutInEndTime
                ? Mathf.Sin((t - CutInEndTime) * holdWobbleSpeed * Mathf.PI) * holdWobblePixels
                : 0f;
            portrait.anchoredPosition = new Vector2(x, wobble);

            if (flash == null) return;

            // 閃光は進行方向の先端に追従
            var dir = config.fromLeft ? 1f : -1f;
            flash.anchoredPosition = new Vector2(x + dir * halfW, wobble);
            var pulse = Mathf.Lerp(1.3f, 1f, raw);
            flash.localScale = new Vector3(pulse, pulse, 1f);

            var flashImage = flash.GetComponent<Image>();
            if (flashImage == null) return;
            var a = t < cutInStartTime ? 0f : 1f - Ratio(t, CutInEndTime, flashFadeDuration);
            SetAlpha(flashImage, a);
        }

        private void ApplyShake(float t)
        {
            var target = shakeTarget != null ? shakeTarget : (RectTransform)transform;
            if (target == null) return;
            if (!_shakeHomeCaptured)
            {
                _shakeHome = target.anchoredPosition;
                _shakeHomeCaptured = true;
            }

            var active = t >= cutInStartTime && t < cutInStartTime + shakeDuration;
            if (!active)
            {
                target.anchoredPosition = _shakeHome;
                return;
            }

            var amp = _screenH * shakeAmplitudeRatio;
            target.anchoredPosition = _shakeHome + UnityEngine.Random.insideUnitCircle * amp;
        }

        // ---------------- Reset / Layout ----------------

        // Play() を何度呼んでも壊れないよう、開始時に必ずここを通す
        private void ResetToInitial()
        {
            gameObject.SetActive(true);
            EnsureElements();
            ApplyLayout();

            _impactFired = false;
            _shakeHomeCaptured = false;

            SetPieceActive(true);
            ApplyTimeline(0f);
        }

        // 帯・ライン・カットイン・閃光をまとめて表示/非表示 (DimPanel と WhiteFlash は除く)
        private void SetPieceActive(bool active)
        {
            if (topBar != null) topBar.gameObject.SetActive(active);
            if (bottomBar != null) bottomBar.gameObject.SetActive(active);
            if (whiteFlash != null) SetAlpha(whiteFlash, 0f);
        }

        // 画面サイズ比からすべての寸法を計算する (解像度非依存)
        private void ApplyLayout()
        {
            var root = ResolveCanvasRect();
            _screenW = root != null ? root.rect.width : 0f;
            _screenH = root != null ? root.rect.height : 0f;
            // Canvas 外 (prefab 編集中など) では rect が 0 になるので画面サイズで代用する
            if (_screenW < 1f || _screenH < 1f)
            {
                _screenW = Screen.width;
                _screenH = Screen.height;
            }
            _barH = barHeightPixels > 0f ? barHeightPixels : _screenH * barHeightRatio;

            LayoutBar(topBar, true);
            LayoutBar(bottomBar, false);
            LayoutLine(topLine, true);
            LayoutLine(bottomLine, false);

            _topPortraitHalfW = LayoutPortrait(topPortrait, topCutIn, true, out _topPortraitRange);
            _bottomPortraitHalfW = LayoutPortrait(bottomPortrait, bottomCutIn, false, out _bottomPortraitRange);
            LayoutFlash(topFlash, topCutIn);
            LayoutFlash(bottomFlash, bottomCutIn);

            if (dimPanel != null)
            {
                dimPanel.color = new Color(0f, 0f, 0f, 0f);
                dimPanel.raycastTarget = false;
            }
            if (whiteFlash != null)
            {
                whiteFlash.color = new Color(1f, 1f, 1f, 0f);
                whiteFlash.raycastTarget = false;
                whiteFlash.rectTransform.SetAsLastSibling();
            }
        }

        private void LayoutBar(RectTransform bar, bool top)
        {
            if (bar == null) return;
            bar.anchorMin = new Vector2(0f, top ? 1f : 0f);
            bar.anchorMax = new Vector2(1f, top ? 1f : 0f);
            bar.pivot = new Vector2(0.5f, top ? 1f : 0f);
            bar.sizeDelta = new Vector2(0f, _barH);
            bar.anchoredPosition = new Vector2(0f, top ? _barH : -_barH); // 画面外から

            var img = bar.GetComponent<Image>();
            if (img != null)
            {
                img.color = barColor;
                img.raycastTarget = false;
            }
        }

        private void LayoutLine(RectTransform line, bool top)
        {
            if (line == null) return;
            // 帯の子。Pivot は「侵入側 = 外側の端」に置き、横スケール 0→1 でそこから伸ばす。
            var fromLeft = top ? topCutIn.fromLeft : bottomCutIn.fromLeft;
            line.anchorMin = new Vector2(0f, top ? 1f : 0f);
            line.anchorMax = new Vector2(1f, top ? 1f : 0f);
            line.pivot = new Vector2(fromLeft ? 0f : 1f, 0.5f);
            line.sizeDelta = new Vector2(0f, _screenH * lineThicknessRatio);
            // 帯の外側の端 = 画面の上端 / 下端。そこからのオフセットで縦位置を決める。
            var offset = _screenH * (top ? topLineOffsetRatio : bottomLineOffsetRatio);
            line.anchoredPosition = new Vector2(0f, top ? -offset : offset);
            line.localScale = new Vector3(0f, 1f, 1f);

            var img = line.GetComponent<Image>();
            if (img != null)
            {
                img.color = lineColor;
                img.raycastTarget = false;
            }
        }

        // 戻り値: カットイン画像の半幅 (閃光の追従位置に使う)
        private float LayoutPortrait(RectTransform portrait, CutInConfig config, bool top, out Vector2 range)
        {
            range = Vector2.zero;
            if (portrait == null) return 0f;

            var img = portrait.GetComponent<Image>();
            if (img != null)
            {
                if (config.portrait != null) img.sprite = config.portrait;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 1f);
            }

            var h = _barH * portraitHeightScale;
            var sprite = config.portrait != null ? config.portrait : (img != null ? img.sprite : null);
            var aspect = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
            var w = h * aspect;
            portrait.sizeDelta = new Vector2(w, h);

            // アンカーを侵入側の端に置く → X は「その端から何ピクセル入ったか」になる
            var ax = config.fromLeft ? 0f : 1f;
            portrait.anchorMin = new Vector2(ax, 0.5f);
            portrait.anchorMax = new Vector2(ax, 0.5f);
            portrait.pivot = new Vector2(0.5f, 0.5f);

            var dir = config.fromLeft ? 1f : -1f;
            range = new Vector2(-dir * _screenW * portraitStartRatio, dir * _screenW * portraitStopRatio);
            portrait.anchoredPosition = new Vector2(range.x, 0f);
            return w * 0.5f;
        }

        private void LayoutFlash(RectTransform flash, CutInConfig config)
        {
            if (flash == null) return;
            var ax = config.fromLeft ? 0f : 1f;
            flash.anchorMin = new Vector2(ax, 0.5f);
            flash.anchorMax = new Vector2(ax, 0.5f);
            flash.pivot = new Vector2(0.5f, 0.5f);
            flash.sizeDelta = new Vector2(_screenH * flashWidthRatio, _barH * 1.25f);

            var img = flash.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = false;
                SetAlpha(img, 0f);
            }
        }

        private RectTransform ResolveCanvasRect()
        {
            if (canvasRect != null) return canvasRect;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = canvas.rootCanvas.transform as RectTransform;
            return canvasRect != null ? canvasRect : transform as RectTransform;
        }

        private void SetDim(float alpha)
        {
            if (dimPanel != null) SetAlpha(dimPanel, alpha);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        // シェイク中に中断されても位置がずれたまま残らないようにする
        private void OnDisable()
        {
            if (_shakeHomeCaptured && shakeTarget != null) shakeTarget.anchoredPosition = _shakeHome;
        }

        // ---------------- Easing ----------------

        // 経過時刻 t を [start, start+duration] の 0..1 に正規化
        private static float Ratio(float t, float start, float duration)
            => duration <= 0f ? (t >= start ? 1f : 0f) : Mathf.Clamp01((t - start) / duration);

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseOutQuart(float t) => 1f - Mathf.Pow(1f - t, 4f);
        private static float EaseOutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

        // ---------------- Hierarchy 自動生成 ----------------

        /// <summary>
        /// 未設定の要素を生成して階層を組む。すでに参照が刺さっているものは触らない。
        /// エディタで一度実行しておけば、以後は Inspector から自由に差し替えられる。
        /// </summary>
        [ContextMenu("Build Hierarchy")]
        public void BuildHierarchy()
        {
            EnsureElements();
            ApplyLayout();
        }

        private void EnsureElements()
        {
            var self = transform as RectTransform;
            if (self == null) return;
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.offsetMin = Vector2.zero;
            self.offsetMax = Vector2.zero;

            if (dimPanel == null) dimPanel = CreateImage(self, "DimPanel", new Color(0f, 0f, 0f, 0f), true);
            if (topBar == null) topBar = CreateImage(self, "TopBar", barColor, false).rectTransform;
            if (bottomBar == null) bottomBar = CreateImage(self, "BottomBar", barColor, false).rectTransform;
            if (topLine == null) topLine = CreateImage(topBar, "TopLine", lineColor, false).rectTransform;
            if (bottomLine == null) bottomLine = CreateImage(bottomBar, "BottomLine", lineColor, false).rectTransform;
            if (topPortrait == null) topPortrait = CreateImage(topBar, "TopPortrait", Color.white, false).rectTransform;
            if (bottomPortrait == null) bottomPortrait = CreateImage(bottomBar, "BottomPortrait", Color.white, false).rectTransform;
            if (topFlash == null) topFlash = CreateImage(topBar, "TopFlash", new Color(1f, 1f, 1f, 0f), false).rectTransform;
            if (bottomFlash == null) bottomFlash = CreateImage(bottomBar, "BottomFlash", new Color(1f, 1f, 1f, 0f), false).rectTransform;
            if (whiteFlash == null) whiteFlash = CreateImage(self, "WhiteFlash", new Color(1f, 1f, 1f, 0f), true);
            if (shakeTarget == null) shakeTarget = self;
        }

        private static Image CreateImage(RectTransform parent, string name, Color color, bool stretchFull)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (stretchFull)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
