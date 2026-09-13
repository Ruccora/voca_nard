# VocaNerd 要件一覧

このドキュメントは会話中で決めた要件と、それぞれがどこに実装されているかをまとめたものです。

---

## 0. プロジェクト全体

### 0.1 ゲームフロー
- **要件**: タイトル → 4 つのミニゲーム選択画面 → 画像タップで動画再生 → Play ボタンでミニゲーム開始
- **実装**:
  - `TitlePanel.cs` → `SelectPanel.cs` → `ExplainPanel.cs`（モーダル）→ `MiniGamePanel.cs`
  - `ScreenType`: Title / Select / MiniGame（Explain はモーダル化で ScreenType から除外）

### 0.2 UniTask 使用
- **要件**: UniTask を使用したい
- **実装**: `Packages/manifest.json` に `com.cysharp.unitask` を UPM 経由で追加

### 0.3 説明画面のメディア
- **要件**: .mp4 動画を再生
- **実装**: `ExplainPanel.SetupAsync` で `VideoPlayer.Prepare`、In が終わってから `Play`（5.2）

### 0.4 単一シーン + Panel 切替
- **要件**: 単一シーンで Panel を Instantiate/Destroy 方式で切替
- **実装**: `ScreenController.ShowAsync` が Prefab を Instantiate、旧 Panel を Destroy

### 0.5 4:3 レターボックス
- **要件**: 4:3 でレターボックス強制、参照解像度 1600x1200
- **実装**: `MainCanvas.prefab`
  - CanvasScaler 参照解像度 1600x1200
  - AspectRatioFitter (FitInParent, 4/3) で 4:3 領域を維持
  - LetterboxBackground（全画面黒 Image）で余白を黒帯化

---

## 1. Panel 基底 (`PanelBase.cs`)

### 1.1 ライフサイクルメソッド
- **要件**: `SetupAsync` / `PanelInAsync` / `PanelOutAsync` を基底で保持
- **実装**: `PanelBase.cs`

### 1.2 IsAnimating フラグ
- **要件**: アニメ中は押せない、基底でフラグ保持、ボタン処理側で tap block
- **実装**: `PanelBase.IsAnimating`、各ボタンハンドラで `if (IsAnimating) return;`

### 1.3 alpha=0 スタート
- **要件**: 新規 Instantiate 時 alpha=0 → PanelIn で fade in が自然に見えるように
- **実装**: `PanelBase.Awake` で `canvasGroup.alpha = 0f`

### 1.4 各 Panel を PanelBase 派生に統一
- **要件**: 統一ライフサイクル
- **実装**: TitlePanel / SelectPanel / ExplainPanel / MiniGamePanel、各ミニゲームクラスも全て派生

---

## 2. 画面遷移 (`ScreenController.cs`)

### 2.1 遷移フロー
- **要件**: `PanelOut → TransitionIn → Destroy → Instantiate → SetupAsync → TransitionOut → PanelIn`
- **実装**: `ScreenController.ShowAsync`

### 2.2 遷移演出の分離
- **要件**: 遷移演出（黒フェード）を差し替えやすくメソッド抜き出し
- **実装**: `protected virtual TransitionInAsync` / `TransitionOutAsync` に集約、`BlackFadeOverlay` 呼び出しはこの2メソッドのみ

### 2.3 初回スキップ
- **要件**: 初回 Show（`_current == null`）は TransitionIn/Out スキップ
- **実装**: `hasCurrent` フラグで分岐

### 2.4 ExplainPanel モーダル化
- **要件**: ExplainPanel を SelectPanel の子として生成（Screen ではなくオーバーレイ）
- **実装**: `SelectPanel.OpenExplainAsync` で `Instantiate(explainPanelPrefabs[index], explainRoot)`（`explainRoot` 未設定なら SelectPanel 自身が親）、Back で `PanelOutAsync + Destroy(self)`

### 2.5 モーダル背景
- **要件**: SelectPanel を暗く見せる（推定）
- **実装**: `ExplainPanel.prefab` に `Scrim` (半透明黒 alpha=0.7) を最下層に追加

---

## 3. BlackFadeOverlay (`BlackFadeOverlay.cs`)

### 3.1 API
- **要件**: singleton、秒数指定可能、FadeIn は input block、FadeOut は input block なし
- **実装**:
  - `Instance` static プロパティ
  - `FadeInAsync(duration, token)` → `blocksRaycasts = true`
  - `FadeOutAsync(duration, token)` → `blocksRaycasts = false`

### 3.2 配置方法
- **要件**: Scene 配置（Runtime 生成ではなく）
- **実装**: `BlackFadeOverlay.prefab` を PrefabGenerator が生成、ユーザーがシーンにドラッグ

---

## 4. サウンド (`AudioManager.cs` / `AudioLibrary.cs` / `Audio.cs` / `AudioKeys.cs`)

### 4.0 全体構成
- **要件**: SE と BGM を再生できる仕組み。呼び出し側は AudioClip を持ち回らず、文字列キーで鳴らす
- **実装**: 4 レイヤ
  | ファイル | 役割 |
  |---|---|
  | `AudioKeys.cs` | `BgmKey` / `SeKey` の string 定数（`SaveData` と同じ定数クラス方式）。`All` 配列は AudioLibrary 自動生成用 |
  | `AudioLibrary.cs` | ScriptableObject。キー → AudioClip + 個別音量倍率 の対応表。Dictionary をキャッシュ（`OnValidate` で破棄） |
  | `AudioManager.cs` | singleton。AudioSource を持ち、ライブラリを引いて実再生 |
  | `Audio.cs` | スタティック窓口。`AudioManager` 不在なら全て無処理なので呼び出し側は存在チェック不要 |

```csharp
Audio.PlaySE(SeKey.Decide);
Audio.PlayBgm(BgmKey.Title);                          // 投げっぱなし
await Audio.PlayBgmAsync(BgmKey.Select, 1f, token);   // フェード完了まで待つ
Audio.StopBgm(0.5f);
```

音源は `Assets/Audio/`（`Assets/Audio/README.md` に追加手順と Import 設定の目安）。

### 4.1 BGM
- **要件**: BGM とその再生機構
- **実装**: `PlayBgmAsync(key | clip, fadeDuration)` / `StopBgmAsync(fadeDuration)`、AudioSource×2 でクロスフェード
- 同じキーが既に鳴っていれば **no-op**（画面遷移で BGM が鳴り直さない）
- 空キーは no-op = 「BGM 据え置き」の意味

### 4.2 SE
- **要件**: SE 再生機構
- **実装**: `PlaySE(key | clip, volumeScale)` で `PlayOneShot`（多重再生可）、`PlaySEAt` で 3D 再生
- 最終音量 = `Master × Se × ライブラリの volumeScale × 引数の volumeScale`

### 4.3 ボリューム
- **実装**: MasterVolume / BgmVolume / SeVolume を 0-1 で保持、setter で即時反映
- `persistVolumes` ON（既定）で `SaveData` (PlayerPrefs) に永続化、`Awake` で読み戻す
- キー: `VocaNerd.Audio.MasterVolume` / `.BgmVolume` / `.SeVolume`

### 4.4 UI ボタン SE の自動化
- **要件**: 各画面で SE 呼び出しを書かなくても決定音が鳴る
- **実装**: `PanelBase.Awake` が配下の `Button` 全てに SE リスナーを差す（`autoButtonSe` / `buttonSeKey`）
  - `base.Awake()` が先に走るので、SE は派生クラスの本処理より先に鳴る
  - `IsAnimating` 中は鳴らさない
  - 入れ子 Panel 配下のボタンは所有 Panel 側に任せる（二重登録防止）
  - 個別に変えたいボタンには `ButtonSeKey` コンポーネントでキーを上書き（空文字 = 無音）。PrefabGenerator は Back ボタンに `SeKey.Cancel` を付ける

### 4.5 カーソル移動 SE の自動化
- **実装**: `SelectionIndicator.LateUpdate` で選択対象が別 target に移った瞬間に `cursorSeKey` を再生
  - `Show()` 直後の初回確定（`_lastSelected == null`）は移動ではないので鳴らさない

### 4.6 画面ごとの BGM 自動切替
- **実装**:
  - `ScreenController.ScreenEntry.bgmKey` — Title / Select はここで指定。`ShowAsync` が Instantiate 直後に投げっぱなしで再生し、遷移演出と並行してクロスフェードする
  - `ExplainPanelBase.bgmKey` — ミニゲームは説明画面 prefab 側で持ち、`MiniGamePanel.Bind(prefab, bgmKey)` 経由で `SetupAsync` が再生（ScreenController の MiniGame 枠は**空にしておく**）

### 4.7 ミニゲーム内 SE
- 自動化対象外。`SeKey.Countdown` / `Start` / `Win` / `Lose` / `Miss` / `Hit` を用意してあるので、
  各ミニゲームの演出メソッド内から `Audio.PlaySE(...)` を直接呼ぶ

---

### 2.6 SelectPanel の開始演出
- **要件**: FadeIn → 1 秒おいて、指定した object を 1.3 倍に拡大。拡大したらそのまま維持する
- **実装**: `SelectPanel.OnPanelInAsync`
  1. `canvasGroup.alpha` 0 → 1（`fadeDuration`）
  2. `introScaleUpDelay`（既定 1 秒）待機
  3. `introScaleUpRect` を `introScaleUpFactor`（既定 1.3）倍へ `introScaleUpDuration`（既定 0.8 秒）で拡大 → 戻さない
  4. 最後に `FocusDefaultSelected()` と `selectionIndicator.Show()`
- 待機・拡大とも `unscaledDeltaTime` 基準
- 拡大の基準は Awake で記録した prefab の `localScale`
- 旧演出（ヘッダーが上から / ボタンが左右から入ってくるスライド）は廃止。
  `headerRect` も使わなくなったので削除
- ボタン個別の演出（選択したボタンが全画面に拡大 → 戻る）も廃止。
  `expandDuration` / `miniGameButtonRects` / `ExpandSelectedAsync` / `CollapseSelectedAsync` /
  `HideSelected` / `ShowSelected` を削除し、`OnPanelPreOutAsync` はカーソルを隠すだけになった

### 2.6.1 ミニゲームボタンのナビゲーション
- **要件**: ボタンが 3 個など半端な数でも、カーソル (選択) がどこかへ消えないこと
- **実装**: `SelectPanel.SetupNavigation`
  - `miniGameButtons` の **未設定 (null) スロットを除いた実体だけ**で、`navigationColumns`
    （既定 2）列のグリッドとして Explicit ナビを配線する（旧実装は 4 個未満だと配線自体を
    スキップしていて、Automatic ナビのまま隣接外へ飛べてしまっていた）
  - 行末では左右を打ち止め、真下が無い場合のみ次の行の最後へ送る
  - **カーソルが消える主因は `SelectionIndicator.targets` の入れ忘れ**
    （targets に無い Selectable が選ばれると `hideWhenNoMatch` で alpha 0 になる）。
    エディタ実行時に `SelectionIndicator.HasTarget` で照合し、漏れていれば警告を出す

### 2.7 選択中のハイライト
- **要件**: 選択時の UIEffect は Edge Shiny ではなく色で表現する。**選択中の対象に UIEffect が
  付いていれば、その明度 (Value) を少し上げる**。`ShinyOutline` は廃止
- **実装**: `SelectionIndicator`
  - **prefab で設定された `colorFilter` / `color` は変えない。効き具合の数値 (`colorIntensity`) だけを動かす**
  - `selectedIntensityShift`（既定 0.25、0 で無効）ぶん `colorIntensity` を上げる（0〜1 でクランプ）
  - 対象の探索は「対象自身 → 配下」の順に `UIEffect` を検索。**無ければ何もしない**（こちらからは付けない）
  - 適用時に `colorIntensity` を退避し、選択が外れた / `Hide()` / `OnDisable` で元に戻す
  - 想定している prefab 側の作り: ボタンの UIEffect に
    `ColorFilter = MultiplyAdditive` / `Color = 白` / `Color Intensity = 0` を設定しておく。
    シェーダ上は `color.rgb * (1 + 白) = 2倍` を intensity で lerp するので、intensity が明るさの量になる
  - `ShinyOutline.cs` と、`Selectable.prefab` / `ExplainPanel.prefab` に付いていた
    ShinyOutline コンポーネントは削除済み

---

### 2.8 パッドの A / B の割り当て (`GamepadButtons.cs`)
- **背景**: Unity の control 名は **位置** で決まる (`buttonSouth` = 下 / `buttonEast` = 右)。
  Xbox パッドは下に A と印刷されているので世間では「A = buttonSouth」で通っているが、
  このプロジェクトで使うパッドは **A が右 / B が下** の任天堂式ラベルなので、
  そのまま組むと「B と書いてあるボタンで決定」になってしまう
- **要件**: 印刷されたラベルの A が決定 (Xbox の A と同じ役割) になるよう全体で揃える
- **実装**: `GamepadButtons.A` = `<Gamepad>/buttonEast` / `GamepadButtons.B` = `<Gamepad>/buttonSouth`
  - 全ミニゲーム・`ResultInput`・`ExplainPanelBase` はこの定数経由でバインドする
    (生の `<Gamepad>/buttonSouth` を直接書かない)
  - EventSystem の Submit / Cancel は既定で `*/{Submit}` = 下ボタン固定なので、
    `UiInputPlayerOne` が UI アクションの複製を作って Submit = A / Cancel = B に差し替える
  - `ZhiXuGamepad` のレイアウトは**位置どおり**のまま。ラベルの読み替えはこの 1 ファイルに閉じる
  - 別ラベルのパッドに乗り換えるときは `GamepadButtons` の 2 行だけ直せばよい

### 2.7 UI 入力を 1P に限定 (`UiInputPlayerOne.cs`)
- **要件**: タイトル / ミニゲーム選択をコントローラーで操作できるようにしつつ、
  操作できるのは 1P だけにする
- **背景**: シーンの EventSystem は `InputSystemUIInputModule` + Input System 既定の
  `DefaultInputActions`。Navigate は dpad / スティック、Submit は `*/{Submit}` (= パッドの A)、
  Cancel は `*/{Cancel}` (= B) に割り当たっているので、そのままだと **2P のパッドでも決定が通る**
- **実装**: `UiInputPlayerOne`
  - UI アクションアセットに「キーボード / マウス / `Gamepad.all[0]`」だけのデバイスマスク
    (`InputActionAsset.devices`) を掛ける。1P の定義は `PlayerDevices` と同じ接続順
  - `InputSystem.onDeviceChange` でパッドの抜き差しのたびに掛け直す
  - `[RuntimeInitializeOnLoadMethod]` で常駐オブジェクトを自動生成するのでシーン配置は不要
  - ミニゲーム本体と説明画面 (`ExplainPanelBase`) は自前の `InputAction` を使っているので影響なし
    （2P はこれまで通り遊べる）

---

## 5. 説明画面 (`ExplainPanelBase.cs` / `ExplainPanel.cs`)

- **要件**: 説明画面 (ゲーム開始前に動画を流す画面) はミニゲームごとに prefab を分けて持つ。
  ScriptableObject (旧 `MiniGameData`) の仕組みは廃止。基底クラスは 1 つだけ用意し、
  そこで共有するのは **In / Out のアニメーション** と **Play / Back の配線** のみ
- **実装**:
  - `ExplainPanelBase : PanelBase` (abstract)
    - SerializeField: `miniGamePrefab` / `bgmKey` / `playButton` / `backButton` /
      `selectionIndicator` / `slideFromLeftRect` / `slideFromRightRect` / `slideFromBottomRect` / `slideMargin`
    - `Closed` — Back で閉じるまで待つ signal (画面ごと破棄されたらキャンセル)
    - `OnAfterPanelInAsync` — In が終わった後の hook (動画の再生開始などはここ)
    - Play → `ScreenController.ShowAsync(MiniGame, go => …Bind(miniGamePrefab, bgmKey), fadeToBlack: true)`
  - `ExplainPanel : ExplainPanelBase` — 動画を流す既定の実装。
    VideoPlayer / RawImage / VideoClip / videoFileName だけを持つ
  - 旧 `MiniGameData` の Title / Thumbnail / Description は廃止。サムネや文言は
    prefab 上に直接置く (コードからは触らない)

### 5.1 In / Out アニメーション
- **要件**:
  - In = **まず画面全体が FadeIn で入り、その後**「左から右へ」「右から左へ」「下から上へ」
    入ってくる 3 つの object が **同じ秒数**で動く
  - Out = スライドだけ逆 (定位置から画面外へ)
- **実装**: `ExplainPanelBase.OnPanelInAsync` → `FadeAsync` → `PlaySlideAsync(inward, offscreen, token)`
  - 対象は `slideFromLeftRect` / `slideFromRightRect` / `slideFromBottomRect` の 3 つ。
    未設定のものは飛ばす
  - 定位置は Awake で `anchoredPosition` を記録。画面外の位置 (`CalcOffscreen`) は
    `パネル半分 + object 半分 + slideMargin` で毎回計算する
  - **FadeIn の前に 3 object を画面外へ逃がしておく** (定位置のまま一緒にフェードインすると
    「その後入ってくる」演出にならない)
  - 尺は 2 つに分離。FadeIn = `PanelBase.fadeDuration` / 移動 = `slideDuration` (既定 0.5 秒、0 で移動なし)
  - In は EaseOutCubic、Out は EaseInCubic

### 5.2 動画再生
- **要件**: VideoPlayer を SerializeField で持ち、どれを再生するかも prefab 側で指定。
  **再生開始は In が終わってから**
- **実装**: `ExplainPanel`
  - `videoPlayer` / `videoDisplay` / `videoClip` / `videoFileName` を SerializeField で保持
  - `SetupAsync` で source 設定と `Prepare()` までを済ませる (ここでは再生しない)
  - `OnAfterPanelInAsync` で `isPrepared` を最大 3 秒待ってから `Play()`
  - WebGL は `videoFileName` (StreamingAssets)、それ以外は `videoClip` を優先

---

### 5.3 ルール枠の演出 (`ExplainPresentation`)
- **要件**: 説明の中身はゲームごとに違う (SpriteAnimation が 1 個 / 2 個、別 Image の
  パラパラ切替が要る等)。汎用の基底クラスを 1 つ用意し、各画面はそれを継承して対応する
- **実装**:
  - `ExplainPresentation : MonoBehaviour` (abstract) — ExplainPanel とは**別 component** として
    prefab に付け、`ExplainPanelBase.presentation` に割り当てる
    - `Play(token)` / `Stop()` — In のスライド完了後に再生、Out と OnDisable / 破棄で停止
    - 派生は `RunAsync(token)` に「ずっと回り続ける」形で書き、停止はキャンセルに任せる
    - 部品:
      | メソッド | 動き |
      |---|---|
      | `PlayOnceAsync(anim)` | 頭から 1 周再生して最終フレームで止める (prefab の loop 設定に依存しない) |
      | `LoopWithPauseAsync(anim, pause)` | 1 周 → pause 秒待機 → 再開 を繰り返す |
      | `FlipImageAsync(image, sprites, interval)` | Image の sprite を interval 秒ごとに順番に差し替え続ける |
      | `SetSprite(image, sprite)` | 1 枚だけ差し替える |
  - 派生クラス:
    | クラス | 演出 |
    |---|---|
    | `QuickDrawExplainPresentation` | SpriteAnimation 2 つを**並行**でループ。どちらも最後まで再生 → `loopPause` (既定 2 秒) 待機 → 再開 |
    | `HopscotchExplainPresentation` | SpriteAnimation 2 つを**交互に 1 つずつ**再生 (同時には動かない)。A/B の番に合わせて別 Image を `spriteForA` / `spriteForB` に差し替える |
    | `MashRaceExplainPresentation` | SpriteAnimation 1 つをループしつつ、別 Image を `flipSprites` の 2 枚で `flipInterval` (既定 1 秒) ごとに切替 |

---

## ミニゲーム 早見表

| # | クラス | ジャンル | P1 入力 | P2 入力 | 時間/長さ | 勝利条件 |
|---|---|---|---|---|---|---|
| 1 | `QuickDrawGame` | 早撃ち反射 | `A` / SouthGP | `L` / EastGP | 単発ラウンド | 先押しで勝ち、Wait 中押下はフォール |
| 2 | `MashRaceGame` | 交互連打 | `A`/`D` | `←`/`→` | 10秒 | 連打数の多い方 |
| 3 | `HopscotchRaceGame` | けんけんぱ | `A`/`D` | `←`/`→` | 30マス | 先にゴール |
| 4 | `BlockDropGame` | だるま落とし | `A`/`D` + `W`/`S` | `←`/`→` + `↑`/`↓` | 30ブロック | 先に全消去 |

---

## 6. ミニゲーム #1: QuickDrawGame

### 概要
```
Idle → Intro → Waiting(3~5秒) → Ready(画像表示) → Reaction → Winner → WaitForExit → Exiting → Select
                              ↘ (押下でフォール) ↗
```

### 6.1 ゲームルール
- **要件**: 開始演出 → 3〜5秒待機 → 画像表示 → 早押しで勝ち、Waiting 中の押下はフォール即敗北
- **実装**: `QuickDrawGame.cs`

### 6.2 演出フロー (5+2段階)
- **要件**: 開始 → 待機 → 表示 → 押下時 → 勝利 → 任意ボタン → 抜け
- **実装**: `PlayIntroEffectAsync` / `PlayWaitAsync` / `PlayRevealEffectAsync` / `WaitForPressAsync` / `PlayPressEffectAsync` / `PlayWinnerEffectAsync` / `WaitForExitPressAsync` / `PlayExitEffectAsync`

### 6.3 入力
- **要件**: キーボード + コントローラー
- **実装**: P1 = `A` / GamepadSouth、P2 = `L` / GamepadEast

### 6.4 明示的ライフサイクル
- **要件**: Unity ライフサイクル依存でなく Panel と同等の明示的初期化
- **実装**: `SetupAsync`（InputAction 作成 + View reset）、`OnPanelInAsync`（Enable + Round 開始）、`OnPanelOutAsync`（Cancel + Disable）、`OnDestroy`（Dispose のみ）

### 6.5 P1/P2 の識別性向上
- **要件**: 画面から誰が何のキーか分かる
- **実装**: 左右端に「P1 (A)」「P2 (L)」ラベル、色分け（青/赤）

### 6.6 終了後の任意ボタン退出
- **要件**: 勝利演出完了後、任意ボタン押下で抜ける演出 → メニュー画面へ
- **実装**: `WaitForExitPressAsync` + `PlayExitEffectAsync` → `ScreenController.ShowAsync(Select)`

---

---

## 7. ミニゲーム #2: MashRaceGame

### 概要
```
Idle → Opening(黒フェード明け → キャラ 0/4 往復 ×4 → ラベル非表示 → Ready → Go) → Playing(10秒 / Go の FadeOut 完了で開始)
     → Result (キャラ上昇 → 背景スクロール → object 生成 → 背景停止 → object 継続 → キャラ落下)
     → Winner → WaitForExit → Exiting → Select
```

### 7.0 開始演出
- **要件**:
  - 遷移の黒フェードで入ってきた後に開始
  - キャラを 0 / 4 フレームで交互アニメーション。0 のとき A 画像、4 のとき B 画像を表示 (明滅なし)
  - 4 往復したら完全停止
  - `Ready` の開始時に P1 / P2 ラベルと A / B 表示を消す (ここから先はゲーム前だと分かるように)
  - `Ready` が ScaleDown + FadeIn で登場 (FadeIn は ScaleDown と同時開始・尺は別指定)
  - `Go` が ScaleUp + FadeOut。FadeOut は ScaleUp が半分まで進んでから残り時間で消える
  - **ゲーム開始は `Go` の FadeOut 完了時**。同時にタイマーを表示し、A / B は「次に押す必要があるキー」を常時表示する
- **実装**: `MashRaceGame.PlayOpeningAsync`
  - `ScreenController.WaitForTransitionFadeAsync` で明転を待つ
  - `PlayOpeningCharacterAsync` — `openingCycles` 回 `ShowOpeningFrameAsync(0/4)`、A/B は CanvasGroup の alpha 切り替え
  - `HideOpeningLabels` (= `HidePlayerLabels` + `HideKeyLabels`) を Ready の直前に呼ぶ
  - `PlayReadyAsync` — ScaleDown と FadeIn を `UniTask.WhenAll` で同時進行 (`readyScaleDuration` / `readyFadeInDuration`)
  - `PlayGoAsync` — `goFadeOutStartRatio` (既定 0.5) 到達後の残り時間で FadeOut。`RunRoundAsync` が await するので完了 = ゲーム開始
  - `PlayGameAsync` の頭で `SetTimer(playDuration)` + `UpdateKeyHint(1/2)`、終了時に `HideKeyLabels`
- **スケール指定の方針**: 倍率 (比率) は使わない。**開始スケール = prefab で設定した scale**、
  **到達スケール = Inspector の `〜EndScale` (絶対値)**。対象は `readyEndScale` / `goEndScale` /
  `groundEndScale` / `starsEndScale` / `charEndScale` / `earthEndScale`。`SetScale` は XY のみ変更し Z は prefab の値を残す
  - `SetTimer` — 残り時間を「秒:1/100秒」(`09:58` = 9.58 秒) で `SpriteNumber` に表示。端数は切り上げで 0 になって初めて `00:00`
  - タイマーは結果演出の飛び始め (`PlayResultEffectAsync` の `StartFlyAnimation` 直後) に `Clear()` で消す

### 7.0.1 押すキーの常時表示
- **要件**: プレイ中は A / B のどちらを押す必要があるかを常に表示する
- **実装**: `UpdateKeyHint(player)`
  - 次に必要な向き = `lastDirection == 0 ? 左(A) : -lastDirection`。該当する CanvasGroup だけ alpha 1
  - 押下成功 (`HandlePress`) ごとに更新。ミス明滅 (`PlayMissAsync`) の後もここへ戻す

### 7.1 ゲームルール
- **要件**: 左右交互連打でゲージ増加、同方向連打はミス + 0.2 秒ロック、10 秒プレイ
- **実装**: `MashRaceGame.cs`

### 7.2 入力
- **要件**: キーボード AD と ←→
- **実装**: P1 = `A`/`D`、P2 = `←`/`→`

### 7.3 レイアウト
- **要件**: 縦割り（P1 左、P2 右、中央 Divider）
- **実装**: `CreateHalfAreaVertical` + Divider

### 7.4 結果演出（複合スクロール）
- **要件**:
  - キャラは cruise 位置 (500px？) で固定、背景スクロールで飛行感
  - 最後 200px で減速
  - 背景 3000px あるが最大到達したら背景停止、object のみスクロール
  - object 生成は背景最大到達の半分前から
- **実装**: `AnimateFlyAsync`
  - Phase 1: キャラ上昇 (`charRiseDuration`)
  - Phase 2: 背景を `bgTarget = min(bgMaxScroll, totalFly)` までスクロール
  - Phase 2 途中 (`bgMaxScroll/2` 到達) から Object 生成開始
  - `bgMaxScroll` 到達で背景停止、Object のみ継続
  - 最後 `decelZone` (200px) で減速

### 7.4.1 勝敗によるキャラの挙動 (現行)
- **要件**:
  - **引き分けなし**。連打数が同数ならランダムで 1P / 2P のどちらかを勝者にする
  - 敗者も溜まったエネルギー (連打数) ぶんは勝者と一緒に飛ぶ。尽きたら先に落ちる
  - **勝利演出を始める時、敗者はパワーが残っていても敗北演出 (落下) に入る**
  - **勝利演出は敗北演出 (落下) の開始から最低 1 秒空ける** (`winnerEffectMinDelayAfterLoserFall`)
  - 勝利演出は Z 回転ではなく **指定スケールへの変更 + 真横の往復移動**。スケールの尺と往復の尺は別物。
    往復はラウンド中ずっと続き、スプライトアニメーションも再生し続ける
  - 勝利演出を予約するタイミングは、パワーが余って白に到達した場合は **白 FadeIn の 1 秒後**
    (`winnerEffectDelayAfterWhite`)。予算切れで白に届かなかった場合はシーケンス終了時
  - **Result (勝敗表示 + 入力受付) は勝利演出の開始から 1.5 秒後** (`resultDelayAfterWinnerEffect`)
- **実装**: `MashRaceGame.PlayResultEffectAsync`
  - 勝者判定は `_p1Won`（同数時は `UnityEngine.Random.value < 0.5f`）。表示メッセージもこれを使う
  - 換算は勝者の演出予算と共通 (`secondsPerAlternation = ToWhiteSeconds() / whiteReachAlternations`)
  - `FlyThenFallLoserAsync(loser, minPower * secondsPerAlternation)` — 上昇 → パワー切れまで滞空 → 落下
    - 滞空は `UniTask.WhenAny(パワー分の Delay, _loserFlyCut.Task)`。`StartWinnerEffect` が
      `_loserFlyCut.TrySetResult()` するので、勝利演出の予約で滞空を打ち切って落下に入る
    - 落下開始時に `_shrinkChar*` から自分を外し (縮小と落下の競合防止)、`_loserFallTime` を記録
  - `StartWinnerEffect` → `RunWinnerEffectAsync`（`_winnerCts` = round トークン由来、二重起動なし）
    1. 敗者の落下開始 (`_loserFell`) を待ち、`_loserFallTime` から最低秒数まで待機
    2. `_winnerEffectStarted.TrySetResult()`（Result の基準）+ スプライトアニメ再生
    3. `ScaleWinnerCharAsync`（`winnerEffectEndScale` / `winnerEffectScaleDuration`、一度だけ）を `Forget()` し、
       `MoveWinnerCharAsync`（`winnerEffectMoveDistance` / `winnerEffectMoveDuration` の sin 往復）を回し続ける
  - `PlayWinnerEffectAsync` は `_winnerEffectStarted` を待ってから `resultDelayAfterWinnerEffect` 秒後に Result を出す

### 7.4.2 背景シーケンスの順序 (現行)
1. 地面: 少し上昇 (`groundRiseDuration`)
2. 地面: `groundEndScale` まで縮小 (`groundShrinkDuration`)
3. 地面: 連打数ぶん下降して消える (`groundDescendDuration`)
4. 星 + 飛んでいるキャラ: `starsEndScale` / `charEndScale` まで縮小 (`starsShrinkDuration`)
   - 完了後、星だけ続けて **最終 ScaleDown (`starsFadeOutEndScale` = 0.2) + FadeOut**
     (`starsFadeOutDuration`。地球の演出とは独立した尺)
   - 4 の一連は `ShrinkStarsThenFadeOutAsync` を `Forget()` して並行実行し、シーケンス本体は 5 の開始時刻まで待つ
5. 地球: 下から競り上がる (`earthRiseDuration`)。開始は **星の縮小が終わる `earthRiseLeadBeforeStarsEnd` 秒前**
   (既定 1 秒。星の縮小尺でクランプ) なので、星の最終 FadeOut と地球の競り上がりが重なる
6. 地球: `earthEndScale` まで縮小 (`earthShrinkDuration`)
7. 待機 (`earthHoldBeforeWhite`)
8. 白 FadeIn (`whiteFadeDuration`)
9. `winnerEffectDelayAfterWhite` (既定 1 秒) 待って勝利演出を予約 — この時点で敗者はパワーが残っていても落下する。
   勝利演出 (スケール変更 + 真横の往復) は落下開始から `winnerEffectMinDelayAfterLoserFall` (既定 1 秒) 後に始まり、
   その 1.5 秒後 (`resultDelayAfterWinnerEffect`) に Result が出る

- 星の FadeOut は `starsGroup` があればそれ、無ければ `starsRect` の Graphic の alpha を直接操作する
- 星の Z 回転 (`Update`) は演出停止でも止めない。停止時は「その時の見た目で固定」

### 7.5 Object の挙動
- **要件**: スクロール停止時に object も停止、その後 上下に少し揺れる
- **実装**: `MashRaceFlyObject.cs` (別クラス)
  - `MoveDown(distance)` — スクロール中に呼ばれる
  - `StartSway()` — スクロール終了時に呼ばれ、以降 sine wave で上下揺れ
  - 各 object は独立位相 (`Random.value * 2π`) で同期を防止

### 7.6 SerializeField Prefab パターン
- **要件**: AddComponent はやめて SerializeField Prefab で保持
- **実装**: `MashRaceGame.flyObjectPrefab`、`SpawnObject` は `Instantiate(flyObjectPrefab, layer)` のみ

### 7.7 背景の視覚
- **要件**: 背景は1色ベタ
- **実装**: `P1Base` / `P2Base`（1色固定）+ `P1Background` / `P2Background`（スクロール層、パターン装飾）+ `P1ObjectLayer` / `P2ObjectLayer`（動的生成 object）

### 7.8 画面はみ出し防止
- **実装**: 各 Player エリアに RectMask2D を付与

---

### 7.9 変更履歴（この機能特有）
- 初期案: メートル単位の数値カウントアップ演出
- → キャラが実際に上に飛ぶ演出
- → キャラは cruise 固定、背景がスクロール
- → 背景 max 到達で停止、Object が代替スクロール
- → Object は停止時に上下 sway

---

## 8. ミニゲーム #3: HopscotchRaceGame

### 概要
```
Idle → Opening (けんけんぱデモ3マス → Ready → Go) → Playing (残りマスをA/Bキーで進む)
     → Goal → Winner → WaitForExit → Exiting → Select
```
- 進む: 正しいキー → 0.4秒でマーカー移動
- 失敗: 0.3秒ロック
- トグル: 1秒周期で jumpable/unjumpable、間隔最小3マス

### 8.1 ゲームルール
- **要件**: けんけんぱ、A/B パターンのマスを正しいキーで進む、失敗すると 1 秒停止 (8.12)、移動 0.4 秒/マス
- **実装**: `HopscotchRaceGame.cs`

### 8.2 レイアウト
- **要件**: 見た目は左斜め上から右斜め下へスクロール、縦割り (上下、後に「横2:縦1」比率)
- **実装**:
  - 上下分割 (P1 上、P2 下)
  - `cellOffset = (30, -15)` で 2:1 比率の対角配置

### 8.3 セル
- **要件**: 3種 (A/B) + トグル属性、30 マス自動生成、トグル連続禁止 (3マス間隔)
- **実装**:
  - `GenerateCourse` で `cellsSinceToggle >= toggleMinSpacing (3)` 条件
  - `cellCount = 30` 固定

### 8.4 トグルの周期切替
- **要件**: 1秒ごとに jumpable / unjumpable 切替
- **実装**: `IsToggleOn() = ((int)(_playElapsed / toggleInterval)) % 2 == 0`
- 視覚的に緑/赤で表示 (`HopscotchCell.SetToggleState`)

### 8.5 入力
- **要件**: P1: A/D、P2: (2P想定なので) ←/→
- **実装**: 該当

### 8.6 コース共有＋別インスタンス
- **要件**: 1P/2P とも同じレース、マス自体は別
- **実装**: `_course` は共有、`_p1Cells` / `_p2Cells` は別 GameObject リスト
- コースは `GenerateCourse` で 1 本だけランダム生成し、両トラックに同じ内容を当てる

### 8.7 HopscotchCell 抜き出し
- **要件**: SerializeField Prefab パターン
- **実装**: `HopscotchCell.cs` (Setup / SetToggleState)、`cellPrefab` SerializeField

### 8.8 勝敗
- **要件**: 先にゴールした方が勝ち
- **実装**: `MoveAsync` 完了時に `position >= cellCount - 1` チェック、`_goalSignal.TrySetResult()`

### 8.9 マスの見た目 (わっか画像＋色)
- **要件**:
  - わっか画像は 5 種 (`Assets/Texture/Hopscotch/wakka01-05`) からマスごとにランダム
  - 色は 3 種 (きいろ `#f6eb69` / あか `#d6484e` / あお `#4b4bed`)、同色が連続しない
  - 開始色を決めたら、その色から `きいろ→あか→あお` の順でループ
    (1P きいろ始まり → きいろ/あか/あお…、2P あか始まり → あか/あお/きいろ…)
- **実装**:
  - `cellSprites` / `cellColors` SerializeField
  - `CellData.spriteIndex` はコース生成時に決定 → 1P/2P 共有
  - 開始色のみプレイヤーごとにランダム (`_p1ColorStart` / `_p2ColorStart`)、
    色 = `cellColors[(colorStart + courseIndex) % 3]` でループするため隣接マスは必ず別色
  - `HopscotchCell.Setup(isTypeA, isToggle, sprite, color)` が `background` /
    `secondaryImage` に反映 (けん = わっか 1 つ、ぱ = わっか 2 つ)

### 8.10 開始演出 (けんけんぱデモ → Ready/Go)
- **要件**:
  - レースの頭に けんけんぱ が 1 セットあり、そこを自動で 3 マス飛ぶ演出から始まる
  - 飛ぶ前の位置 (start cell) は **ぱ**。自キャラも ぱ の画像 (アニメーションの最終フレーム) で待機
    → 見た目の並びは `ぱ → けん → けん → ぱ`
  - デモは 1P/2P 同時。飛んだ 3 マスは本番コースの頭 3 マスそのもの (position 2 から本番開始)
  - デモの後に MashRace と同じ Ready → Go 演出を出す
- **実装**:
  - `IntroPattern = { A, A, B }` (けん・けん・ぱ) を `GenerateCourse` が頭 3 マスに固定生成。
    このマスにはトグルを置かない
  - `CreateStartCell` が `Setup(isTypeA: false, ...)` で start cell を ぱ にする。
    色は `GetCellColor(colorStart, -1)` (負数でも巡回する剰余)
  - キャラは けん / ぱ で **SpriteAnimation を 2 つ**持つ (`player1KenAnim` / `player1PaAnim`、2P も同様)。
    `ShowCellAnim(player, courseIndex, play)` が飛び先のマス種別で使う方だけ `SetActive(true)` にして
    `Play()`、もう片方は隠す。`play: false` は最終フレームで静止 (着地して待機している状態)。
    start cell (-1) は ぱ 扱いなので、開始時は ぱ アニメの最終フレームで待機する
  - `PlayKenKenPaDemoAsync` が `UniTask.WhenAll` で 1P/2P 同時に `JumpAsync` を 3 回
  - `JumpAsync(player, state, targetIndex, duration, token)` を本番の `MoveAsync` と共有 (尺だけ差し替え)。
    ゴール判定は `MoveAsync` 側にのみ持たせ、デモでは発火しない
  - Ready/Go は MashRace と同じ実装 (`PlayReadyAsync` = ScaleDown + FadeIn、
    `PlayGoAsync` = ScaleUp + 途中から FadeOut、Go はプレイと並行)
  - 旧 `introText` ("READY?") / `countdownText` (3-2-1-GO) は廃止

### 8.11 Play Again (再戦)
- **要件**:
  - Play Again でも初回開始時とまったく同じ状態から始まる
  - リザルトから間を置かず始まらないよう、QuickDraw と同じく 1 秒待ってから開始演出に入る
- **実装**:
  - `StartRound(replay: true)` → `RunRoundAsync(token, replay)` → `PlayOpeningAsync(token, replay)`。
    明転待ちの直後に `replayDelay` (既定 1 秒) を挟む
  - `ResetRoundView` が Ready/Go の alpha・scale、キャラの足元位置、
    `CanvasGroupBlinker.Restore()` (ミス明滅で透明のまま残るのを防ぐ) を初期状態に戻す
  - `ResetPlayerStates` が position (-1)・`_winner`・`_playElapsed` とキャラの ぱ 姿勢を戻す
  - コース・マスは `GenerateCourse` / `SpawnCells` で毎ラウンド作り直し (再戦ごとに別コース)

### 8.12 失敗演出 (飛び上がり + 左右に傾く)
- **要件**: 間違えたら 1 秒くらいかけて、左右左右と傾きながら飛び上がる
- **実装**: `StopAsync` → `PlayMissJumpAsync(player, missLockDuration, token)`
  - `missLockDuration` (既定 1 秒) = 演出尺 = そのプレイヤーの入力ロック時間
  - 飛び上がり: 尺いっぱいで `missJumpHeight * Sin(t * π)` の山なり 1 回
  - 傾き: 尺を `missTiltCount` (既定 4 = 左右左右) 等分して `±missTiltAngle` を交互に当てる
  - マスは進まないので `RefreshCells` は触らず、キャラの `anchoredPosition` / `localRotation` だけ動かす。
    `finally` で必ず足元・home 回転に戻す
  - 明滅は `missBlinkDuration` (既定 0.3 秒) に分離。0 にすれば明滅なし

### 8.13 待機のたてゆれ (Y スケール)
- **要件**: 飛んでいない状態が 0.5 秒続いたら待機とみなし、Y スケールを 0.9 ↔ 1 で繰り返す。
  飛んでいる間は 1
- **実装**: `Update` → `UpdateIdleScale(state, character, homeScale)` (1P/2P それぞれ)
  - `PlayerState.idleElapsed` が「飛んでいない秒数」。`isMoving` (マス移動) または
    `isStopped` (失敗の飛び上がり) の間は 0 にリセットし、scale を home に戻す
  - `idleScaleDelay` (既定 0.5 秒) を超えたらそこから開始。
    `y = Lerp(idleScaleMinY, 1, (1 + cos(2πt)) / 2)` なので開始時点がちょうど 1 倍、
    そこから縮んで戻るのを `idleScalePeriod` (既定 0.6 秒) 周期で繰り返す
  - X / Z は home のまま。scale をかける対象は `player1Character` (子の Ken/Pa アニメも一緒に伸縮する)

---

---

## 9. ミニゲーム #4: BlockDropGame

### 概要
```
Idle → Intro(1.2s) → Countdown(3-2-1-GO) → Playing (30ブロックを叩き落とす)
     → Winner → WaitForExit → Exiting → Select
```
- 移動: A/D (P1) または ←/→ (P2)、1フレームで左右を切替
- 叩く: W/S (P1) または ↑/↓ (P2)
- ペナルティ: 同サイドの棒付きブロック叩き → 0.5秒待機

### 9.1 ゲームルール
- **要件**: だるま落とし、A/D で左右移動、W/S でノック
- **実装**: `BlockDropGame.cs`

### 9.2 レイアウト
- **要件**: 縦割り、中央線あり
- **実装**: `CreateHalfAreaVertical` + 中央 Divider (縦線)

### 9.3 ブロック
- **要件**: 3種 (Normal / StickRight / StickLeft)、30 個
- **実装**: `BlockDropBlock.BlockType` enum、`blockCount = 30`

### 9.4 ペナルティ
- **要件**: 棒があるものと同じサイドで叩くと 0.5 秒待機
- **実装**: `penalty` 判定 → `UniTask.Delay(penaltyDuration)`

### 9.5 移動時間 / 落下時間
- **要件**: 左右移動 1F、次段の落下 1F (どちらも補間なしで即着地)
- **実装**: `MoveAsync` / `BlockDropBlock.DropAsync` が座標を即時セットして 1 フレーム待つだけ

### 9.6 ノック方向
- **要件**: 左から叩くと右へ、逆もしかり
- **実装**: `flyToRight = state.side == PlayerSide.Left`

### 9.7 入力
- **要件**: P1: A/D + W/S、P2: (2P想定なので) ←/→ + ↑/↓
- **実装**: 該当

### 9.8 BlockDropBlock 抜き出し
- **要件**: SerializeField Prefab パターン
- **実装**: `BlockDropBlock.cs` (FlyAwayAsync / DropAsync)、`blockPrefab` SerializeField

---

---

## 10. 各ミニゲーム共通パターン

### 10.1 演出フロー
```
Idle → Intro → Countdown (該当時) → Playing → Winner → WaitForExit → Exiting → Select 遷移
```

### 10.2 Round 管理
- `StartRound()` → 前ラウンド `CancelRound()` → `RunRoundAsync().Forget()`
- `_roundCts` (CancellationTokenSource) で中断可能

### 10.3 InputAction 管理
- SetupAsync で作成
- OnPanelInAsync で Enable
- OnPanelOutAsync で Disable
- OnDestroy で Dispose

### 10.4 Play Again ボタン (廃止)
- リザルトの再戦は入力のみ (10.5) で行う。各ミニゲームの prefab にあった Play Again UI ボタンと
  `playAgainButton` / `OnPlayAgain` は削除済み

### 10.5 リザルトの再戦 / 戻る (`ResultInput.cs`)
- **要件**: 全ミニゲーム共通で、完了後に **A で再戦 / B で戻る**
  (キーボードは **Enter で再戦 / X で戻る**)。操作できるのは **1P のみ**
- **実装**: `ResultInput` を各ゲームが 1 つ持ち、`WaitForExitPressAsync` の間だけ Enable する
  - 再戦 = `<Gamepad>/buttonSouth` / `<Keyboard>/enter` / `<Keyboard>/numpadEnter`
    → `StartRound(replay: true)` (SE: Decide)
  - 戻る = `<Gamepad>/buttonEast` / `<Keyboard>/x`
    → `_exitSignal` を立てて `PlayExitEffectAsync` → `ScreenController.ShowAsync(Select)` (SE: Cancel)
  - 1P 判定は `PlayerDevices.IsPlayerOne`。2P のパッドは無視する
- **注意**: リザルトでは `ClearFocus()` で UI 選択を外す。選択が残っていると EventSystem の Submit
  (どのパッドの A でも飛ぶ) が走ってしまい 1P 限定が崩れる
- キーボードの Esc / Backspace は `MiniGamePanel` の Back が拾う (`ResultInput` には入れない。二重遷移を防ぐ)

### 10.6 1P/2P のデバイス振り分け (`PlayerDevices.cs`)
- **要件**: 同じボタン (A/B) を 1P/2P 両方にバインドしつつ、押したパッドで持ち主を決める
- **実装**: キーボードは 1P/2P 両方に有効 (キーで分離)。Gamepad は接続順 `Gamepad.all[0]` = 1P、`[1]` = 2P
- 各ゲームは `PlayerInputAction.Make` / `OnPress` でアクションを作る。`OnPress` の中で
  押下エッジの判定と `PlayerDevices.IsForPlayer(device, player)` を通す
- **注意**: 接続順ベースなので、1P のパッドを抜き差しすると 2P のパッドが 1P に繰り上がる

### 10.6.1 なぜ PassThrough なのか (`PlayerInputAction.cs`)
- **背景**: `<Gamepad>/...` は接続中の**全パッド**に解決されるので、1P/2P に同じパスを張ると
  1 つのアクションに複数の control がぶら下がる。`InputActionType.Button` はこの状態で
  Input System の衝突解決 (conflict resolution) が有効になり、
  「今アクションを駆動している control より強く押されたか」しか通さなくなる
- **症状**: ボタンの actuation はどちらも 1.0 なので、2P が押している最中の 1P の押下は
  「アクションの状態が変わらない」と判断されて**捨てられる**。2 人が同じボタンを叩く
  連打 / 早押しで片方の入力が恒常的に消える
- **実装**: `PlayerInputAction.Make` は `InputActionType.PassThrough` でアクションを作る。
  PassThrough は衝突解決を行わず、control ごとに独立して `performed` が飛ぶ
- **注意**: PassThrough は値が変わるたびに `performed` が来る (離したときも来る) ので、
  押した瞬間だけを取り出すのは `PlayerInputAction.OnPress` の責務。
  生の `performed` を直接購読しないこと

### 10.7 パッド操作の割り当て
| ゲーム | 操作 | キーボード (1P / 2P) | Gamepad |
| --- | --- | --- | --- |
| QuickDraw | 抜刀 | `A` / `L` | A |
| MashRace | 交互連打 | `A`,`D` / `←`,`→` | A, B |
| HopscotchRace | けん / ぱ | `A`,`D` / `←`,`→` | A, B |
| BlockDrop | 左右移動 | `A`,`D` / `←`,`→` | 十字キー, 左スティック |
| BlockDrop | 叩く | `W`,`S` / `↑`,`↓` | A, B |
| 全ゲーム (リザルト) | 再戦 / 戻る | `Enter` / `X` (1P のみ) | A / B |

---

## 11. Editor Tooling (`PrefabGenerator.cs`)

### 11.1 メニュー
- `VocaNerd > Generate Sample Prefabs`

### 11.2 生成対象 Prefab
- MainCanvas / BlackFadeOverlay / AudioManager（+ `Assets/Data/AudioLibrary.asset`）
- TitlePanel / SelectPanel / ExplainPanel / MiniGamePanel
- QuickDrawGame / MashRaceGame / HopscotchRaceGame / BlockDropGame
- MashRaceFlyObject / HopscotchCell / BlockDropBlock

### 11.3 責務
- Prefab 内部の GameObject/Component 構造の組み立て（`new GameObject` / `AddComponent` は Editor コードのみ許容）
- `SerializedObject` で Inspector 参照を明示的に配線（`AssignField` / `AssignArray`）
- ランタイムコードでは `Instantiate(prefab)` のみ使用（AddComponent 廃止）

---

## 12. Unity 側手動セットアップ

`Assets/Scripts/SETUP.md` 参照

1. `VocaNerd > Generate Sample Prefabs` を実行
2. Scene に `MainCanvas.prefab` / `BlackFadeOverlay.prefab` / `AudioManager.prefab` をドラッグ
3. ミニゲームごとに説明画面 prefab を作成し、`miniGamePrefab` / `bgmKey` / 動画をアサイン
4. `SelectPanel.prefab` の `Explain Panel Prefabs` 配列に 4 つの Explain prefab をアサイン（0=左上 1=右上 2=左下 3=右下）
5. `ScreenController` (`AspectFrame` にアタッチ済み) の Screens に Title / Select / MiniGame プレハブを設定

---

## 変更履歴・大きな設計変更

| 段階 | 変更内容 |
|---|---|
| 初期 | Panel は常駐 + CanvasGroup フェード方式 |
| Destroy 方式化 | Prefab を Instantiate/Destroy に変更 |
| 説明画面のデータ | ミニゲームごとの Explain prefab（旧 MiniGameData は廃止）|
| PrefabGenerator 導入 | Editor 拡張で Prefab を自動生成 |
| PanelBase 抽出 | 共通ライフサイクル基底クラス |
| BlackFadeOverlay | 遷移用黒フェード singleton |
| AudioManager | BGM/SE 管理 singleton |
| 4:3 レターボックス | MainCanvas に AspectRatioFitter |
| ExplainPanel モーダル化 | Screen ではなく SelectPanel 子に |
| SerializeField Prefab 統一 | ランタイム AddComponent 全廃 |
| ScreenController の Transition 抽象化 | TransitionIn/OutAsync 抜き出し |
| サウンドを string キー化 | AudioLibrary(SO) + AudioKeys 定数 + Audio スタティック窓口。UI SE / 画面 BGM を自動化、ボリュームを SaveData 永続化 |
| Panel In/Out を Transition 外に | 見える演出と隠す演出を分離 |
