using Pencil.McpServer.Models;
using System.Text.RegularExpressions;

namespace Pencil.McpServer.Services
{
	public sealed class ScenarioBuilder
	{
		private const string TemplateValue = "Template.PencilCanvas";
		private const string ScenarioValue = "Template.xml";
		private const string OutputWidthValue = "800";
		private const string OutputHeightValue = "800";

		private const string SceneDurationValue = "0:0:10";

		private const string PictureStartTimeValue = "0:0:0";
		private const string PictureDurationValue = "0:0:10";
		private const string PictureTopValue = "0.1";
		private const string PictureLeftValue = "0.1";
		private const string PictureHeightValue = "0.8";
		private const string PictureWidthValue = "0.8";

		private const string TextFontSizeValue = "0.05";
		private const string TextAlignmentValue = "Center";
		private const string TextStartTimeValue = "0:0:0.5";
		private const string TextDurationValue = "0:0:9";
		private const string TextTopValue = "0.85";
		private const string TextLeftValue = "0.05";
		private const string TextHeightValue = "0.1";
		private const string TextWidthValue = "0.9";

		private static readonly Regex IdRegex = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

		public PencilCanvasRoot Build(string text, string imagePath, string outputFileName, string? id = null)
		{
			var root = new PencilCanvasRoot
			{
				LocalOutputFileName = outputFileName,
				Template = TemplateValue,
				Scenario = ScenarioValue,
				OutputWidth = OutputWidthValue,
				OutputHeight = OutputHeightValue,
				Sequence = new List<SceneElementBase>
				{
					new Scene
					{
						Duration = SceneDurationValue,
						Layers = new List<LayerBase>
						{
							new PictureLayer
							{
								Source = BuildImageSource(imagePath),
								StartTime = PictureStartTimeValue,
								Duration = PictureDurationValue,
								Top = PictureTopValue,
								Left = PictureLeftValue,
								Height = PictureHeightValue,
								Width = PictureWidthValue
							},
							new TextLayer
							{
								Text = text,
								FontSize = TextFontSizeValue,
								TextAlignment = TextAlignmentValue,
								StartTime = TextStartTimeValue,
								Duration = TextDurationValue,
								Top = TextTopValue,
								Left = TextLeftValue,
								Height = TextHeightValue,
								Width = TextWidthValue
							}
						}
					}
				}
			};

			// 明示的な id が与えられていれば優先して設定する。与えられていない場合は出力ファイル名から推定する。
			if (!string.IsNullOrWhiteSpace(id))
			{
				if (IdRegex.IsMatch(id))
				{
					root.Id = id;
				}
				else
				{
					throw new ArgumentException("_ID の形式が不正です。許可文字は英数字、ハイフン、アンダースコア、長さは 1..64 の範囲です。", nameof(id));
				}
			}
			else
			{
				try
				{
					var fileName = Path.GetFileNameWithoutExtension(outputFileName) ?? string.Empty;
					if (IdRegex.IsMatch(fileName))
					{
						root.Id = fileName;
					}
				}
				catch
				{
					// 設定不能な場合は _ID を設定しない
				}
			}

			return root;
		}

		public static string BuildImageSource(string imagePath)
		{
			if (string.IsNullOrWhiteSpace(imagePath))
			{
				throw new ArgumentException("imagePath is required", nameof(imagePath));
			}

			if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) &&
				(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
			{
				// オンライン画像の URL をそのまま使用
				return imagePath;
			}

			if (Path.IsPathFullyQualified(imagePath))
			{
				// ローカル絶対パスは file:/// 形式に変換
				return new Uri(imagePath).AbsoluteUri;
			}

			throw new ArgumentException("imagePath must be an absolute local path or an absolute http/https URL.", nameof(imagePath));
		}
	}
}
