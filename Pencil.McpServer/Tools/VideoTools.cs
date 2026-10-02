using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pencil.McpServer.Services;

namespace Pencil.McpServer.Tools;

[McpServerToolType]
public sealed class VideoTools(
	ScenarioBuilder scenarioBuilder,
	PencilRenderer pencilRenderer)
{
	[McpServerTool(Name = "generate_video")]
	[Description("ローカルの画像とテキストから PENCIL を実行して mp4 動画を生成します。text は空文字不可、imagePath は存在するローカル絶対パス（png/jpg/jpeg）、outputFileName は .mp4 のローカル絶対パスです。処理には数十秒〜数分かかる場合があります。成功時は生成された動画ファイルのフルパスを含むメッセージを返します。")]
	public async Task<string> GenerateVideo(
		[Description("動画下部に表示する文字列。空文字や空白のみは不可です。")]
		string text,
		[Description("使用する画像ファイルのローカル絶対パス。拡張子は .png / .jpg / .jpeg のみ対応します。")]
		string imagePath,
		[Description("出力する mp4 ファイルのローカル絶対パス。拡張子は .mp4 必須で、出力先フォルダーが無い場合は作成されます。同名ファイルが既にある場合は削除して上書きします。")]
		string outputFileName,
		CancellationToken cancellationToken = default)
	{
		ValidateText(text);
		ValidateImagePath(imagePath);
		EnsureOutputPath(outputFileName);

		var scenario = scenarioBuilder.Build(text, imagePath, outputFileName);
		return await pencilRenderer.RenderAsync(scenario, outputFileName, cancellationToken).ConfigureAwait(false);
	}

	private static void ValidateText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new McpException("text は空にできません。表示する文字列を指定してください。");
		}
	}

	private static void ValidateImagePath(string imagePath)
	{
		if (string.IsNullOrWhiteSpace(imagePath))
		{
			throw new McpException("imagePath は必須です。");
		}

		if (!Path.IsPathFullyQualified(imagePath))
		{
			throw new McpException("imagePath はローカルの絶対パスで指定してください。");
		}

		if (!File.Exists(imagePath))
		{
			throw new McpException($"imagePath のファイルが見つかりません: {imagePath}");
		}

		var extension = Path.GetExtension(imagePath);
		var isSupported = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
			|| extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
			|| extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

		if (!isSupported)
		{
			throw new McpException("imagePath の拡張子は .png / .jpg / .jpeg のみ対応しています。");
		}
	}

	private static void EnsureOutputPath(string outputFileName)
	{
		if (string.IsNullOrWhiteSpace(outputFileName))
		{
			throw new McpException("outputFileName は必須です。");
		}

		if (!Path.IsPathFullyQualified(outputFileName))
		{
			throw new McpException("outputFileName はローカルの絶対パスで指定してください。");
		}

		var extension = Path.GetExtension(outputFileName);
		if (!extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
		{
			throw new McpException("outputFileName の拡張子は .mp4 のみ対応しています。");
		}

		var directoryPath = Path.GetDirectoryName(outputFileName);
		if (string.IsNullOrWhiteSpace(directoryPath))
		{
			throw new McpException("outputFileName の出力先フォルダーを解決できません。");
		}

		Directory.CreateDirectory(directoryPath);
	}
}

