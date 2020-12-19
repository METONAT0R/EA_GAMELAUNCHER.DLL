namespace GameLauncher
{
	public abstract class DownloaderCommand
	{
		protected Downloader _downloader;

		public Downloader Downloader => _downloader;

		protected DownloaderCommand(Downloader downloader)
		{
			_downloader = downloader;
		}

		public abstract void Execute(ICommandArgument parameters);
	}
}
