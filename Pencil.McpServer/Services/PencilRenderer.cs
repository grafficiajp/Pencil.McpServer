using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using Newtonsoft.Json;
using Pencil.McpServer.Models;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Pencil.McpServer.Services;

public sealed class PencilRenderer
{
	private const string PencilExePath = @"C:\PencilSystem\RenderingApp\PencilRenderingApp.exe";
	private const string FtpSettingConfigPath = @"C:\PencilSystem\UploadSetting\FtpSetting.config";
	private const int RenderTimeoutMinutes = 10;
	private const int MaxErrorTailLength = 2000;

	private readonly ILogger<PencilRenderer> _logger;
	private readonly IFtpUploader _ftpUploader;

	public PencilRenderer(ILogger<PencilRenderer> logger, IFtpUploader ftpUploader)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_ftpUploader = ftpUploader ?? throw new ArgumentNullException(nameof(ftpUploader));
	}

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

				_logger.LogInformation("PENCIL rendering completed successfully. Output: {OutputFileName}", outputFileName);

				// FTP アップロード処理（_ID が設定されていれば実行）
				string? publicUrl = null;
				try
				{
					if (!string.IsNullOrWhiteSpace(scenario?.Id))
					{
						publicUrl = await _ftpUploader.UploadFileAsync(outputFileName, scenario.Id, cancellationToken).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					// FtpUploader が FailOnUploadError=true の時は例外が来るためここに到達する。
					throw new McpException($"アップロードに失敗しました: {ex.Message}", ex);
				}

				var convertedFtpUrl = TryGetHttpsUrlFromFtpSetting();
				if (!string.IsNullOrWhiteSpace(convertedFtpUrl) && !string.IsNullOrWhiteSpace(scenario?.Id))
				{
					convertedFtpUrl = CombineUrl(convertedFtpUrl, scenario.Id + ".mp4");
				}

				if (!string.IsNullOrWhiteSpace(publicUrl) && !string.IsNullOrWhiteSpace(convertedFtpUrl))
				{
					return $"動画生成が完了しました。出力ファイル: {outputFileName}。公開 URL: {publicUrl}。利用 URL: {convertedFtpUrl}";
				}

				if (!string.IsNullOrWhiteSpace(publicUrl))
				{
					return $"動画生成が完了しました。出力ファイル: {outputFileName}。公開 URL: {publicUrl}";
				}

				if (!string.IsNullOrWhiteSpace(convertedFtpUrl))
				{
					return $"動画生成が完了しました。出力ファイル: {outputFileName}。利用 URL: {convertedFtpUrl}";
				}

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

	private static string? TryGetHttpsUrlFromFtpSetting()
	{
		if (!File.Exists(FtpSettingConfigPath))
		{
			return null;
		}

		try
		{
			var content = File.ReadAllText(FtpSettingConfigPath);
			var rawUrl = TryExtractFtpServerUrl(content);
			if (string.IsNullOrWhiteSpace(rawUrl))
			{
				return null;
			}

			return ToHttpsUrl(rawUrl);
		}
		catch
		{
			return null;
		}
	}

	private static string? TryExtractFtpServerUrl(string content)
	{
		var xmlMatch = Regex.Match(content, "<add\\s+key=[\"']UP_FtpServerURL[\"']\\s+value=[\"'](?<v>[^\"']+)[\"']", RegexOptions.IgnoreCase);
		if (xmlMatch.Success)
		{
			return xmlMatch.Groups["v"].Value.Trim();
		}

		var pairMatch = Regex.Match(content, "UP_FtpServerURL\\s*[:=]\\s*[\"']?(?<v>[^\"'\\r\\n]+)", RegexOptions.IgnoreCase);
		if (pairMatch.Success)
		{
			return pairMatch.Groups["v"].Value.Trim();
		}

		return null;
	}

	private static string ToHttpsUrl(string value)
	{
		var trimmed = value.Trim();
		if (trimmed.StartsWith("//", StringComparison.Ordinal))
		{
			trimmed = "https:" + trimmed;
		}
		else if (!trimmed.Contains("://", StringComparison.Ordinal))
		{
			trimmed = "https://" + trimmed.TrimStart('/');
		}
		else if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
		{
			trimmed = new UriBuilder(uri)
			{
				Scheme = Uri.UriSchemeHttps,
				Port = -1
			}.Uri.ToString();
		}

		return trimmed.TrimEnd('/');
	}

	private static string CombineUrl(string baseUrl, string relative)
	{
		return $"{baseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";
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
			_logger.LogWarning(ex, "一時ファイル削除に失敗しました: {TempJsonPath}", tempJsonPath);
		}
	}
}
