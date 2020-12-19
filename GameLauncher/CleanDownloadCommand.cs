namespace GameLauncher
{
	public class CleanDownloadCommand : DownloaderCommand
	{
		public CleanDownloadCommand(Downloader downloader)
			: base(downloader)
		{
		}

		public override void Execute(ICommandArgument parameters)
		{
			_downloader.StartCleanDownload((CleanDownloadCommandArgument)parameters);
		}
	}
}
