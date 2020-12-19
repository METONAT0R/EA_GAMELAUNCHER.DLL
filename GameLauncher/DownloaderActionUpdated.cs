using System;

namespace GameLauncher
{
	public delegate void DownloaderActionUpdated(long dowloadLength, long downloadCurrent, long compressedLength, string fileName, DateTime downloadPartStart);
}
