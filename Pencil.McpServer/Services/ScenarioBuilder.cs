using Pencil.McpServer.Models;

namespace Pencil.McpServer.Services;

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

	public PencilCanvasRoot Build(string text, string imagePath, string outputFileName)
	{
		return new PencilCanvasRoot
		{
			LocalOutputFileName = outputFileName,
			Template = TemplateValue,
			Scenario = ScenarioValue,
			OutputWidth = OutputWidthValue,
			OutputHeight = OutputHeightValue,
			Sequence =
			[
				new Scene
				{
					Duration = SceneDurationValue,
					Layers =
					[
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
					]
				}
			]
		};
	}

	private static string BuildImageSource(string absolutePath)
	{
		// NOTE: PENCIL 側が受け付ける Source 形式（file URI か別形式か）は実機で要確認。
		return new Uri(absolutePath).AbsoluteUri;
	}
}
