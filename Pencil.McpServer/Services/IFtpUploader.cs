using System.Threading;
using System.Threading.Tasks;

namespace Pencil.McpServer.Services
{
	public interface IFtpUploader
	{
		/// <summary>
		/// Upload local file to FTP server using specified id as target filename (id + ".mp4").
		/// Returns the public URL if configured, or null when upload is disabled or no public URL configured.
		/// May throw when upload fails and configuration requires failure to surface.
		/// </summary>
		Task<string?> UploadFileAsync(string localFilePath, string id, CancellationToken cancellationToken);
	}
}
