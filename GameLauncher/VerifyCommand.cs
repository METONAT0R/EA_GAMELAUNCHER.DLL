namespace GameLauncher
{
	public class VerifyCommand : DownloaderCommand
	{
		public VerifyCommand(Downloader downloader)
			: base(downloader)
		{
		}

		public override void Execute(ICommandArgument parameters)
		{
			_downloader.StartVerificationAndDownload((VerifyCommandArgument)parameters);
		}
	}
}
