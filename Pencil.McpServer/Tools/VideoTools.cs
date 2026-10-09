using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pencil.McpServer.Services;
using System.IO;
using System;

namespace Pencil.McpServer.Tools
{
	[McpServerToolType]
	public sealed class VideoTools
	{
		private const string OutputDirectory = @"C:\PencilOut";

		private readonly ScenarioBuilder _scenarioBuilder;
		private readonly PencilRenderer _pencilRenderer;

		public VideoTools(ScenarioBuilder scenarioBuilder, PencilRenderer pencilRenderer)
		{
			_scenarioBuilder = scenarioBuilder ?? throw new ArgumentNullException(nameof(scenarioBuilder));
			_pencilRenderer = pencilRenderer ?? throw new ArgumentNullException(nameof(pencilRenderer));
		}

		[McpServerTool(Name = "generate_video")]
		[Description("ローカルの画像とテキストから PENCIL を実行して mp4 動画を生成します。text は空文字不可、imagePath はローカル絶対パスまたは http/https の画像 URLです。出力先は c:/PencilOut 固定で、_ID はサーバー側で自動採番されます。処理には数十秒〜数分かかる場合があります。成功時は https://grafficia.xsrv.jp/uploadtest/ 配下の動画URLを返します。")]
		public async Task<string> GenerateVideo(
			[Description("動画下部に表示する文字列。空文字や空白のみは不可です。")]
			string text,
			[Description("使用する画像ファイルのローカル絶対パス、または http/https の画像 URL。拡張子は .png / .jpg / .jpeg が推奨されます。")]
			string imagePath,
			CancellationToken cancellationToken = default)
		{
			ValidateText(text);
			ValidateImagePath(imagePath);

			var id = Guid.NewGuid().ToString("N");
			var outputFileName = BuildOutputFileName(id);
			var scenario = _scenarioBuilder.Build(text, imagePath, outputFileName, id);
			return await _pencilRenderer.RenderAsync(scenario, outputFileName, cancellationToken).ConfigureAwait(false);
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

			// http/https の URL を許容
			if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) &&
				(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
			{
				var ext = Path.GetExtension(uri.AbsolutePath);
				if (!IsSupportedImageExtension(ext))
				{
					throw new McpException("imagePath の拡張子は .png / .jpg / .jpeg のみ対応しています。");
				}

				return;
			}

			if (!Path.IsPathFullyQualified(imagePath))
			{
				throw new McpException("imagePath はローカルの絶対パス、または http/https の URL で指定してください。");
			}

			if (!File.Exists(imagePath))
			{
				throw new McpException($"imagePath のファイルが見つかりません: {imagePath}");
			}

			var extension = Path.GetExtension(imagePath);
			if (!IsSupportedImageExtension(extension))
			{
				throw new McpException("imagePath の拡張子は .png / .jpg / .jpeg のみ対応しています。");
			}
		}

		private static bool IsSupportedImageExtension(string? extension)
		{
			if (string.IsNullOrEmpty(extension)) return false;
			return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
		}

		private static string BuildOutputFileName(string id)
		{
			Directory.CreateDirectory(OutputDirectory);
			return Path.Combine(OutputDirectory, id + ".mp4");
		}
	}
}

