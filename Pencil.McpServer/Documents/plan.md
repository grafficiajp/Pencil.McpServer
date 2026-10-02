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
  - `outputFileName` (string): 出力する mp4 のフルパス（絶対パス、拡張子 .mp4）
- 説明文は Claude が使い方を判断できるよう、「何をするか」「各引数の意味と制約」「処理に時間がかかること」「成功時は出力ファイルのフルパスを返すこと」を具体的に書く
- 引数チェック（失敗時は原因が分かるメッセージで例外を投げる）:
  - `text` が空でない
  - `imagePath` が絶対パスで、ファイルが存在し、拡張子が対応形式
  - `outputFileName` が絶対パスで拡張子が .mp4。出力先フォルダが無ければ作成する

# PENCIL が受け付ける JSON 仕様（これが正）
PENCIL に渡すのは次の構造の JSON ファイル。**値はすべて文字列**で、数値も `"0.8"` のように文字列で出力する。
時間は `"0:0:10"` / `"0:0:0.5"` 形式（時:分:秒）。

```jsonc
{
  "_LocalOutputFileName": "c:/output.mp4",        // 出力mp4のパス
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
`generate_video` の3引数から、次の固定構成を作る。固定値は `const` で名前を付けてクラス先頭にまとめる。
- トップ: `_LocalOutputFileName` = `outputFileName`、`_Template` = "Template.PencilCanvas"、`_Scenario` = "Template.xml"、`_root.OutputWidth` / `_root.OutputHeight` = "800"
- Sequence は `Scene` を1つ（Duration "0:0:10"）
  - PictureLayer: `Source` = 画像のURI、StartTime "0:0:0"、Duration "0:0:10"、Top/Left "0.1"、Height/Width "0.8"
  - TextLayer: `Text` = `text`、FontSize "0.05"、TextAlignment "Center"、StartTime "0:0:0.5"、Duration "0:0:9"、Top "0.85"、Left "0.05"、Height "0.1"、Width "0.9"
- `imagePath`（ローカル絶対パス）を `Source` 用のURIに変換する処理は、**専用メソッド `BuildImageSource(string absolutePath)` に分離**する。初期実装は `new Uri(absolutePath).AbsoluteUri`（file:///形式）とし、「PENCIL側の受け付け形式に合わせて要確認」とコメントを残す

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
