namespace GameLauncher
{
	public class DeleteCommand : DownloaderCommand
	{
		public DeleteCommand(Downloader downloader)
			: base(downloader)
		{
		}

		public override void Execute(ICommandArgument parameters)
		{
			_downloader.StartDelete((DeleteCommandArgument)parameters);
		}
	}
}
