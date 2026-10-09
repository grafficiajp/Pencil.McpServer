# Pencil.McpServer

PENCIL CLI をラップして `generate_video` ツールを提供する .NET 10 / stdio MCP サーバーです。

## ビルド

```powershell
dotnet build .\Pencil.McpServer.slnx
```

## テスト

テストプロジェクトの内容と使い方は [Pencil.McpServer.Tests/Readme.md](../Pencil.McpServer.Tests/Readme.md) を参照してください。

## 実行

```powershell
dotnet run --project .\Pencil.McpServer\Pencil.McpServer.csproj
```

## claude_desktop_config.json 登録例

`command` はビルド済み exe のフルパスに置き換えてください。

```json
{
  "mcpServers": {
	"pencil": {
	  "command": "<ABSOLUTE_PATH_TO>\\Pencil.McpServer.exe",
	  "args": []
	}
  }
}
```

## MCP Inspector 動作確認

1. サーバーを stdio で起動可能な状態にする。
2. MCP Inspector で stdio 接続を作成し、`command` に `dotnet`、`args` に `run --project <csprojの絶対パス>` を設定する。
3. 接続後に `tools/list` を実行し、`generate_video` が表示されることを確認する。
4. `generate_video` を実行し、`text` / `imagePath` を与えて動画生成を確認する。
   - サーバー側で GUID を自動生成して `_ID` として付与し、出力先は `C:\\PencilOut\\<GUID>.mp4` に固定される。
   - FTP アップロード成功時は、結果としてアップロード済みファイルの公開 URL のみを返す。
   - FTP アップロード URL を取得できない場合は、従来どおり出力ファイルパスを含むメッセージを返す。
