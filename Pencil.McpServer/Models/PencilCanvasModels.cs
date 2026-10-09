using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pencil.McpServer.Models
{
	public sealed class PencilCanvasRoot
	{
		[JsonProperty("_ID", Order = 0, NullValueHandling = NullValueHandling.Ignore)]
		public string? Id { get; set; }

		[JsonProperty("_LocalOutputFileName", Order = 1)]
		public string LocalOutputFileName { get; set; } = string.Empty;

		[JsonProperty("_Template", Order = 2)]
		public string Template { get; set; } = string.Empty;

		[JsonProperty("_Scenario", Order = 3)]
		public string Scenario { get; set; } = string.Empty;

		[JsonProperty("_root.OutputWidth", Order = 4)]
		public string OutputWidth { get; set; } = string.Empty;

		[JsonProperty("_root.OutputHeight", Order = 5)]
		public string OutputHeight { get; set; } = string.Empty;

		[JsonProperty("Sequence", Order = 6)]
		public List<SceneElementBase> Sequence { get; set; } = new List<SceneElementBase>();
	}

	public abstract class SceneElementBase
	{
		[JsonProperty("Overlay.Name", Order = 1)]
		public abstract string OverlayName { get; }

		[JsonProperty("Duration", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
		public string? Duration { get; set; }
	}

	public sealed class Scene : SceneElementBase
	{
		public const string OverlayNameValue = "Scene";

		public override string OverlayName => OverlayNameValue;

		[JsonProperty("Layers", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
		public List<LayerBase> Layers { get; set; } = new List<LayerBase>();
	}

	public sealed class EmptyScene : SceneElementBase
	{
		public const string OverlayNameValue = "EmptyScene";

		public override string OverlayName => OverlayNameValue;
	}

	public abstract class LayerBase
	{
		[JsonProperty("Overlay.Name", Order = 1)]
		public abstract string OverlayName { get; }
	}

	public sealed class PictureLayer : LayerBase
	{
		public const string OverlayNameValue = "PictureLayer";

		public override string OverlayName => OverlayNameValue;

		[JsonProperty("Source", Order = 2)]
		public string Source { get; set; } = string.Empty;

		[JsonProperty("StartTime", Order = 3)]
		public string StartTime { get; set; } = string.Empty;

		[JsonProperty("Duration", Order = 4)]
		public string Duration { get; set; } = string.Empty;

		[JsonProperty("Top", Order = 5)]
		public string Top { get; set; } = string.Empty;

		[JsonProperty("Left", Order = 6)]
		public string Left { get; set; } = string.Empty;

		[JsonProperty("Height", Order = 7)]
		public string Height { get; set; } = string.Empty;

		[JsonProperty("Width", Order = 8)]
		public string Width { get; set; } = string.Empty;
	}

	public sealed class TextLayer : LayerBase
	{
		public const string OverlayNameValue = "TextLayer";

		public override string OverlayName => OverlayNameValue;

		[JsonProperty("Text", Order = 2)]
		public string Text { get; set; } = string.Empty;

		[JsonProperty("FontSize", Order = 3)]
		public string FontSize { get; set; } = string.Empty;

		[JsonProperty("TextAlignment", Order = 4)]
		public string TextAlignment { get; set; } = string.Empty;

		[JsonProperty("StartTime", Order = 5)]
		public string StartTime { get; set; } = string.Empty;

		[JsonProperty("Duration", Order = 6)]
		public string Duration { get; set; } = string.Empty;

		[JsonProperty("Top", Order = 7)]
		public string Top { get; set; } = string.Empty;

		[JsonProperty("Left", Order = 8)]
		public string Left { get; set; } = string.Empty;

		[JsonProperty("Height", Order = 9)]
		public string Height { get; set; } = string.Empty;

		[JsonProperty("Width", Order = 10)]
		public string Width { get; set; } = string.Empty;
	}

	public static class PencilJsonDefaults
	{
		public static readonly JsonSerializerSettings SerializerSettings = new()
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.Indented
		};
	}
}
