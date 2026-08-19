# Assets/Audio

BGM / SE の音源置き場。

```
Assets/Audio/
├── BGM/   ← ループ素材 (.ogg / .wav)
└── SE/    ← ワンショット素材 (.wav)
```

## 追加手順

1. ファイルをここに入れる（ファイル名は `bgm_title.ogg` / `se_decide.wav` のようにキーと揃えると分かりやすい）
2. `Assets/Data/AudioLibrary.asset` を開き、対応するキーの行の **Clip** にドラッグしてアサイン
3. キー自体を増やす場合は `Assets/Scripts/AudioKeys.cs` に定数を足してから、AudioLibrary に同じキーの行を追加する

## Import 設定の目安

| 種別 | Load Type | Compression | Preload |
|---|---|---|---|
| BGM | Streaming | Vorbis (Quality 70 前後) | OFF |
| SE  | Decompress On Load | PCM または ADPCM | ON |

## 再生方法

```csharp
Audio.PlaySE(SeKey.Decide);          // ワンショット
Audio.PlayBgm(BgmKey.Title);         // クロスフェードで切替（待たない）
await Audio.PlayBgmAsync(BgmKey.Select, 1f, token);
Audio.StopBgm(0.5f);
```

UI ボタンの決定音・カーソル移動音・画面ごとの BGM は自動で鳴るので、
個別に書く必要があるのはミニゲーム内の SE のみ。詳細は `Assets/Scripts/REQUIREMENTS.md` の 4 章。
