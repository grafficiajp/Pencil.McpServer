# Pencil.McpServer.Tests

## 概要
`Pencil.McpServer.Tests` は、`Pencil.McpServer` のユニットテスト用プロジェクトです。現在は `ScenarioBuilder` が生成するシナリオ JSON の構造・必須キー・レイヤー構成を検証します。

## 何をテストしているか
`Services/ScenarioBuilderTests.cs` の `Build_SerializesRequiredSpecificationKeys` で、主に以下を確認しています。

- 出力先ファイル名 (`_LocalOutputFileName`) が正しく入る
- 出力幅 (`_root.OutputWidth`) が期待値 (`"800"`) になる
- `Sequence` の先頭要素が `Scene` である
- `Layers` に `PictureLayer` と `TextLayer` が順序通り含まれる

このテストにより、`ScenarioBuilder.Build(...)` の生成結果が想定仕様を満たすかを回帰確認できます。

## 使用技術
- .NET 10 (`net10.0`)
- xUnit
- Microsoft.NET.Test.Sdk
- Newtonsoft.Json（JSON シリアライズ／検証用）

## 使い方
ソリューションルート（`Pencil.McpServer`）で実行します。

### 1) テストプロジェクトのみ実行
```powershell
dotnet test .\Pencil.McpServer.Tests\Pencil.McpServer.Tests.csproj
```

### 2) ソリューション全体のテスト実行
```powershell
dotnet test .\Pencil.McpServer.slnx
```

### 3) 特定テストのみ実行
```powershell
dotnet test .\Pencil.McpServer.Tests\Pencil.McpServer.Tests.csproj --filter "FullyQualifiedName~ScenarioBuilderTests.Build_SerializesRequiredSpecificationKeys"
```

## 追加時の方針
- `Pencil.McpServer` 側の公開 API（例: `ScenarioBuilder`）を対象に、振る舞いベースでテストを追加する
- JSON のフィールド名は `PencilCanvasModels` の `JsonProperty` 設定に合わせて検証する
- 既存テスト名に合わせて `メソッド名_期待結果` 形式で命名する
