# VocaNerd シーン組み立て手順

Unity 側で行う手作業をまとめる。スクリプトは全て `Assets/Scripts/` に配置済み。
Panel は **Prefab 化して Instantiate/Destroy 方式** で切り替える。

## 1. UniTask のインポート

`Packages/manifest.json` に `com.cysharp.unitask` を追加済み。Unity 起動時に自動で GitHub から解決される。Package Manager で `UniTask` が表示されれば OK。

## 2. ScriptableObject（4ミニゲーム分）を作成

Project ビューで右クリック → `Create > VocaNerd > MiniGameData` を 4 つ作成。各アセットに以下を設定:

- **Title**: ミニゲーム名
- **Description**: 説明文
- **Thumbnail**: 選択画面用のサムネ Sprite
- **Video File Name**: WebGL 用に `Assets/Video/` からコピーする説明動画のファイル名（例: `dance.mp4`）
- **Video Clip**: Editor / 通常ビルドで再生する説明動画

## 3. Panel Prefab を生成

Unity メニュー **`VocaNerd > Generate Sample Prefabs`** を実行すると、
`Assets/Prefabs/` 以下に以下 4 つの Prefab が自動生成される（`Assets/Editor/PrefabGenerator.cs`）。

- TitlePanel.prefab
- SelectPanel.prefab
- ExplainPanel.prefab
- MiniGamePanel.prefab

各 Prefab には CanvasGroup と対応する Panel スクリプトがアタッチ済み、Inspector 参照も配線済み。
レイアウトはサンプル配置なので必要に応じて調整すること。

`SelectPanel.prefab` の `Mini Games` 配列だけは空なので、
手順 2 で作成した 4 つの MiniGameData ScriptableObject を Prefab を開いてドラッグしてアサインする。

以下は自動生成される Prefab の構造リファレンス。

### TitlePanel.prefab

各 Prefab のルートには **CanvasGroup を必ずアタッチ**（ScreenController が alpha でフェード制御する）。

### TitlePanel.prefab
```
Root (RectTransform + CanvasGroup + TitlePanel.cs)
└── StartButton (Button)
```
Inspector:
- Start Button → StartButton

### SelectPanel.prefab
```
Root (RectTransform + CanvasGroup + SelectPanel.cs)
├── MiniGameButton_0 (Button + Image)
├── MiniGameButton_1
├── MiniGameButton_2
└── MiniGameButton_3
```
Inspector:
- Mini Games: 4 つの MiniGameData（ScriptableObject）
- Mini Game Buttons: 4 つの Button
- Mini Game Thumbnails: 4 つの Image（各ボタンの Image を流用可）

### ExplainPanel.prefab
```
Root (RectTransform + CanvasGroup + ExplainPanel.cs)
├── TitleText (TMP_Text)
├── DescriptionText (TMP_Text)
├── VideoDisplay (RawImage)
├── VideoPlayer (VideoPlayer, RenderMode=Render Texture, PlayOnAwake=OFF)
├── PlayButton (Button)
└── BackButton (Button)
```
Inspector:
- Title Text / Description Text: TMP_Text
- Video Player: VideoPlayer
- Video Display: RawImage
- Play Button / Back Button: Button

VideoPlayer 設定:
- Render Mode: `Render Texture`
- Target Texture: 専用 RenderTexture をアサイン
- Audio Output Mode: `Direct` または `None`
- Play On Awake: OFF

RawImage の Texture にも同じ RenderTexture を設定しておく（初期表示のため）。

### MiniGamePanel.prefab
```
Root (RectTransform + CanvasGroup + MiniGamePanel.cs)
├── TitleText (TMP_Text)
└── BackButton (Button)
```
Inspector:
- Title Text: TMP_Text
- Back Button: Button

## 4. シーン組み立て

`Assets/Scenes/SampleScene.unity` に以下 2 つの Prefab をドラッグ配置する:

```
MainCanvas (Prefab)                    ← 4:3レターボックス + ScreenController付き
├── LetterboxBackground (black)
└── AspectFrame (AspectRatioFitter 4:3, ScreenController)
    └── (実行時にここへPanelがInstantiateされる)

BlackFadeOverlay (Prefab)              ← 独自Canvas、常に最前面
```

**MainCanvas.prefab** は 1600x1200 参照解像度・4:3 強制のセットアップ済み:
- CanvasScaler: ScaleWithScreenSize, 1600x1200, MatchWidthOrHeight=0.5
- LetterboxBackground: 全画面黒 Image（非4:3の画面で黒帯として見える）
- AspectFrame: `AspectRatioFitter.FitInParent`, 4/3 → 親サイズに合わせて4:3を保持
- ScreenController が AspectFrame にアタッチ済み、`Root` は AspectFrame 自身に配線済み

`BlackFadeOverlay.prefab` は独自 Canvas（sortingOrder=short.MaxValue）を持つので
MainCanvas とは別に **シーン直下に配置**する。コードからは
`BlackFadeOverlay.Instance.FadeInAsync(0.5f)` で使う。

`AspectFrame` の `ScreenController` の Inspector:
- **Root**: Panel を Instantiate する親 RectTransform（Canvas 直下でよい）
- **Screens** 配列に 4 要素:
  - `Title` → TitlePanel.prefab
  - `Select` → SelectPanel.prefab
  - `Explain` → ExplainPanel.prefab
  - `MiniGame` → MiniGamePanel.prefab
- **Fade Duration**: 0.25 など

## 5. 動作フロー

- 起動時: `ScreenController.Start()` が Title Prefab を Instantiate
- StartButton → Select を Instantiate、Title は Destroy
- 各 MiniGameButton → Explain を Instantiate + `Bind(data)`、Select は Destroy
- PlayButton → MiniGame を Instantiate + `Bind(data)`、Explain は Destroy（VideoPlayer も一緒に破棄されるので停止処理不要）
- BackButton → Select を Instantiate、現在の画面は Destroy

Panel 間の直接参照は無いので、Prefab は完全に独立。
