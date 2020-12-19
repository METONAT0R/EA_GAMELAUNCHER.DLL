namespace GameLauncher
{
	public class CleanDownloadCommandArgument : ICommandArgument
	{
		public string ServerPath
		{
			get;
			set;
		}

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

		public bool UseIndexCache
		{
			get;
			set;
		}

		public CleanDownloadCommandArgument(string serverPath, string package, string patchPath, bool useIndexCache)
		{
			ServerPath = serverPath;
			Package = package;
			PatchPath = patchPath;
			UseIndexCache = useIndexCache;
		}
	}
}
