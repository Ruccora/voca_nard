# VocaNerd 開発セッションログ

Claude Code を使った VocaNerd プロジェクトの開発作業を、実装順にまとめたログ。要件と実装対応は `Assets/Scripts/REQUIREMENTS.md`、Unity 側の手順は `Assets/Scripts/SETUP.md` を参照。

---

## プロジェクト概要

- **プロジェクト**: VocaNerd (Unity 2D URP)
- **内容**: タイトル → ミニゲーム選択 → 各ミニゲームプレイ の 2人ローカルゲーム
- **ミニゲーム 4種**: QuickDraw / MashRace / HopscotchRace / BlockDrop
- **主要スタック**: Unity 6.0 / URP / UniTask / TextMeshPro / InputSystem
- **画面比**: 4:3 レターボックス (1600×1200)

---

## Phase 1: ゲームフレームワーク基盤

**目的**: タイトル → 選択 → 説明 → ミニゲーム の遷移フローと共通基盤を作る

**実装:**
- **UniTask 導入** (`Packages/manifest.json` に `com.cysharp.unitask`)
- **単一シーン + Panel 切替方式** (Prefab を Instantiate/Destroy)
- **`ScreenController.cs`** — Prefab の生成・破棄で画面遷移、シングルトン
- **`MiniGameData.cs`** (ScriptableObject) — Title/Description/Thumbnail/VideoClip/MiniGamePrefab 保持
- **`PanelBase.cs`** — 全画面パネルの基底クラス
  - `SetupAsync` / `PanelInAsync` / `PanelOutAsync` (`OnPanelInAsync`/`OnPanelOutAsync` を virtual で override 可能)
  - `IsAnimating` フラグでボタンタップ多重発火を防止
  - CanvasGroup 経由の alpha フェード
- **4 つの Panel クラス**: `TitlePanel` / `SelectPanel` / `ExplainPanel` / `MiniGamePanel`
- **動画再生機構** (`ExplainPanel`): VideoPlayer + RenderTexture + RawImage で .mp4 再生
- **REQUIREMENTS.md** / **SETUP.md** ドキュメント作成

**設計判断:**
- Timeline より **UniTask ベース** を採用（拡張性・コード管理優先）
- Panel は常駐ではなく **Instantiate/Destroy** 方式 (メモリ効率 & 状態リセット容易)

---

## Phase 2: PrefabGenerator (Editor 拡張)

**目的**: 手作業の Prefab 構築を廃止し、コードから一括生成

**実装:**
- **`Assets/Editor/PrefabGenerator.cs`** — Editor 拡張
- メニュー `VocaNerd/Generate/*` で個別 or 一括生成
- `SerializedObject` で参照フィールドを自動配線 (`AssignField` / `AssignArray`)
- 対象: MainCanvas / TitlePanel / SelectPanel / ExplainPanel / MiniGamePanel / QuickDrawGame / MashRaceGame / HopscotchRaceGame / BlockDropGame / MashRaceFlyObject / HopscotchCell / HopscotchStartCell / BlockDropBlock / BlackFadeOverlay / AudioManager

**設計判断:**
- 手書き YAML .prefab は GUID 管理が壊れやすいので、**Editor スクリプト → Unity API 経由**の生成に統一
- ランタイムコードでは `AddComponent` / `new GameObject` を排除、**すべて SerializeField Prefab 参照**

**後で分離** (Phase 12):
- `PrefabGenerator.cs` (Panel 側) と `PrefabGenerator.Cells.cs` (Cell 側) に partial class で分割

---

## Phase 3: ミニゲーム #1 QuickDrawGame (早撃ち反射)

**ルール**: 開始演出 → 3〜5秒ランダム待機 → 画像表示 → 先押しで勝ち。Waiting 中の押下はフォール (即敗北)

**演出フロー (7段階):**
```
Idle → Intro → Waiting(3~5s) → Ready → Reaction → Winner → WaitForExit → Exiting → Select遷移
        ↘ (押下でフォール) ↗
```

**入力:**
- P1: `A` / GamepadSouth
- P2: `L` / GamepadEast (後に East → South に統一、2 gamepad 対応)

**主要フィールド:**
- 5 段階演出メソッドを別関数化 (`PlayIntroEffectAsync` / `PlayWaitAsync` / `PlayRevealEffectAsync` / `PlayPressEffectAsync` / `PlayWinnerEffectAsync`)
- `UniTaskCompletionSource` で押下/exit signal を管理
- Panel と同等のライフサイクル (Unity 依存廃止)

**UI:**
- 左上/右上に P1/P2 の色分けキーラベル (青/赤)

---

## Phase 4: ミニゲーム #2 MashRaceGame (交互連打)

**ルール**: 10秒間、L/R を交互に押して連打数を競う。同方向連打はミス + 0.2秒ロック

**演出フロー:**
```
Idle → Intro → Countdown(3-2-1-GO) → Playing(10s)
     → Result (キャラ上昇 → 背景スクロール → object 生成 → 背景停止 → object 継続 → キャラ落下)
     → Winner → WaitForExit → Exiting → Select
```

**入力:**
- P1: `A`/`D`
- P2: `←`/`→`

**視覚設計の変遷:**
1. **初期**: 縦割り (P1上/P2下)、距離テキスト
2. → **横並び (縦割り左右)** に変更、飛行アイコン
3. → **距離テキスト削除**、上に飛ぶアイコン
4. → **キャラは cruise 高で固定、背景スクロール** で飛行感
5. → **背景 max 到達で停止、object のみ継続スクロール**
6. → **オブジェクト停止時に上下 sway** (`MashRaceFlyObject.StartSway`)

**MashRaceFlyObject** (別クラス):
- `MoveDown(distance)` / `StartSway()`
- Prefab 参照 (`flyObjectPrefab`) で Instantiate、AddComponent 排除
- 各オブジェクトは独立位相 (`Random.value * 2π`) で同期を防止

---

## Phase 5: ミニゲーム #3 HopscotchRaceGame (けんけんぱ)

**ルール**: 30 マスの A/B/Toggle セルを正しいキーで進む。失敗は 0.3秒ロック、移動 0.4秒/マス

**演出フロー:**
```
Idle → Intro → Countdown → Playing (30マス) → Goal → Winner → WaitForExit → Exiting → Select
```

**入力:**
- P1: `A` (A-pattern) / `D` (B-pattern)
- P2: `←` / `→`

**セル生成:**
- 30 マス自動生成、A/B 型ランダム、20% トグルセル
- トグル間隔最小 3 マス (連続禁止)
- 1 秒周期で jumpable/unjumpable 切替

**視覚設計の大改修:**
1. **初期**: 対角レイアウト、マーカーがセル間を移動
2. → **視覚を大幅変更**: キャラは右側固定、Cell が奥から流れてくる perspective 表示
3. → **後方 cell を消す判定緩和**: 2 マス手前まで残す (`visibleBehind = 2`)
4. → **通過セルは scale > 1** (画面手前へ迫るように見せる)
5. → **次のジャンプ対象は「1つ前」の位置** に配置、スタート地点にプレースホルダー足場
6. → **キャラ足元にセル配置** (`feetOffset` プロパティ)
7. → **失敗時キャラ Blink** (CanvasGroupBlinker)
8. → **移動成功時キャラ Jump アニメ** (sine, 移動時間と同期)
9. → **スタート足場を別 Prefab (`HopscotchStartCell`)** に分離、`_p1Cells[0]` として組み込み → 通常のセルと同様にスクロール
10. → **画面を縦割り+右上から左下に流れる方向** に変更

**HopscotchCell** (Prefab):
- Primary Platform + Label (A/B) + Toggle Mark
- Secondary Platform (B-type のみ活性化、side-by-side)
- Secondary Toggle Mark も対応

---

## Phase 6: ミニゲーム #4 BlockDropGame (だるま落とし)

**ルール**: 30 ブロックを左右移動 (A/D) + ノック (W/S) で全消去、先着勝ち

**演出フロー:**
```
Idle → Intro → Countdown → Playing → Winner → WaitForExit → Exiting → Select
```

**入力:**
- P1: `A`/`D` 移動 + `W`/`S` ノック
- P2: `←`/`→` 移動 + `↑`/`↓` ノック

**ブロック仕様:**
- 3 種類: Normal / StickLeft / StickRight
- 同サイドで棒付きを叩くと 0.5秒ペナルティ
- 左から叩くと右へ飛ぶ (逆も然り)
- 移動 0.1秒
- 飛行距離: 900px (棒がマスク外に確実に出るよう調整)

**BlockDropBlock** (別 Prefab):
- Body Image + LeftStick + RightStick GameObject 子
- `Setup(type)` で棒の可視制御
- `FlyAwayAsync` / `DropAsync` (UniTask)

---

## Phase 7: 共通機構

### BlackFadeOverlay (`BlackFadeOverlay.cs`)
- Scene 配置のシングルトン (Prefab 経由、DontDestroyOnLoad 廃止)
- `FadeInAsync(duration)` / `FadeOutAsync(duration)`
- FadeIn は `blocksRaycasts = true` (入力ブロック)、FadeOut は false
- `sortingOrder = short.MaxValue` で常に最前面

### AudioManager (`AudioManager.cs`)
- Scene 配置のシングルトン
- BGM: 2 つの AudioSource でクロスフェード (`PlayBgmAsync` / `StopBgmAsync`)
- SE: PlayOneShot (`PlaySE(clip, volumeScale)`)
- MasterVolume / BgmVolume / SeVolume で個別音量制御

### CanvasGroupBlinker (`CanvasGroupBlinker.cs`)
- 1F 毎に alpha 0/1 を切替するストロボ点滅
- `BlinkAsync(duration)` で秒数指定
- `[RequireComponent(CanvasGroup)]` + `Reset()` で auto-wire

### SpriteAnimation (`SpriteAnimation.cs`)
- Sprite 配列 + frameDuration で切替アニメ
- `Play()` / `Stop()` / `SetFrame(int)` (固定表示)
- Loop / PlayOnAwake / SetFrameDuration / SetSprites

### CursorController (`CursorController.cs`)
- 起動時にマウス非表示 & Locked (キーボード/ゲームパッドのみ操作前提)

### SelectionIndicator (`SelectionIndicator.cs`)
- EventSystem の選択追従で矢印 ▶ を移動
- `Left/Right/Center` 位置切替 (enum)
- `blinker` (`CanvasGroupBlinker`) 参照で Blink 演出

### SelectionKeeper (`SelectionKeeper.cs`)
- 空領域クリックで EventSystem 選択が消える問題を防止
- Update 毎に直前の有効選択を記録・復元

### MainCanvas + 4:3 レターボックス
- CanvasScaler 参照解像度 1600×1200
- AspectRatioFitter (FitInParent, 4/3) で常時 4:3 保持
- LetterboxBackground (全画面黒) で余白を黒帯化
- CursorController + EventSystem + InputSystemUIInputModule + SelectionKeeper を同梱

---

## Phase 8: 画面遷移とモーダル化

### ScreenController の遷移フロー
```
現Panel の PanelOut (見える)
  ↓
TransitionIn (黒フェード)
  ↓
Destroy 旧 & Instantiate 新 (黒画面裏)
  ↓
SetupAsync
  ↓
TransitionOut (黒フェード解除)
  ↓
新Panel の PanelIn (見える)
```

**後に修正**: `PanelOut` と `PanelIn` を **並列実行 (クロスフェード)** に変更、`PanelPreOutAsync` フックも追加

### ExplainPanel モーダル化
- 旧: ScreenType の一つ、Select から遷移
- 新: `SelectPanel` の子として Instantiate (モーダル)
  - `explainPanelPrefab` (SerializeField) を Instantiate
  - `explainRoot` に配置
  - 背景 Scrim (半透明黒) で SelectPanel を暗く見せる
  - Back で自身を Destroy (Select は残る)
  - Play で `ScreenController.ShowAsync(MiniGame)`

### TransitionInAsync / TransitionOutAsync
- `protected virtual` メソッドとして抜き出し
- 現状は `BlackFadeOverlay.Instance.FadeIn/OutAsync` を呼ぶだけ
- 別演出 (スライド・ワイプ等) に差し替え可能

---

## Phase 9: UI ナビゲーション統合

### 全画面キーボード/ゲームパッド操作
- `MainCanvas` の EventSystem に `InputSystemUIInputModule` を追加
- Space/Enter/GamepadSouth で Submit → 選択中の Button 発火
- 矢印/D-pad で Selectable 間ナビゲート

### 各 Panel の defaultSelected
- `PanelBase.defaultSelected` (Selectable, SerializeField)
- `OnPanelInAsync` で `FocusDefaultSelected()` を呼ぶ
- Prefab 配線 (Title=StartButton, Select=MiniGame[0], Explain=PlayButton, MiniGame=BackButton)

### TitlePanel Exit ボタン
- Start + Exit の 2 ボタン
- Exit → `Application.Quit()` (Editor では PlayMode 停止)

### SelectionIndicator の Show/Hide
- デフォルト alpha 0 (非表示)
- `Show()` で表示、`Hide()` で非表示
- Title/SelectPanel の `PanelIn` 完了後に `Show()` を呼ぶ設計

### Back Input
- MiniGamePanel に InputAction 追加 (`Escape` / `Backspace` / `GamepadEast` / `GamepadSelect`)
- 押下で `ShowAsync(Select)` に遷移
- `CanAcceptBack` プロパティで Winner / WaitForExit フェーズのみ受付 (Playing 中は無視)

### PlayAgain フォーカス
- 各ミニゲーム `PlayWinnerEffectAsync` で `SetFocus(playAgainButton)`
- Winner 演出開始と同時に Enter/Space で即リスタート可能

### 2 ゲームパッド対応 (QuickDraw)
- P1/P2 とも `<Gamepad>/buttonSouth` にバインド
- コールバックで `Gamepad.all` のインデックスから player 判別
- Gamepad[0] → P1、Gamepad[1] → P2
- キーボードは 1 台のデバイス制約上、別キー (A / L) を維持

---

## Phase 10: Panel アニメーション基盤

### Rect フィールドの導入
各 Panel に文字/ボタンの `RectTransform` を SerializeField として追加:
- `TitlePanel`: titleLabelRect[] / startButtonRect / exitButtonRect
- `SelectPanel`: headerRect / miniGameButtonRects[4]
- `ExplainPanel`: titleTextRect / descriptionTextRect / videoDisplayRect / playButtonRect / backButtonRect
- `MiniGamePanel`: titleTextRect / backButtonRect

### 実装例
- `TitlePanel.OnPanelOutAsync`: タイトル文字 (Voca/Nerd 2枚) を左右にスライドアウト
- `SelectPanel.OnPanelInAsync`: ヘッダーが上から / ミニゲームボタン 4 枚が左右から slide-in
- `SelectPanel.OnPanelPreOutAsync`: 選択したボタンを画面全体に expand
- `ExplainPanel.OnPanelInAsync`: 説明文が右からスライドイン

### Panel の alpha=0 スタート
- `PanelBase.Awake` で `canvasGroup.alpha = 0` に初期化
- `OnPanelInAsync` の fade で自然に立ち上がる

---

## Phase 11: シェーダー (UI Sparkle → Bloom)

### UISparkle.shader の進化

**v1: 手続き型スパークル (素材不要)**
- Grid × 星型 × Twinkle
- UV.y ベースで下部限定
- セル毎にランダム位相の sin

**v2: マスク対応**
- `_MaskTex` (R チャンネル) で光る領域を精密指定
- Mask Cutoff / Contrast プロパティ
- UV.y ベースは廃止、マスクで完全制御

**v3: 画像色反映**
- `_ColorFromImage` (0-1) で画像色 or fallback をブレンド
- `_ColorBoost` で暗い部分も光量確保

**v4: Bloom 風に変更 (現行)**
- スパークル (Grid × 星型 × Twinkle) 廃止
- マスク領域を画像色で発光させる bloom スタイルに
- 5-tap ブラーでマスク境界を柔らかく
- `_PulseAmount` / `_PulseSpeed` で明滅
- `_PhaseGridSize` で位相を位置ごとにバラつかせ、不規則な明滅

### MaterialGenerator.cs
- `VocaNerd/Create/UI Sparkle Material` メニュー
- `Assets/Materials/UISparkle.mat` を自動生成

### 適用先
- QuickDrawGame の背景 (`back_MIKIRI` + `back_MIKIRI_mask`)
- キラキラ ("MIKIRI = 見切り") 演出

---

## Phase 12: PrefabGenerator 分割

**目的**: `PrefabGenerator.cs` が肥大化 (1000行超) してきたため機能分離

**分離:**
- `PrefabGenerator.cs` — Panel 系 + 共通ヘルパー + メニュー
- `PrefabGenerator.Cells.cs` — Cell 系 (`MashRaceFlyObject` / `HopscotchCell` / `HopscotchStartCell` / `BlockDropBlock`)

**技術:** `public static partial class PrefabGenerator` として同一クラスを分割ファイルに定義

---

## Phase 13: 資産・仕様書

### フォント (日本語対応)
- Noto Sans JP (Google Fonts, SIL Open Font License, 商用可)
- `Assets/Font/` に .ttf + .SDF.asset (Dynamic モード)
- TMP Settings の Fallback に追加 → 全 TMP で日本語表示可能

### 動画
- 各ミニゲーム説明用 .mp4 (block / dance / dowble / race)
- `MiniGameData.videoClip` に紐付け
- ExplainPanel の RawImage + VideoPlayer で再生

### テクスチャ
- QuickDrawGame 用: back_MIKIRI / back_MIKIRI_ball / back_MIKIRI_mask / miku / teto / onpu (MIKIRI 演出用)

### ドキュメント
- `REQUIREMENTS.md` — 全要件と実装対応表
- `SETUP.md` — Unity 側手動セットアップ手順
- `SESSION_LOG.md` — 本ファイル

---

## 主要な設計判断まとめ

| 判断 | 採用 | 理由 |
|---|---|---|
| Panel 管理 | Instantiate/Destroy | メモリ効率、状態リセット容易 |
| 遷移 | UniTask ベース | Timeline より軽量、コードで完結 |
| Prefab 生成 | Editor 拡張 (partial class) | 手書き YAML より安定、GUID 自動管理 |
| ランタイム動的生成 | SerializeField Prefab + `Instantiate` | `AddComponent` を全廃、保守性 UP |
| 画面比 | 4:3 レターボックス | 縦横に安定表示、対応機器を選ばない |
| 遷移演出 | Cross-fade (Panel In/Out 並列) | クリーン、待ち時間短縮 |
| ExplainPanel | Modal (Select の子) | Screen 遷移より軽量、SelectPanel 状態保持 |
| キー入力 | New InputSystem | Legacy より柔軟、ゲームパッド対応容易 |
| フォント | Dynamic SDF | 日本語全字焼き込み不要、事前生成コスト削減 |
| 操作性 | マウス非表示 + キーボード/ゲームパッド | ローカル 2 人プレイ向け、フォーカスベース UI |

---

## Phase 14: セッションログ整備と /exit-log スキル

**日付**: 2026-07-13
**目的**: 開発ログを継続的に残す仕組み化 (作業終了時の自動追記スキル整備) と、ログ配置の適正化。

**実装:**
- 新スキル `.claude/skills/exit-log/SKILL.md` を作成
  - `/exit-log [ヒント]` で起動、セッション内容を自動分析して `SESSION_LOG.md` に新 Phase を追記
  - フロー: パス取得 → git status/diff/log 収集 → Phase 番号決定 → テンプレート組立 → コミット履歴表更新 → Edit で追記 → AskUserQuestion で commit/push 確認
  - 既存の `wt` スキルと同じ SKILL.md フォーマット (frontmatter: name/description/argument-hint/user-invocable/allowed-tools)
- `SESSION_LOG.md` を `Assets/Scripts/` → `.claude/` に移動 (`git mv`, 履歴保持 99%)
  - Unity の Asset tree 外に置くことで .meta 生成不要、Editor import 対象外に
  - Claude Code 関連ファイルを `.claude/` に集約
- 内部相互参照の更新
  - `SESSION_LOG.md` の `REQUIREMENTS.md` / `SETUP.md` 参照 → `Assets/Scripts/` 付きの明示パスに
  - `exit-log/SKILL.md` の `Assets/Scripts/SESSION_LOG.md` 参照 → `.claude/SESSION_LOG.md` に一括置換

**設計判断:**
- ログ配置場所: `.claude/` 採用 — Unity Asset ツリー外に出すことで .meta 管理不要、Claude Code 関連メタデータと集約
- コミット確認: 常に AskUserQuestion 経由 — 「勝手にコミット・push しない」原則を skill レベルに明文化
- Phase 追記方針: 既存 Phase を書き換えず末尾に append — 履歴の破壊を防止

**触ったファイル:**
- `.claude/skills/exit-log/SKILL.md` — 新規作成
- `.claude/SESSION_LOG.md` — 移動 + 相互参照パス更新
- `Assets/Font/NotoSansJP-VariableFont_wght SDF.asset` — Unity 側 SDF atlas 再生成 (副作用)
- `Assets/Material/QuickDraw/Sparkle_ball.mat` — マテリアル再シリアライズ (副作用)
- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` — Unity 側再シリアライズ (副作用)

**コミット:**
- `0f41f3e` 2026-07-13 Add SESSION_LOG.md and per-gamepad routing in QuickDrawGame
- `dd415d7` 2026-07-13 Move SESSION_LOG under .claude, add exit-log skill

**未着手 / 積み残し:**
- `/exit-log` の本番動作検証 (今回はドライラン)
- ログ肥大化対策 (Phase 数増加時の分割戦略)

---

## コミット履歴

| Commit | Date | Summary |
|---|---|---|
| `d83b0b5` | 2026-07-03 | Initial commit: Unity 2D URP project + /wt skill |
| `4ac0da7` | 2026-07-07 | Add VocaNerd game framework and 4 minigames |
| `6e44be9` | 2026-07-08 | Add Hopscotch/Blink/UI navigation code, split PrefabGenerator |
| `6ddcbc0` | 2026-07-08 | Add generated Prefabs and update SampleScene |
| `5ea8bb8` | 2026-07-08 | Add MiniGameData assets, Title textures, RenderTexture, TextMesh Pro essentials |
| `c26eeb2` | 2026-07-08 | Add Noto Sans JP font and SDF asset for Japanese TMP support |
| `4063a09` | 2026-07-08 | Add explain videos: block.mp4, race.mp4 |
| `98a611c` | 2026-07-08 | Add explain video: dowble.mp4 |
| `3ac29aa` | 2026-07-08 | Add explain video: dance.mp4 |
| `75bbd8d` | 2026-07-11 | Add SpriteAnimation, Back input, Enter=PlayAgain focus, QuickDraw textures |
| `a713b3c` | 2026-07-11 | Add UI Sparkle/Bloom shader and MIKIRI mask texture |
| `0f41f3e` | 2026-07-13 | Add SESSION_LOG.md and per-gamepad routing in QuickDrawGame |
| `dd415d7` | 2026-07-13 | Move SESSION_LOG under .claude, add exit-log skill |

---

## 現在の状態 (2026-07-13 時点)

- 全 4 ミニゲームがプレイ可能
- 遷移・操作系がキーボード + 2 ゲームパッド対応
- QuickDrawGame に MIKIRI ボム的な演出用 shader (bloom) 実装済
- HopscotchRaceGame は perspective 表示 + ジャンプ + start platform 統合
- MashRaceGame は複合スクロール + オブジェクト sway
- BlockDropGame は左右移動 + ノック + 棒ペナルティ
- Prefab 生成は Editor メニューで一括再生成可能
- 日本語 TMP 全対応
- 開発ログを `.claude/SESSION_LOG.md` に集約 (Unity Asset 外)
- `/exit-log` スキルで作業終了時に Phase 自動追記可能

## 進行中/未着手

- 各ミニゲーム内演出の追加 (TODO: 効果音、パーティクル等)
- 各ミニゲームの BGM (AudioManager は準備済み、素材未整備)
- HopscotchRaceGame 以外への 2 ゲームパッド対応
- 勝敗集計 (トータルポイント等) の仕組み
