namespace GameLauncher
{
	public class DownloadCommandArgument : ICommandArgument
	{
		public string Package
		{
			get;
			set;
		}

		public string PatchPath
		{
			get;
			set;
		}

		public DownloadCommandArgument(string package, string patchPath)
		{
			Package = package;
			PatchPath = patchPath;
		}
	}
}
