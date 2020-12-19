namespace GameLauncher
{
	public class VerifyCommandArgument : ICommandArgument
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

		public bool StopOnFail
		{
			get;
			set;
		}

		public bool ClearHashes
		{
			get;
			set;
		}

		public bool WriteHashes
		{
			get;
			set;
		}

		public bool Download
		{
			get;
			set;
		}

		public VerifyCommandArgument(string serverPath, string package, string patchPath, bool stopOnFail, bool clearHashes, bool writeHashes, bool download)
		{
			ServerPath = serverPath;
			Package = package;
			PatchPath = patchPath;
			StopOnFail = stopOnFail;
			ClearHashes = clearHashes;
			WriteHashes = writeHashes;
			Download = download;
		}
	}
}
