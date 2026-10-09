using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Pencil.McpServer.Services
{
	internal sealed class FtpUploadOptions
	{
		public bool Enabled { get; set; } = false;
		public string Host { get; set; } = string.Empty;
		public int Port { get; set; } = 21;
		public string Username { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public string RemotePath { get; set; } = "/uploadtest/";
		public bool UsePassive { get; set; } = true;
		public int TimeoutSeconds { get; set; } = 30;
		public int RetryCount { get; set; } = 3;
		public bool Overwrite { get; set; } = true;
		public string PublicUrlBase { get; set; } = string.Empty;
		public bool FailOnUploadError { get; set; } = true;
	}

	public sealed class FtpUploader : IFtpUploader
	{
		private readonly ILogger<FtpUploader> _logger;
		private readonly FtpUploadOptions _options;
		private static readonly Regex IdRegex = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

		public FtpUploader(IConfiguration configuration, ILogger<FtpUploader> logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_options = new FtpUploadOptions();
			var section = configuration.GetSection("FtpUpload");
			if (section.Exists())
			{
				section.Bind(_options);
			}
		}

		public async Task<string?> UploadFileAsync(string localFilePath, string id, CancellationToken cancellationToken)
		{
			if (!_options.Enabled)
			{
				_logger.LogDebug("FTP upload disabled by configuration.");
				return null;
			}

			if (string.IsNullOrWhiteSpace(id) || !IdRegex.IsMatch(id))
			{
				var message = "_ID が不正です。許可文字は英数字、ハイフン、アンダースコア、長さは 1..64 です。";
				if (_options.FailOnUploadError)
				{
					throw new InvalidOperationException(message);
				}

				_logger.LogWarning(message);
				return null;
			}

			if (!File.Exists(localFilePath))
			{
				var message = $"アップロード対象ファイルが存在しません: {localFilePath}";
				if (_options.FailOnUploadError)
				{
					throw new FileNotFoundException(message, localFilePath);
				}

				_logger.LogWarning(message);
				return null;
			}

			var targetFileName = id + ".mp4";
			var remotePath = _options.RemotePath ?? "/";
			if (!remotePath.EndsWith("/")) remotePath += "/";
			if (remotePath.StartsWith("/")) remotePath = remotePath.TrimStart('/');

			var ftpUri = new UriBuilder
			{
				Scheme = Uri.UriSchemeFtp,
				Host = _options.Host,
				Port = _options.Port,
				Path = remotePath + targetFileName
			}.Uri;

			var attempt = 0;
			Exception? lastEx = null;

			while (attempt < Math.Max(1, _options.RetryCount))
			{
				attempt++;
				try
				{
					_logger.LogInformation("Uploading {LocalFile} to FTP {Uri} (attempt {Attempt})", localFilePath, ftpUri, attempt);

					var request = (FtpWebRequest)WebRequest.Create(ftpUri);
					request.Method = WebRequestMethods.Ftp.UploadFile;
					request.UsePassive = _options.UsePassive;
					request.KeepAlive = false;
					request.Timeout = _options.TimeoutSeconds * 1000;
					request.ReadWriteTimeout = _options.TimeoutSeconds * 1000;

					if (!string.IsNullOrEmpty(_options.Username))
					{
						request.Credentials = new NetworkCredential(_options.Username, _options.Password);
					}

					// If overwrite is desired, try to delete existing file first
					if (_options.Overwrite)
					{
						try
						{
							var deleteReq = (FtpWebRequest)WebRequest.Create(ftpUri);
							deleteReq.Method = WebRequestMethods.Ftp.DeleteFile;
							deleteReq.UsePassive = _options.UsePassive;
							deleteReq.KeepAlive = false;
							if (!string.IsNullOrEmpty(_options.Username)) deleteReq.Credentials = request.Credentials;
							using var delResp = (FtpWebResponse)await deleteReq.GetResponseAsync().ConfigureAwait(false);
						}
						catch (WebException)
						{
							// ignore delete errors; file may not exist
						}
					}

					using (var fileStream = File.OpenRead(localFilePath))
					using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
					{
						await fileStream.CopyToAsync(requestStream, 81920, cancellationToken).ConfigureAwait(false);
					}

					using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
					_logger.LogInformation("FTP upload finished. Status: {StatusDescription}", response.StatusDescription);

					if (!string.IsNullOrEmpty(_options.PublicUrlBase))
					{
						var baseUrl = _options.PublicUrlBase;
						if (!baseUrl.EndsWith("/")) baseUrl += "/";
						return baseUrl + targetFileName;
					}

					return null;
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					_logger.LogWarning("FTP upload cancelled.");
					throw;
				}
				catch (Exception ex)
				{
					lastEx = ex;
					_logger.LogWarning(ex, "FTP upload attempt {Attempt} failed.", attempt);

					if (attempt >= _options.RetryCount)
					{
						break;
					}

					var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
					try
					{
						await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						throw;
					}
				}
			}

			var messageEx = new InvalidOperationException("FTP upload failed after retries.", lastEx);
			if (_options.FailOnUploadError)
			{
				_logger.LogError(lastEx, "FTP upload failed and FailOnUploadError is true.");
				throw messageEx;
			}

			_logger.LogWarning(lastEx, "FTP upload failed but FailOnUploadError is false; continuing without public URL.");
			return null;
		}
	}
}
