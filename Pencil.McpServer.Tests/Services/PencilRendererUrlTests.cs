using System.Reflection;
using Pencil.McpServer.Services;
using Xunit;

namespace Pencil.McpServer.Tests.Services;

public sealed class PencilRendererUrlTests
{
	[Fact]
	public void BuildPublicUrlFromOutputFile_ReturnsExpectedPublicUrl()
	{
		var method = typeof(PencilRenderer).GetMethod("BuildPublicUrlFromOutputFile", BindingFlags.NonPublic | BindingFlags.Static);
		var actual = Assert.IsType<string>(method!.Invoke(null, new object[] { @"C:\PencilOut\sample.mp4" }));

		Assert.Equal("https://grafficia.xsrv.jp/uploadtest/sample.mp4", actual);
	}
}
