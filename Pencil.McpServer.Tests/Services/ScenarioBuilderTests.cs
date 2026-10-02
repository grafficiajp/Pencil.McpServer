using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pencil.McpServer.Models;
using Pencil.McpServer.Services;
using Xunit;

namespace Pencil.McpServer.Tests.Services;

public sealed class ScenarioBuilderTests
{
	[Fact]
	public void Build_SerializesRequiredSpecificationKeys()
	{
		var builder = new ScenarioBuilder();
		var outputFileName = @"C:\Temp\output.mp4";
		var imagePath = @"C:\Temp\image.png";

		var scenario = builder.Build("テスト文字列", imagePath, outputFileName);
		var json = JsonConvert.SerializeObject(scenario, PencilJsonDefaults.SerializerSettings);
		var root = JObject.Parse(json);

		Assert.Equal(outputFileName, (string?)root["_LocalOutputFileName"]);
		Assert.Equal("800", (string?)root["_root.OutputWidth"]);

		var sequence = Assert.IsType<JArray>(root["Sequence"]);
		var scene = Assert.IsType<JObject>(sequence.First);
		Assert.Equal("Scene", (string?)scene["Overlay.Name"]);

		var layers = Assert.IsType<JArray>(scene["Layers"]);
		var pictureLayer = Assert.IsType<JObject>(layers[0]);
		var textLayer = Assert.IsType<JObject>(layers[1]);
		Assert.Equal("PictureLayer", (string?)pictureLayer["Overlay.Name"]);
		Assert.Equal("TextLayer", (string?)textLayer["Overlay.Name"]);
	}
}
