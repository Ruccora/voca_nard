---
name: exit-log
description: 現在のセッションでの作業を分析し、.claude/SESSION_LOG.md に新しい Phase として追記する。「exit」「セッション終了」「作業ログ残して」「/exit-log」で起動。VocaNerd 専用。
argument-hint: "[<セッションの概要ヒント>]"
user-invocable: true
allowed-tools: Bash, Read, Write, Edit, Grep, AskUserQuestion, TodoWrite
---

# exit-log - Session Wrap-up

現在の作業セッションを分析し、`VocaNerd/.claude/SESSION_LOG.md` に新しい Phase セクションとして追記するスキル。作業のキリのタイミングやセッション終了時にログを残すために使う。

## repo 構造の前提

```
<repo-root>/                     <- .git を持つ repo root
└── VocaNerd/                    <- Unity project dir
    └── .claude/
        ├── SESSION_LOG.md       <- 追記対象
        └── skills/exit-log/
```

`SESSION_LOG.md` は Phase 1, Phase 2, ... の見出しで開発履歴を蓄積する。本スキルは末尾に新 Phase を appending する。

## 起動方法

- `/exit-log` — 引数なし。現セッションの内容を自動分析して追記
- `/exit-log <ヒント>` — セッションの主題ヒントを引数で渡す (例: `/exit-log シェーダー改修`)
- 「exit」「セッション終了」「作業ログを残して」等の自然言語トリガーでも起動可能

---

## 実行フロー

### 1. パスと状態を取得

```bash
REPO_ROOT=$(git rev-parse --show-toplevel)
PROJECT_DIR="$REPO_ROOT/VocaNerd"
LOG_PATH="$PROJECT_DIR/.claude/SESSION_LOG.md"
```

- `SESSION_LOG.md` が存在するか確認。無い場合は「新規作成でよいか」を AskUserQuestion で確認 (既存 REQUIREMENTS.md 等のテンプレートを参考に骨格を作る)
- 現在の日付を取得: `date +%Y-%m-%d` (システムリマインダーで最新日付が渡されていればそれを優先)

### 2. セッションの作業内容を収集

以下を並列で調べる:

**Git 変更内容:**
```bash
git -C "$REPO_ROOT" status --short          # 未コミット変更
git -C "$REPO_ROOT" diff --stat             # 変更ファイル概要
git -C "$REPO_ROOT" log --oneline -20       # 最近のコミット (直近セッションで push したものを含む)
```

**SESSION_LOG.md の最新 Phase を確認:**
- 最後の Phase 番号を Grep で取得
- 「### コミット履歴」テーブルに未記載のコミットがあれば新規追加対象

**会話履歴からトピック抽出:**
- 直近のセッションでのユーザーからの要求と実装内容を要約
- 主な変更ポイント、設計判断、詰まった点を洗い出す

### 3. Phase 番号を決定

- 既存の最終 Phase 番号を +1 (例: 最終が Phase 13 なら Phase 14)
- 大きな機能追加 or 独立トピックなら新 Phase、既存 Phase の追記/改修なら「Phase X 追記」形式でも良い
- ユーザーに Phase タイトル案を提示 (AskUserQuestion):
  - 例: 「Phase 14: シェーダー Bloom 化 と 2 Gamepad 対応 で追記しますか?」
  - 「はい / タイトルを変更 / 既存 Phase に追記」

### 4. 追記内容を構築

以下のテンプレートに沿って新セクションを組み立てる:

```markdown
## Phase N: <セッションタイトル>

**日付**: <YYYY-MM-DD>
**目的**: <このセッションで達成したかったこと (1-2行)>

**実装:**
- <実装項目 1>
- <実装項目 2>
- ...

**設計判断 (該当があれば):**
- <判断項目>: <採用> — <理由>

**触ったファイル:**
- `<ファイルパス>` — <変更概要>

**コミット (該当があれば):**
- `<hash>` <YYYY-MM-DD> <summary>

**未着手 / 積み残し (該当があれば):**
- <TODO 項目>

---
```

- 実装項目は具体的に (「シェーダーを改修」ではなく「UISparkle.shader: 5-tap ブラー追加、Pulse Amount プロパティ追加」等)
- コミット履歴は `git log` から未記載のものを抽出
- 未着手は Todo が残っていれば含める

### 5. コミット履歴テーブルも更新

`SESSION_LOG.md` の末尾近くに「## コミット履歴」テーブルがあれば、そこにも今回のコミットを追記する。

新 Phase セクションは「## コミット履歴」より **上** に挿入。既存の「## 現在の状態」「## 進行中/未着手」セクションは末尾に維持。

### 6. ファイル書き込み

- `Edit` ツールで既存の SESSION_LOG.md に追記
  - 挿入位置は「## コミット履歴」の直前 (存在する場合)
  - 「## 現在の状態」の直前 (コミット履歴が無い場合)
- 「## 現在の状態」セクションも今のスナップショットで軽く更新 (例: 新機能を追加した旨、既存項目に反映)

### 7. ユーザーに確認

追記後、以下の情報を出力:

```
SESSION_LOG.md に Phase N を追記しました:
- Phase タイトル: <タイトル>
- 触ったファイル数: <n> 件
- 追記行数: 約 <n> 行

次のアクション:
- そのまま終了 → 何もしない
- コミット & push → `git add .claude/SESSION_LOG.md && commit && push` を提案
```

**「コミットしますか?」を AskUserQuestion で選択:**
- はい: `git add` して commit & push (メッセージは「Add SESSION_LOG Phase N: <タイトル>」)
- いいえ: 変更を残したまま終了 (ユーザーが後で自分でコミット)

### 8. 出力

最後にセッションの締めとして短くまとめる:
- 何を達成したか
- 何が積み残しか
- 次回セッションで最初にやるべきこと (もしあれば)

---

## ルール / 注意点

- **勝手にコミット・push しない**: 常に AskUserQuestion で確認
- **既存 Phase を書き換えない**: 追記のみ。ユーザーが明示的に「Phase X を修正」と言った場合のみ書き換え検討
- **日付は現在時刻を使う**: システムリマインダーで最新日付が渡されていればそれを優先
- **Phase 番号の連番を守る**: 既存の最終番号 +1
- **触ったファイルが多い場合の要約**: 20 個以上あるなら「主要ファイル」と「その他 (n 件)」に分けてまとめる
- **未着手項目**: 会話中に「後でやる」「TODO」「未実装」と言及されたものを拾い、「進行中/未着手」セクションに追加

---

## エラー処理

- `SESSION_LOG.md` が読めない → ユーザーに存在確認 & 新規作成の提案
- Git 情報取得失敗 → 会話履歴のみから Phase を組み立てる (コミット履歴セクションはスキップ)
- Phase 番号が読めない → ユーザーに直接尋ねる

---

## 出力形式 (例)

```
セッションログを追記しました:

## Phase 14: シェーダー Bloom 化 と 2 Gamepad 対応

**日付**: 2026-07-13
**目的**: QuickDraw の背景に発光演出を、複数ゲームパッドを認識させる

**実装:**
- UISparkle.shader を bloom 化 (Grid × 星型スパークル廃止、マスク領域を画像色で発光)
- 5-tap ブラーでマスク境界を柔らかく
- Phase Grid Size で位置ごとの位相バラつき
- QuickDrawGame: P1/P2 とも Gamepad South にバインド、Gamepad.all の index で振り分け

**触ったファイル:** 3 件
- Assets/Shaders/UISparkle.shader
- Assets/Scripts/QuickDrawGame.cs
- .claude/SESSION_LOG.md

**コミット:**
- a713b3c 2026-07-11 Add UI Sparkle/Bloom shader and MIKIRI mask texture
- 0f41f3e 2026-07-13 Add SESSION_LOG.md and per-gamepad routing in QuickDrawGame

**未着手:**
- 他ミニゲームの 2 Gamepad 対応 (MashRace/Hopscotch/BlockDrop)
- BGM 素材整備

次のアクション:
- 今回の変更を SESSION_LOG.md に反映済み
- コミットしますか? [はい / いいえ]
```
