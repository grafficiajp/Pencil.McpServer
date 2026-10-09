# 役割
あなたは C# / .NET 10 と MCP（Model Context Protocol）サーバー実装に精通したエンジニアです。
既存コードはありません。ゼロから新規プロジェクトを作成してください。

# 目的
ローカルのレンダリングCLI「PENCIL」をラップし、動画(mp4)を生成する MCP サーバーを作る。
Claude デスクトップ（MCPホスト）から stdio で接続し、`generate_video` ツールを呼べる状態にする。

# 技術要件
- .NET 10 コンソールアプリ（プロジェクト名: Pencil.McpServer、名前空間: Pencil.McpServer）
- MCP実装は公式C# SDK（NuGet: `ModelContextProtocol`、ホスティングは `Microsoft.Extensions.Hosting`）を使う
  - **MCPのプロトコル処理（initialize / tools/list / tools/call / 通信フレーミング）を自前で実装してはいけない**
  - 登録は `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` を基本とする
  - SDK の API は版によって変わるため、使用する版の公式ドキュメント／READMEに合わせること
- JSON生成は `Newtonsoft.Json`
- 通信は stdio のみ（TCP / HTTP は実装しない）
- 不要なパッケージは追加しない

# 厳守事項（stdio 破損防止）
- `Console.WriteLine` / `Console.Write` / `Console.Out` など標準出力への書き込みを、コード全体で一切使わない
- ログは `ILogger` で出力し、全レベルを標準エラーに向ける
  （`builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);`）
- PENCIL の標準出力・標準エラーは MCP の標準出力に流さず、プロセスから取得して処理する
- 一時ファイルは、例外発生時も含めて必ず削除する（`finally`）

# ツール定義
- ツール名: `generate_video`（メソッド名は `GenerateVideo`。名前が `generate_video` になるよう SDK の仕様に従う）
- クラスに `[McpServerToolType]`、メソッドに `[McpServerTool]` と `[Description]`、全引数に `[Description]` を付ける
- 引数:
  - `text` (string): 動画の下部に表示する文字列
  - `imagePath` (string): 使用する画像のローカル絶対パス（png / jpg / jpeg）
  - `id` (string?, 省略可): 任意。生成結果を公開する識別子（JSON の `_ID` に対応）。許可文字: 英数字、ハイフン、アンダースコア、長さ 1..64。
  - `outputFileName` (string): 出力する mp4 のフルパス（絶対パス、拡張子 .mp4）
- 説明文は Claude が使い方を判断できるよう、「何をするか」「各引数の意味と制約」「処理に時間がかかること」「成功時は出力ファイルのフルパスを返すこと」を具体的に書く
- 引数チェック（失敗時は原因が分かるメッセージで例外を投げる）:
  - `text` が空でない
  - `imagePath` がローカル絶対パスまたは `http://` / `https://` のURIである
  - `imagePath` がローカル絶対パスの場合は、ファイルが存在し、拡張子が対応形式である
  - `imagePath` がURIの場合は、PENCIL が一時フォルダに画像をダウンロードしてから処理する前提で受け付ける
  - `outputFileName` が絶対パスで拡張子が .mp4。出力先フォルダが無ければ作成する
  - `id` は省略可。指定された場合は正規表現 `^[A-Za-z0-9_-]{1,64}$` に一致すること。指定がない場合は ScenarioBuilder が outputFileName のファイル名（拡張子除く）から推定して `_ID` を設定することがある

# PENCIL が受け付ける JSON 仕様（以下 リクエストJSON）
PENCIL に渡すのは次の構造の JSON ファイル。**値はすべて文字列**で、数値も `"0.8"` のように文字列で出力する。
時間は `"0:0:10"` / `"0:0:0.5"` 形式（時:分:秒）。

```jsonc
{
  "_ID": "pencilout0000",
  "_LocalOutputFileName": "c:/PencilOut/pencilout0000.mp4",        // 出力mp4のパス
  "_Template": "Template.PencilCanvas",           // 固定値
  "_Scenario": "Template.xml",                    // 固定値
  "_root.OutputWidth": "800",
  "_root.OutputHeight": "800",
  "Sequence": [                                   // 先頭から順にシーンを出力
    {
      "Overlay.Name": "Scene",                    // 動画シーンの識別子
      "Duration": "0:0:10",                       // 省略可
      "Layers": [                                 // 背面から前面の順に描画
        {
          "Overlay.Name": "PictureLayer",         // 画像レイヤーの識別子
          "Source": "<画像のURI>",
          "StartTime": "0:0:0", "Duration": "0:0:5",
          "Top": "0.1", "Left": "0.1", "Height": "0.8", "Width": "0.8"   // 動画解像度に対する比率
        },
        {
          "Overlay.Name": "TextLayer",            // 文字レイヤーの識別子
          "Text": "表示する文字",
          "FontSize": "0.05",                     // 動画の高さに対する比率
          "TextAlignment": "Center",
          "StartTime": "0:0:0.5", "Duration": "0:0:2",
          "Top": "0.85", "Left": "0.05", "Height": "0.1", "Width": "0.9"
        }
      ]
    },
    { "Overlay.Name": "EmptyScene", "Duration": "0:0:3" }   // 空白シーン
  ]
}
```

追記:

- リクエストJSON の _ID プロパティ:
  - リクエストJSON に `_ID` プロパティが存在する場合、ここに任意の文字列を指定すると、生成した動画ファイルを特定の FTP サーバーにアップロードする実装とする。
  - アップロードされたファイル名は `{_ID}.mp4` となる。例: `_ID": "id000"` と指定した場合、生成された動画は次の URL で認証なしにアクセス可能になる:
    http://grafficia.xsrv.jp/uploadtest/id000.mp4

- FTP アップロード — 実装フローと設定項目:
  - フロー（高レベル）:
    1. PENCIL レンダリングが成功し、出力ファイルが存在することを確認する。
    2. `_ID` の存在を確認し、有効な文字列かバリデーションを行う（許可文字: 英数字, ハイフン, アンダースコア、長さ上限 64）。
    3. アップロード設定を読み取り（appsettings.json / 環境変数）、UploadToFtpAsync を呼ぶ。
    4. アップロード成功時は公開 URL を組み立てて MCP の成功レスポンスに含める（例: PublicUrlBase + `{_ID}.mp4`）。
    5. アップロード失敗時は設定に従いエラーを返すか（既定: エラー）、ローカルパスのみ返すかを決定する。

  - 実装方針:
    - 外部ライブラリを増やさない方針に従い、.NET 標準の FTP クライアント（System.Net.FtpWebRequest）を用いて STOR 操作でアップロードする。
    - UploadToFtpAsync は CancellationToken を受け取り、タイムアウト・キャンセル時に確実に中断してストリームと接続を解放する。
    - 再試行（RetryCount、指数バックオフ）を実装する。タイムアウト・例外情報はログに記録する。
    - 認証情報は可能な限り環境変数かユーザーシークレットで管理し、ソース管理に平文で置かない。

  - 設定項目（appsettings.json 例）:

```json
"FtpUpload": {
  "Enabled": true,
  "Host": "grafficia.xsrv.jp",
  "Port": 21,
  "Username": "",            // 空の場合は匿名接続を試行（推奨はしない）
  "Password": "",
  "RemotePath": "/uploadtest/",
  "UsePassive": true,
  "TimeoutSeconds": 30,
  "RetryCount": 3,
  "Overwrite": true,
  "PublicUrlBase": "http://grafficia.xsrv.jp/uploadtest/",
  "FailOnUploadError": true    // true: アップロード失敗は MCP エラーとして返す
}
```

  - 入力検査（_ID）:
    - 正規表現例: `^[A-Za-z0-9_-]{1,64}$`。不正な ID はエラーにして処理を中断する。

  - 成功レスポンス:
    - アップロード成功時は MCP の成功レスポンスに公開 URL を追加する。例: `http://grafficia.xsrv.jp/uploadtest/{_ID}.mp4`。

  - エラー処理:
    - アップロードに失敗した場合はログ記録の上で設定に従い MCP エラーを返す（FailOnUploadError=true の場合）。
    - 例外メッセージは長すぎないように切り詰めて返す。

  詳細（運用・実装上の注意）:

  - セキュリティ:
    - 標準 FTP は認証情報と転送内容が平文のため、セキュリティリスクが高い。可能であれば FTPS（Explicit/Implicit）または SFTP（SSH）への移行を強く推奨する。SFTP を採用する場合は追加のライブラリ（例: SSH.NET）が必要になる。
    - 平文 FTP を用いる場合はアクセス元を限定する（社内ネットワーク、VPN のみ許可等）、かつ認証情報は appsettings.json に平文で置かず、環境変数やユーザーシークレット、Vault で管理すること。
    - PublicUrlBase によって公開されるファイルは認証不要で参照可能となるため、個人情報や機密データをアップロードしない運用ルールを必ず定める。

  - ネットワーク／ファイアウォール:
    - Passive モード（UsePassive=true）を推奨する。FTP サーバー側でパッシブ用ポートレンジを設定し、当該ポート群をファイアウォールで開放する必要がある。
    - コントロールポート（通常 21）とデータ用のパッシブポートを考慮し、NAT／プロキシ環境での接続性を事前に確認する。

  - 転送の挙動とパフォーマンス:
    - バイナリ転送を利用する（UseBinary=true を設定推奨）。大きなファイルでは TimeoutSeconds を長めにし、RetryCount を増やすと安定する。
    - Overwrite=true の場合は既存ファイルを削除してからアップロードする実装とする。サーバー固有の挙動により一時的に不整合が生じるため、必要であればサーバー側で原子的な置換（リネーム）を検討する。

  - 操作・テスト手順（例）:
    - curl (Passive モード) の例:
      - curl --ftp-pasv -T local.mp4 ftp://user:pass@grafficia.xsrv.jp/uploadtest/id000.mp4
    - PowerShell の簡易例:
      - $wc = New-Object System.Net.WebClient; $wc.Credentials = New-Object System.Net.NetworkCredential("user","pass"); $wc.UploadFile("ftp://grafficia.xsrv.jp/uploadtest/id000.mp4","STOR","C:\\path\\to\\local.mp4")
    - 公開 URL は PublicUrlBase とファイル名を結合して構築する（末尾スラッシュの有無に注意）。

  - ログ・監視:
    - アップロードの試行・成功・失敗をログに残す。ログには認証情報を出力しない（マスキング／除外）。
    - 失敗時は FTP の応答コードや例外メッセージ（必要に応じて切り詰め）を記録し、オペレータが対応できるようにする。

  - 例外・リトライ方針:
    - 一時的なネットワーク障害は指数バックオフで再試行する（既定: RetryCount=3、バックオフ: 2^attempt 秒）。
    - 認証失敗やパーミッションエラーなど恒久的エラーは再試行せず即時失敗とする。

  - 運用上の注意:
    - 公開領域にファイルが蓄積すると容量問題や公開ポリシーに影響するため、古いファイルの自動削除や TTL を検討する。
    - _ID によって公開 URL が確定するため、命名衝突を避ける命名規約（プレフィックス、日付、UUID など）を用いることを推奨する。

  - 改善提案:
    - セキュリティ強化のため SFTP/FTPS を検討する（SFTP は外部ライブラリが必要）。
    - リモートディレクトリが存在しない場合に MKD で自動作成する機能を追加すると運用が楽になる。
    - 大容量ファイルを扱う場合はデフォルトのタイムアウトを長めにするか、転送進捗ログを出すと良い。

- 画像の `Source` に指定できるパス:
  - `Source` には、MCP サーバーがインストールされているローカル PC の絶対ファイルパス（必要に応じて file:/// 形式の URI に変換）または、http/https で始まるオンライン上の画像 URL を指定できる。
  - 例: `"Source": "http://grafficia.xsrv.jp/sampledata/jpg/H1.jpg"`

# 実装要件

## 1. プロジェクト構成（Program.cs に処理を詰め込まない）
```
Program.cs                      … ホスト構築とMCP登録のみ
Tools/VideoTools.cs             … generate_video（引数検証と各サービスの呼び出し）
Models/PencilCanvasModels.cs    … JSON用モデル
Services/ScenarioBuilder.cs     … 引数 → モデルのオブジェクトツリー構築
Services/PencilRenderer.cs      … 一時JSON作成・プロセス起動・結果判定・クリーンアップ
```

## 2. データモデル
- 上記JSONの全キーを `[JsonProperty("...")]` で正確にマッピングする（ドット付き・先頭アンダースコア付きのキー名に注意）
- 共通基底クラスを作る: `SceneElementBase`（`Overlay.Name`、`Duration`）、`LayerBase`
  - `Scene`（`Layers` を持つ）、`EmptyScene`、`PictureLayer`、`TextLayer` を継承クラスとして実装
  - `Overlay.Name` の値は各クラスの固定値とし、外から変更できないようにする（"Scene" / "EmptyScene" / "PictureLayer" / "TextLayer"）
  - `Sequence` は `List<SceneElementBase>`、`Layers` は `List<LayerBase>`。派生クラスが正しくシリアライズされることを確認する
- 数値・時間も含め、すべてのプロパティを `string` で持つ
- `NullValueHandling.Ignore`、インデント付きで出力する。キーの順序は仕様書の並びに合わせる（`Order` 指定）

## 3. シナリオの組み立て（ScenarioBuilder）
`generate_video` の引数（text, imagePath, id, outputFileName）から、次の固定構成を作る。id は省略可能で、固定値は `const` で名前を付けてクラス先頭にまとめる。
- トップ: `_LocalOutputFileName` = `outputFileName`、`_Template` = "Template.PencilCanvas"、`_Scenario` = "Template.xml"、`_root.OutputWidth` / `_root.OutputHeight` = "800"
- Sequence は `Scene` を1つ（Duration "0:0:10"）
  - PictureLayer: `Source` = 画像のURI、StartTime "0:0:0"、Duration "0:0:10"、Top/Left "0.1"、Height/Width "0.8"
  - TextLayer: `Text` = `text`、FontSize "0.05"、TextAlignment "Center"、StartTime "0:0:0.5"、Duration "0:0:9"、Top "0.85"、Left "0.05"、Height "0.1"、Width "0.9"
- `imagePath`（ローカル絶対パスまたは `http://` / `https://` のURI）を `Source` 用のURIに変換する処理は、**専用メソッド `BuildImageSource(string imagePath)` に分離**する。`imagePath` がURIならそのまま使い、ローカル絶対パスなら `new Uri(imagePath).AbsoluteUri`（file:///形式）に変換する。PENCIL はURI指定時に一時フォルダへ画像をダウンロードしてから処理する前提とする

## 4. JSONファイルの出力
- 一時ファイル: `Path.Combine(Path.GetTempPath(), $"pencil_{Guid.NewGuid():N}.json")`
- **UTF-8 BOMなし**で書き込む（`new UTF8Encoding(false)`）。日本語テキストが文字化けしないこと

## 5. PENCIL の起動（PencilRenderer）
- 定数（クラス先頭）: `const string PencilExePath = @"C:\PencilSystem\RenderingApp\PencilRenderingApp.exe";`
- `System.Diagnostics.Process` で起動する
  - `UseShellExecute = false`、`CreateNoWindow = true`
  - 引数は `ArgumentList` で渡す（文字列連結しない）。引数は一時JSONの絶対パスのみ
  - 標準出力・標準エラーの両方をリダイレクトし、デッドロックを避けるため**非同期で並行して読み取る**
- `WaitForExitAsync` で待機。タイムアウトを `const` で定義（初期値10分）し、超過時はプロセスツリーごと終了（`Kill(entireProcessTree: true)`）してエラーを返す
- `CancellationToken`（MCPからのキャンセル）にも対応し、キャンセル時もプロセスを終了して一時ファイルを削除する
- 判定:
  - 終了コード0かつ `outputFileName` のファイルが実在 → 成功。出力ファイルのフルパスを含む成功メッセージを返す
  - 終了コード0でもファイルが無い → エラー（「終了コード0だが出力ファイルが見つからない」）
  - 終了コード非0 → エラー。終了コードと PENCIL の標準エラー内容（長すぎる場合は末尾を一定文字数に切り詰め）を含める
  - PENCIL の exe が存在しない → 分かりやすいエラー
- エラーは、SDK が MCP のエラー結果（isError）として Claude に返す形で通知する。未処理例外でサーバーを落とさない
- 一時JSONは `finally` で削除する

## 6. 作らないもの
- JSON Schema による検証機能、スキーマ生成機能、`validate` ツール、TCPサーバー、自前のJSON-RPC処理

# 成果物
- 上記構成のソース一式、`.csproj`、`.gitignore`（`bin/` `obj/` `.vs/` を除外）
- `README.md`: ビルド方法、`claude_desktop_config.json` への登録例（`command` はビルド済みexeのパス、パスはプレースホルダ）、MCP Inspector での動作確認手順
- 任意: `ScenarioBuilder` が生成するJSONのキー名・構造が上記仕様と一致することを確認する xUnit テスト

# 完了条件
- `dotnet build` が警告なしで通る
- コード内に標準出力への書き込みが無い
- MCP Inspector で `tools/list` に `generate_video` が表示され、3つの引数にそれぞれ説明文が付いている
