using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using Newtonsoft.Json;
using Pencil.McpServer.Models;

namespace Pencil.McpServer.Services;

public sealed class PencilRenderer(ILogger<PencilRenderer> logger)
{
	private const string PencilExePath = @"C:\PencilSystem\RenderingApp\PencilRenderingApp.exe";
	private const int RenderTimeoutMinutes = 10;
	private const int MaxErrorTailLength = 2000;

	public async Task<string> RenderAsync(PencilCanvasRoot scenario, string outputFileName, CancellationToken cancellationToken)
	{
		if (!File.Exists(PencilExePath))
		{
			throw new McpException($"PENCIL 実行ファイルが見つかりません: {PencilExePath}");
		}

		var tempJsonPath = Path.Combine(Path.GetTempPath(), $"pencil_{Guid.NewGuid():N}.json");

		try
		{
			var json = JsonConvert.SerializeObject(scenario, PencilJsonDefaults.SerializerSettings);
			await File.WriteAllTextAsync(tempJsonPath, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

			using var process = CreateProcess(tempJsonPath);
			EnsureOutputFileDeleted(outputFileName);

			if (!process.Start())
			{
				throw new McpException("PENCIL の起動に失敗しました。");
			}

			var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
			var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(RenderTimeoutMinutes));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
			{
				KillProcessTree(process);
				throw new McpException($"PENCIL の実行がタイムアウトしました（{RenderTimeoutMinutes}分）。");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				KillProcessTree(process);
				throw;
			}

			await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
			var stdout = await stdoutTask.ConfigureAwait(false);
			var stderr = await stderrTask.ConfigureAwait(false);

			if (process.ExitCode == 0)
			{
				if (!File.Exists(outputFileName))
				{
					throw new McpException("PENCIL は終了コード 0 で完了しましたが、出力ファイルが見つかりません。");
				}

				logger.LogInformation("PENCIL rendering completed successfully. Output: {OutputFileName}", outputFileName);
				return $"動画生成が完了しました。出力ファイル: {outputFileName}";
			}

			var errorSource = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
			var errorTail = GetTail(errorSource, MaxErrorTailLength);
			throw new McpException($"PENCIL の実行に失敗しました。終了コード: {process.ExitCode}。詳細: {errorTail}");
		}
		finally
		{
			TryDeleteTempFile(tempJsonPath);
		}
	}

	private static Process CreateProcess(string tempJsonPath)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = PencilExePath,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};

		startInfo.ArgumentList.Add(tempJsonPath);

		return new Process
		{
			StartInfo = startInfo
		};
	}

	private static void KillProcessTree(Process process)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch
		{
			// kill 処理の失敗は元例外を優先する
		}
	}

	private static string GetTail(string value, int maxLength)
	{
		if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
		{
			return value;
		}

		return value[^maxLength..];
	}

	private static void EnsureOutputFileDeleted(string outputFileName)
	{
		if (!File.Exists(outputFileName))
		{
			return;
		}

		try
		{
			File.Delete(outputFileName);
		}
		catch (Exception ex)
		{
			throw new McpException($"既存の出力ファイルを削除できませんでした: {outputFileName}", ex);
		}
	}

	private void TryDeleteTempFile(string tempJsonPath)
	{
		try
		{
			if (File.Exists(tempJsonPath))
			{
				File.Delete(tempJsonPath);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "一時ファイル削除に失敗しました: {TempJsonPath}", tempJsonPath);
		}
	}
}
