namespace GameLauncher
{
	public class ScheduleBuildCommandArgument : ICommandArgument
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

		public ScheduleBuildCommandArgument(string serverPath, string package, string patchPath)
		{
			ServerPath = serverPath;
			Package = package;
			PatchPath = patchPath;
		}
	}
}
