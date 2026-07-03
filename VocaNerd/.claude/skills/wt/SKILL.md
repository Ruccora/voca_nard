---
name: wt
description: main から新規 git worktree を作成し、引数の作業内容をその worktree で実行する。VocaNerd 専用。「/wt <作業内容>」で起動。
argument-hint: "[--no-copy-settings] <作業内容>"
user-invocable: true
allowed-tools: Bash, Read, Write, Edit, Glob, Grep, TodoWrite, AskUserQuestion
---

# wt - Worktree Task

VocaNerd プロジェクトで、`origin/main` から新しい worktree を作成し、引数で指定された作業内容をその worktree で実行するスキル。

## repo 構造の前提

このリポジトリは **repo root の直下に Unity project ディレクトリ `VocaNerd/` がネストしている** 構造：

```
<repo-root>/                     <- .git を持つ repo root
├── .gitignore
└── VocaNerd/                    <- Unity project dir（Assets/ProjectSettings/Packages を持つ）
    ├── .claude/skills/wt/
    ├── .claude/worktrees/<topic>/   <- 本スキルが worktree を配置する場所
    ├── Assets/
    ├── ProjectSettings/
    └── ...
```

worktree（git worktree add で作られるチェックアウト）自体は repo 全体をチェックアウトするため、worktree 内でも Unity project は `<worktree>/VocaNerd/` にネストして現れる。

## 起動方法

- `/wt <作業内容>` — 作業内容を引数で渡す
- `/wt --no-copy-settings <作業内容>` — Unity Editor 設定のコピー（ステップ 4.5）をスキップする
- `/wt` — 引数なし。AskUserQuestion で作業内容を尋ねる

例：
- `/wt 単語カード UI のレイアウト調整`
- `/wt 発音再生の遅延調査`
- `/wt --no-copy-settings クリーン状態でビルド検証`

---

## 実行フロー

### 1. 作業内容とフラグを確定する

- 引数を先頭からパースし、以下のフラグを取り除く：
  - `--no-copy-settings` — ステップ 4.5 の Unity Editor 設定コピーをスキップする
- フラグ以外の残りを「作業内容」として保持
- 作業内容が空なら AskUserQuestion で「今回の作業内容は？」と尋ねる
- 取得した作業内容はメモしておく（後で worktree 上の作業プロンプトとして再利用）
- `COPY_SETTINGS` を boolean として保持（デフォルト `true`、`--no-copy-settings` 指定時のみ `false`）

### 1.5. GitHub ユーザー名・repo root・Unity project dir を取得する

ブランチプレフィックスと絶対パスを動的に解決する。

```bash
USER=$(gh api user --jq .login 2>/dev/null) \
  || USER=$(git config user.name | tr -d ' ' | tr '[:upper:]' '[:lower:]') \
  || USER=""
REPO_ROOT=$(git rev-parse --show-toplevel)        # 例: /Users/s13762/VocaNerd（.git を持つ）
PROJECT_DIR="$REPO_ROOT/VocaNerd"                 # Unity project dir（Assets/ProjectSettings を持つ）
```

- `gh` が未認証 / 未インストールの場合は `git config user.name` で代用
- どちらも空なら AskUserQuestion で「ブランチプレフィックスとして使うユーザー名は？」と尋ねる
- 以降の `<user>` は取得した値で置換する

### 2. topic 名を生成する

作業内容から短い英語の kebab-case スラッグを生成する。

- 3〜6 語以内、英小文字とハイフンのみ
- ローマ字や直訳ではなく、内容を表す英語名にする
- 例：
  - 「単語カード UI のレイアウト調整」→ `word-card-layout-tweak`
  - 「発音再生の遅延調査」→ `pronunciation-latency-probe`
- 生成した topic 名を **ユーザーに 1 度だけ確認**（AskUserQuestion）。「`<topic>` で進める？修正があれば指示してください」。即 OK なら進める。

### 3. 事前チェック

- `git worktree list` で実体を確認
- 同名ブランチがローカル/リモートに既にあるか確認：
  ```bash
  git branch --list "<user>/<topic>"
  git ls-remote --heads origin "<user>/<topic>"
  ```
- 既存があればユーザーに確認（「既存ブランチを使う / 別名にする / 中止」）
- 配置先 `$PROJECT_DIR/.claude/worktrees/<topic>` が既に存在するか確認。存在すれば同様に確認

### 4. main を最新化して worktree 作成

```bash
git fetch origin main
git worktree add -b "<user>/<topic>" "$PROJECT_DIR/.claude/worktrees/<topic>" origin/main
```

- ブランチ命名は **必ず `<user>/<topic>`** 形式
- ベースは `origin/main`（ローカル main の状態に依存しない）
- 作成後 `git worktree list` で実体を確認
- 作成された worktree は repo 全体のチェックアウトなので、Unity project は `$PROJECT_DIR/.claude/worktrees/<topic>/VocaNerd/` にネストして存在する

### 4.5. Unity Editor 設定を元の worktree からコピーする

**`--no-copy-settings` が指定されていた場合（`COPY_SETTINGS=false`）はこのステップ全体をスキップし、その旨を出力に明記する。** 以下はデフォルト（`COPY_SETTINGS=true`）の挙動。

`.gitignore` で除外されている `UserSettings/`・`Library/` 配下のビルド設定を、元の Unity project（`$PROJECT_DIR`）から新規 worktree 内の Unity project へコピーする。Unity Editor を **新 worktree で初めて起動する前** に実施すること。

```bash
SRC="$PROJECT_DIR"                                              # 元の Unity project
DST="$PROJECT_DIR/.claude/worktrees/<topic>/VocaNerd"           # worktree 内の Unity project

# UserSettings 全体（ウィンドウレイアウト・EditorUserSettings など）
if [ -d "$SRC/UserSettings" ]; then
  rsync -a "$SRC/UserSettings/" "$DST/UserSettings/" \
    || echo "WARN: UserSettings コピー失敗 — 手動で再コピーしてください"
fi

# Library 配下のうち、ビルドターゲット / プロファイル / Recorder セッション設定だけ選択的にコピー
mkdir -p "$DST/Library"
for item in \
  EditorUserBuildSettings.asset \
  BuildSettings.asset \
  BuildPlayer.prefs \
  BuildProfileContext.asset \
  BuildProfiles \
  Recorder; do
  if [ -e "$SRC/Library/$item" ]; then
    rsync -a "$SRC/Library/$item" "$DST/Library/" \
      || echo "WARN: Library/$item コピー失敗 — 手動で再コピーしてください"
  fi
done
```

- **Library 全体をコピーしてはいけない**：キャッシュが破損したり数 GB 単位の無駄が発生する。上記の小さな設定ファイル群のみをコピーする
- 元の Unity Editor が起動中でも上記ファイルは通常壊れないが、Recorder セッションが書き換え中の瞬間に当たる可能性はある。怪しい場合は元側の Editor をいったん閉じてから再実行する
- コピー対象が存在しなければスキップ（`if [ -e ... ]` でガード済み）
- 失敗してもブロッキングにはせず、ユーザーに「コピー失敗：手動で BuildSettings/Recorder 設定を再設定してください」と伝える

### 5. worktree 内で作業を実行

- 作成した worktree 内の Unity project ディレクトリ（`$PROJECT_DIR/.claude/worktrees/<topic>/VocaNerd`）に作業ディレクトリを切り替える方針で進める
  - 以降のファイル操作は Unity project 配下の絶対パスを使う
  - 例：`<repo-root>/VocaNerd/.claude/worktrees/<topic>/VocaNerd/Assets/...`
- ステップ 1 で受け取った作業内容を、新しい conversation context として扱い、調査・実装を進める
- TodoWrite を活用して作業を可視化

---

## ルール / 注意点

- **ブランチ名**：必ず `<user>/<topic>` 形式（`<user>` はステップ 1.5 で動的取得）
- **worktree 配置**：`$PROJECT_DIR/.claude/worktrees/<topic>`（Unity project ディレクトリ配下）
- **worktree 内の Unity project 位置**：`<worktree>/VocaNerd/`（worktree は repo 全体のチェックアウトのため 1 段深い）
- **メインリポジトリ側に未コミット変更があっても、worktree 作成は通常成功する**。が、`git status` で軽く触れて、無関係な変更には触らないようにする
- **作業終了後**：worktree の commit / push / PR 作成はユーザー指示があるまで行わない。勝手に push しない
- **エラー時**：worktree 作成や fetch でエラーが出たら、原因を報告してユーザーに判断を仰ぐ。`git worktree remove --force` などの破壊的操作は事前確認が必須

---

## 出力形式

開始時にユーザーに次の情報を伝える：

```
Worktree 準備完了：
- ブランチ: <user>/<topic>
- worktree パス: <repo-root>/VocaNerd/.claude/worktrees/<topic>
- Unity project パス: <repo-root>/VocaNerd/.claude/worktrees/<topic>/VocaNerd
- ベース: origin/main @ <commit-sha-short>
- Unity 設定コピー: <下記いずれかの文言>
  - 成功時: `UserSettings / BuildSettings / Recorder を元の Unity project から複製済み`
  - スキップ時（`--no-copy-settings` 指定）: `スキップ（--no-copy-settings 指定）`
  - 失敗時: `失敗 — BuildSettings/Recorder 設定を手動で再設定してください`

作業開始します: <作業内容>
```

実出力時は `<user>` / `<repo-root>` を実値（例: `kimura-ryoma-ab` / `/Users/s13762/VocaNerd`）に置換すること。

その後は通常の作業フローに移行する。
