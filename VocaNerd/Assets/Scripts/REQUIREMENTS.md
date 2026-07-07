# VocaNerd 要件一覧

このドキュメントは会話中で決めた要件と、それぞれがどこに実装されているかをまとめたものです。

---

## 0. プロジェクト全体

### 0.1 ゲームフロー
- **要件**: タイトル → 4 つのミニゲーム選択画面 → 画像タップで説明＋動画再生 → Play ボタンでミニゲーム開始
- **実装**:
  - `TitlePanel.cs` → `SelectPanel.cs` → `ExplainPanel.cs`（モーダル）→ `MiniGamePanel.cs`
  - `ScreenType`: Title / Select / MiniGame（Explain はモーダル化で ScreenType から除外）

### 0.2 UniTask 使用
- **要件**: UniTask を使用したい
- **実装**: `Packages/manifest.json` に `com.cysharp.unitask` を UPM 経由で追加

### 0.3 説明画面のメディア
- **要件**: .mp4 動画を再生
- **実装**: `ExplainPanel.SetupAsync` で `VideoPlayer.Prepare` → `Play`

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
- **実装**: `SelectPanel.OpenExplainAsync` で `Instantiate(explainPanelPrefab, explainRoot)`、Back で `PanelOutAsync + Destroy(self)`

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

## 4. AudioManager (`AudioManager.cs`)

### 4.1 BGM
- **要件**: BGM とその再生機構
- **実装**: `PlayBgmAsync(clip, fadeDuration)` / `StopBgmAsync(fadeDuration)` を提供、AudioSource×2 を使ってクロスフェード

### 4.2 SE
- **要件**: SE 再生機構
- **実装**: `PlaySE(clip, volumeScale)` で `PlayOneShot`、`PlaySEAt` で 3D 再生

### 4.3 ボリューム
- **実装**: MasterVolume / BgmVolume / SeVolume を 0-1 で保持、即時反映

---

## 5. MiniGameData (`MiniGameData.cs`)

- **要件**: ScriptableObject でミニゲーム情報を保持、prefab 参照を含む
- **実装**: `[CreateAssetMenu(menuName = "VocaNerd/MiniGameData")]`
  - Title / Description / Thumbnail / VideoClip / MiniGamePrefab

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
Idle → Intro(1.2s) → Countdown(3-2-1-GO) → Playing(10秒)
     → Result (キャラ上昇 → 背景スクロール → object 生成 → 背景停止 → object 継続 → キャラ落下)
     → Winner → WaitForExit → Exiting → Select
```

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
Idle → Intro(1.2s) → Countdown(3-2-1-GO) → Playing (30マスをA/Bキーで進む)
     → Goal → Winner → WaitForExit → Exiting → Select
```
- 進む: 正しいキー → 0.4秒でマーカー移動
- 失敗: 0.3秒ロック
- トグル: 1秒周期で jumpable/unjumpable、間隔最小3マス

### 8.1 ゲームルール
- **要件**: けんけんぱ、A/B パターンのマスを正しいキーで進む、失敗すると 0.3 秒停止、移動 0.4 秒/マス
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

### 8.7 HopscotchCell 抜き出し
- **要件**: SerializeField Prefab パターン
- **実装**: `HopscotchCell.cs` (Setup / SetToggleState)、`cellPrefab` SerializeField

### 8.8 勝敗
- **要件**: 先にゴールした方が勝ち
- **実装**: `MoveAsync` 完了時に `position >= cellCount - 1` チェック、`_goalSignal.TrySetResult()`

---

---

## 9. ミニゲーム #4: BlockDropGame

### 概要
```
Idle → Intro(1.2s) → Countdown(3-2-1-GO) → Playing (30ブロックを叩き落とす)
     → Winner → WaitForExit → Exiting → Select
```
- 移動: A/D (P1) または ←/→ (P2)、0.1秒で左右を切替
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

### 9.5 移動時間
- **要件**: 左右移動 0.1 秒
- **実装**: `moveDuration = 0.1f`

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

### 10.4 Play Again ボタン
- 各ミニゲーム内に Play Again UI ボタン、`StartRound` を再呼び出し

---

## 11. Editor Tooling (`PrefabGenerator.cs`)

### 11.1 メニュー
- `VocaNerd > Generate Sample Prefabs`

### 11.2 生成対象 Prefab
- MainCanvas / BlackFadeOverlay / AudioManager
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
3. `MiniGameData` ScriptableObject を 4 つ作成、各ミニゲーム Prefab をアサイン
4. `SelectPanel.prefab` の `Mini Games` 配列に 4 つの ScriptableObject をアサイン
5. `ScreenController` (`AspectFrame` にアタッチ済み) の Screens に Title / Select / MiniGame プレハブを設定

---

## 変更履歴・大きな設計変更

| 段階 | 変更内容 |
|---|---|
| 初期 | Panel は常駐 + CanvasGroup フェード方式 |
| Destroy 方式化 | Prefab を Instantiate/Destroy に変更 |
| ScriptableObject 導入 | MiniGameData 作成 |
| PrefabGenerator 導入 | Editor 拡張で Prefab を自動生成 |
| PanelBase 抽出 | 共通ライフサイクル基底クラス |
| BlackFadeOverlay | 遷移用黒フェード singleton |
| AudioManager | BGM/SE 管理 singleton |
| 4:3 レターボックス | MainCanvas に AspectRatioFitter |
| ExplainPanel モーダル化 | Screen ではなく SelectPanel 子に |
| SerializeField Prefab 統一 | ランタイム AddComponent 全廃 |
| ScreenController の Transition 抽象化 | TransitionIn/OutAsync 抜き出し |
| Panel In/Out を Transition 外に | 見える演出と隠す演出を分離 |
