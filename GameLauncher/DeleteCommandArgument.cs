namespace GameLauncher
{
	public class DeleteCommandArgument : ICommandArgument
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

		public DeleteCommandArgument(string serverPath, string package, string patchPath)
		{
			ServerPath = serverPath;
			Package = package;
			PatchPath = patchPath;
		}
	}
}
