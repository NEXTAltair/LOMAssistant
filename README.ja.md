![珈琲のおとも](media/banner.ja.webp)

# 珈琲のおとも

[简体中文](README.md) · [繁體中文](README.zh-Hant.md) · [English](README.en.md) · **日本語** · [한국어](README.ko.md)

『活俠傳』（Legend of Mortal）用の BepInEx 6 プラグインです。名前の「咖啡伴侣」（コーヒーのおとも）は、プレイヤーが瑞杏につけたあだ名「咖啡」（コーヒー）から来ています。瑞杏の中国語読みが中国のコーヒーチェーン「瑞幸（Luckin Coffee）」と同じだからです。最新版のゲームに対応し、ストーリーの全体像、分岐の選択、数値エディタ、珈琲のおとものセーブ画面、クイックセーブなどを追加します。

[日本語化MOD](https://dlaqe2334.github.io/LOM-JPMOD/)を導入している場合、このMODのウィンドウやメッセージは自動的に日本語で表示されます（設定タブ上部の「表示言語」でも切り替え可能）。現在はよく使う画面の文字だけを翻訳しており、それ以外は中国語のままです。翻訳は正確でない場合があります。おかしな訳を見つけたら [Issues](https://github.com/kevinnie2003/LOMAssistant/issues) で教えてください。以下のスクリーンショットは中国語版です。

[![ダウンロード](https://img.shields.io/github/v/release/kevinnie2003/LOMAssistant?style=for-the-badge&label=GitHub&labelColor=24292F&color=A3302A&logo=github&logoColor=white)](https://github.com/kevinnie2003/LOMAssistant/releases/latest/download/LOMAssistant.zip) [![Nexus Mods](https://img.shields.io/badge/Nexus%20Mods-%E3%83%80%E3%82%A6%E3%83%B3%E3%83%AD%E3%83%BC%E3%83%89-D98F40?style=for-the-badge&labelColor=8A5A28&logo=data%3Aimage%2Fpng%3Bbase64%2CiVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAEXElEQVR4AazW7XkTOxAF4E0auaYSQiVAJQmVECrBVEJuJeG8Y42stR3yBz%2BalTQfZ45GWq3vt3%2F8e319PUQeI7%2BH%2FEz%2F8FaamwQSAOQh%2FVUgXUSC7%2BmBt5g%2FJtHvyFPkMARG%2B9BFfW47AgGU%2BGfMQPQCrUQvwWts9BJ8yRh4izl91LO9zNG28YNz2JbfJCB59BJzzHA2AXQSTOU7g6e70%2B9D%2FL5GmggsJOBFvW2TQGbfI90EHDN5jujTVWu9lX6KZhU69k%2FJ%2FS22ahnD4KenQ2LmKgJZPUWzKhCeCcaeCJYAEEKqYStWYf%2BaGHbhU6KDiRQceltd%2Be6T3AAgAwEi4GXYJBHMZosksgrzVcRdJQ%2BGZBYIZ42rsQp8XlBWEEkFqkAnXlx3w2NW2aubhiS3OIm32J2HH9M4BghwqmmcJkjGqmBeAOXw9gPZnXVJXrYx%2F7g4VbUQaJ2EPa4%2BJAQrec1vPBBcq1YuI1kTdzdYuWrOrQ525UOgBom0V9Mh82pxRMI21DwPSdNVO8S%2BzkuZh6Tpqtlr2wynFHnU6tPXa7ga3HAC2KaMJPYQkdX%2Fynesfm7rALlc2Iy7Dzg2vQqGLt2IPXXxe4k8R1Rs%2Bo%2BEJ6c8Y4fndc1s1%2F7LjC3dptpyVQUofnkMOQbU9Xu5imGuzp7WII86WIkBqoJ9Xce0a6pwVT1ngFeBGESAY%2BfuJ8ZRn9tYZa%2FmS5Kr2nuv6iVO5WwCZ%2FRtU%2BItPwFYu7v1UW1bktVKa3J68FurJR65v709IsXNLaBoKUNP0tc8iZXXKgnwy6R0vgMfUqFbZyBQu4boJFCTxWwFy3TzDktQZIZhF5Ok3yIzLmP77c2ZusSt8f9nPgnUhOIdkXT91Da4bVkrUjAh4c1RDeTFehPK1o8%2BA4yte8yggTOcTWLltbJWOrA9dtn0eNeHiBhE5lmKw%2FkQxkHCJqFMK3B8qz3lHLDVZDzEjeFWFYjPmmTrX3LAR6JV9ZevK9BKvSSqoGzmLb54QHruC2feJGyDOK%2FuzXtkkHCbwpDn80qAgqHF5dTgEnVg272SVts%2B9E0aFiII0a%2By%2Bj8UgYuyuWbtmZPfSX3xkJhAI4ZPJ522MUDCtu1IjCo01qEIJGA9nb%2BGEyJuOO%2F2yjru1XbApdk%2FJLGA2us2DeLIUT03AZOWj3GS%2BCFEnPqr5LFL3iAdt%2FZiHDhvhgOtUrZMTI2H849bBHzjrRrA8Dt3Se602%2Fuz8nR9W7HtEIeARdg6lWTjvyZ3qI9NAEsORMlWR7qSkRywMyKRErvt9BJZITsiYh4To1pW7wpHnr4vqNNNmFJjCISR%2BMJ5lWqlAelXDDg7gsZ6wMaSlj%2BHCDLm9BKbR11t5uoKeKetwGrKIw8BXiUJCKCo320WA%2BstR9tri8o%2BCZiNSiCxAiDC3IKIFRBjvnrAgbhzcNng9DaZOw93%2Bc3kAHcEKOJgfyogc%2BDpNquSBLivnr9mxBiwfgc8cI7p%2BZVsN35%2FAAAA%2F%2F8CpstnAAAABklEQVQDAC6567qqPgZJAAAAAElFTkSuQmCC)](https://www.nexusmods.com/legendofmortal/mods/10)

[![ダウンロード数](https://img.shields.io/github/downloads/kevinnie2003/LOMAssistant/total?style=flat&label=%E3%83%80%E3%82%A6%E3%83%B3%E3%83%AD%E3%83%BC%E3%83%89%E6%95%B0&color=555&logo=github&logoColor=white)](https://github.com/kevinnie2003/LOMAssistant/releases)

## 導入

ZIP には BepInEx が同梱されています。

**インストーラー（おすすめ）**：解凍して `咖啡伴侣安装器.exe` を実行し、「インストール／更新」をクリックします。インストーラーは Windows の表示言語に合わせて表示されます。

- ゲームフォルダを自動で探し、最新版をダウンロードします（GitHub に繋がらない場合は同梱版を導入）。
- 競合する古いMODは `BepInEx\已停用的Mod` に移動します（削除はしません）。設定はそのまま残ります。
- 更新するときはもう一度実行するだけです。Windows の「PC を保護しました」が出たら「詳細情報」→「実行」。

<img src="media/installer.webp" width="640" alt="インストーラー：ゲームフォルダ、インストール済みと予定のバージョン、更新内容、インストール／更新、診断の書き出し、アンインストール">

**手動**：Steam で『活侠傳』を右クリック → 管理 → ローカルファイルを閲覧し、ZIP の中身をすべてゲームフォルダに上書きで展開します。

**アンインストール**：インストーラーの「アンインストール」をクリックします。セーブデータには影響しません。

## ホットキー

| キー | 機能 |
|---|---|
| F8 | 設定。セーブ画面では選択中のセーブにメモを書く |
| F5 / F7 | クイックセーブ／クイックロード（最新のクイックセーブ） |
| F6 | 「クイックセーブ」タブ（一覧、ロード、メモ、削除） |
| F2 | 既読会話を早送り（Ctrl 押しっぱなしと同じ）。未読の会話で止まる |
| F4 | 判定前に戻す |
| F9 | 数値タブ |
| Tab | 咖啡伴侣ウィンドウ |
| パッド L3 | 咖啡伴侣ウィンドウを開閉（左スティック押し込み。設定タブで R3 や LB+RB に変更可） |
| ESC | ゲームのメニューに「見送り」「即勝利」「診断を書き出す」を追加 |

キーは設定タブの「ホットキー」で変更でき、どれも「解除」できます（ウィンドウを開く Tab を除く）。

## 機能

### ストーリー

- **ストーリー回顧**（Tab）：
  - **いま**：今の場面の選択肢・ダイスの結果、その後の会話を読んだかどうか、分岐条件、現在地のランダムイベント（次に起きるものを選べる）、現在の任務と足りない条件。

    <img src="media/story-now.webp" width="640" alt="いま：この場面の選択肢を回ごとにまとめ、数値の増加は緑、減少は赤。現在の任務">

  - **ルート図**：メインストーリー全体を暦に沿って並べ、サブは下に表示。駅をクリックすると、いつ・どこで・どんな条件で発生するかが分かります。任務名・場所・会話で検索できます。

    <img src="media/story-route.webp" width="640" alt="ルート図：メインストーリーを暦の駅として並べ、サブクエストは下に">

  - **ストーリー図**：任務ひとつを流れ図にし、各ルートの選択肢・条件と数値の変化を表示します。

    <img src="media/story-flow.webp" width="640" alt="ストーリー図：場面と場面の間の選択肢・条件・行き先">

  - **分岐セーブ**：重要な場面の始まりで自動保存し、「ここからやり直す」で何度でも試せます。セーブフォルダに `LOMStory_` で始まるファイルとして保存され、Steam クラウドで同期されます。

    <img src="media/story-nodes.webp" width="640" alt="分岐セーブ：記録した場面を日付順に並べ、やり直し・削除">

  - **エンディング一覧**：エンディング（汗青書）、死亡（生死簿）、実績（風雲史）。未達成のものは到達方法を表示します。ゲーム図鑑の番号も表示し、番号で検索でき、番号順・時期順で並べ替えられます。

    <img src="media/story-endings.webp" width="640" alt="エンディング一覧：エンディング・死亡・実績、達成・未達成で絞り込み">

- **ダイスを選ぶ**：ダイス判定でルートをクリックすると、その結果になります。
- **選択肢の結果**：会話の選択肢に、選ぶと何が変わるかを表示。ロックされた選択肢には本当の解放条件を表示し、「ワンクリック解除」で条件を満たせます。

  <img src="media/option-hint.webp" width="640" alt="選択肢の後ろに結果と解放条件">

- **分岐ウィンドウ**（既定オフ、ネタバレ注意）：分岐条件を表示し、満たしていれば緑。

  <img src="media/branch-panel.webp" width="480" alt="分岐ウィンドウ：満たしていれば緑、未達は赤">

- **UI を隠して CG を見る**：ストーリー中に右クリック。
- **ランダムイベントを選ぶ**（既定オフ）：仕事や散策などのランダムイベントを選んで発生させます。

  <img src="media/event-picker.webp" width="480" alt="ランダムイベントを選ぶ：発生しうるイベント、確率と変化">

- **フラグ変化の通知**（既定オフ、ネタバレ注意）。
- **マップの建物イベント表示**（既定オン）：育成マップで建物にマウスを乗せると、そこのメイン・サブクエスト（今発生するか、時期、条件。満たしていれば緑、未達は赤）とランダムイベントの確率を表示します。今発生できて、まだ見ていないメイン・サブクエストがある建物は光り続けます（ランダムイベントは対象外）。

### セーブ

- **クイックセーブ**：会話・選択肢・ダイスの時や育成画面で F5 を押すと保存（ゲームのスロットは使いません。最大 100 個、変更可）。F7 でクイックロード（最新をロード）、F6 で一覧。

  <img src="media/anytime-save.webp" width="640" alt="クイックセーブタブ：全セーブの一覧、ロード・メモ・削除">

- **判定前に戻す**（F4）：判定の前の会話に戻り、F9 で数値を調整してから進むと、新しい数値で判定し直します。
- **珈琲のおとものセーブ画面**（既定オン。セーブとロードの両方の画面を置き換え、設定でゲーム標準に戻せます）：想い人の顔、ゲーム内日付、保存時刻、メモ、ロード後に流れる会話。手動セーブとオートセーブは別ページ（ゲームパッドは LB / RB）。

  <img src="media/save-browser.webp" width="640" alt="新しいロード画面：左に想い人の顔とメモ付きの一覧、右に立ち絵とロード後の会話">

- **伝奇スロット 5 ページ**（本来は 1 ページ 12 枠、変更可）：伝奇の保存画面と図鑑の伝奇画面の下にページ送りボタンが付き、保存時は空きのある最初のページを開きます。
- **手動セーブ 100 スロット**（本来は 20）、**セーブのメモ**、オートセーブの**旧データを 5 個ずつ保持**。

### 育成と戦闘

- 設定タブ：能力値の上限突破、修練回数無制限、鍛造・錬丹無制限、決闘で無敵、集団戦でダメージなし、奥義クールなし、ゲーム速度 1x/2x/3x/5x、ピンボールの得点。
- ESC メニューの「見送り」と「即勝利」。
- **進行の高速化**：シーン切り替え、ロード、早送りが速くなります。
- **行動力**：通常／消費なし／毎旬N回。ストーリーの発生に影響することがあるので、こまめにセーブしてください。
- **決闘の敵行動予測**（既定オフ）：決闘で行動を選ぶとき、画面右端に敵の予定行動を表示し、敵がその場で行動を変えそうなときは注記します。
- **四師兄が全アイテムを販売**（既定オフ）。

### 数値タブ（F9）

能力値、武功、門派、好感度、天賦、修練ポイント、施設、書籍、アイテム、称号、すべてのフラグを編集できます。フラグは門派の好感度・人物・六道・ストーリーなどに分類されています。フラグ・書籍・アイテムの名前を押すと、どのストーリーで変更・判定されるかを任務・時期・場所とともに一覧し、ストーリー図へ移動できます。むやみに変えるとストーリーが壊れることがあるので、先にセーブしてください。

<img src="media/stats.webp" width="640" alt="数値タブ：能力値・武功・門派・称号・好感度などをすべて編集可能">

### その他

- **ゲームパッド**：咖啡伴侣ウィンドウはパッドだけで操作できます（検索欄や数値欄への入力にはキーボードが必要）。左スティック押し込み（L3）でウィンドウを開き、左スティックか十字キーでポインター移動、A でクリック、右スティックでスクロール、LB / RB でタブ切替、B で閉じます。ウィンドウを開いている間、ゲームはパッドに反応しません。PS・Switch のコントローラーではそれぞれのボタン名（× / ○、L1 / R1 など）で案内します。
- **ダークモード**：設定タブの「ウィンドウの配色」でライト・ダーク・システムに合わせるを選べます。
- **伝奇の書き出し**：ゲームの「状態 → 伝奇」に、今のセーブのまだ終わっていない伝奇をデスクトップに txt で保存するボタンを追加します。
- **多言語**：ゲームの言語（簡体字・繁体字・韓国語）に合わせて表示し、日本語化MODがあれば日本語で表示します。
- **診断を書き出す**：ESC →「環境設定」→「診断を書き出す」で、ログ・設定・プラグイン一覧をデスクトップにまとめます（セーブは含みません）。
- **競合の検出**：競合する古いMODがあると、設定タブを開いて知らせます。

## よくある質問

**不具合があったら？** まずインストーラーをもう一度実行して最新版にしてください。それでも直らなければ、診断を書き出して [Issues](https://github.com/kevinnie2003/LOMAssistant/issues) に状況と一緒に投稿してください。

**導入しても何も変わらない？** タスクトレイから Steam を完全に終了してから、ゲームを起動し直してください。

**ゲームのパスに日本語などが含まれている？** 英語以外の文字を含むパスでは BepInEx の読み込みに失敗し、MOD が動作しません（インストーラーが赤字で警告します）。ゲームフォルダ全体を英数字だけのパス（例：`D:\Games\LegendOfMortal`。Steam 版はライブラリで右クリック → プロパティ → インストール済みファイル → インストールフォルダを移動）に移してから、インストーラーを実行し直してください。

**ほかの MOD とホットキーがかぶったら？** ほかの MOD（日本語化パッチ、邪神认知污染ON、剧情随行、設定ファイルでキーを決めている MOD）が同じキーを使っていると分かると、このMODの方のホットキーを未割り当てにし、設定タブの上に一度だけお知らせを出します。Tab でウィンドウを開いて該当タブを選ぶか、設定の「ホットキー」で別のキーを割り当ててください。どのホットキーも手動で「解除」できます。

**Linux / Steam Deck？** 手動で導入し、Steam の起動オプションに `WINEDLLOVERRIDES="winhttp=n,b" %command%` を追加してください。

## 設定

設定はすべてウィンドウの設定タブ（F8）にあり、変更はすぐ保存されます。名前にマウスを乗せると説明が出ます。`BepInEx\config\LOMAssistant.cfg` にも保存されます。

<img src="media/settings.webp" width="640" alt="設定タブ：スイッチ・数値・ゲーム速度・ホットキーをグループごとに">

## クレジット

[中国語版 README](README.md#致谢) を参照してください。本プロジェクトは爱死氏の「活侠传助手」をもとにしており、MortalAnytimeSave、赵活系统、LOM_SaveSlotExpander の機能やコードを参考・移植しています。[lxsb222-spec](https://github.com/lxsb222-spec)、[blakceyes](https://github.com/blakceyes)、[NEXTAltair](https://github.com/NEXTAltair) のプルリクエストに感謝します。
