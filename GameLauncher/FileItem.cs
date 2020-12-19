namespace GameLauncher
{
	internal class FileItem
	{
		public string Path
		{
			get;
			set;
		}

		public string File
		{
			get;
			set;
		}

		public string Hash
		{
			get;
			set;
		}

		public int FromSection
		{
			get;
			set;
		}

		public int ToSection
		{
			get;
			set;
		}

		public int Offset
		{
			get;
			set;
		}

		public int Length
		{
			get;
			set;
		}

		public int Compressed
		{
			get;
			set;
		}

		public FileItem(string path, string file, string hash, int fromSection, int toSection, int offset, int length, int compressed)
		{
			Path = path;
			File = file;
			Hash = hash;
			FromSection = fromSection;
			ToSection = toSection;
			Offset = offset;
			Length = length;
			Compressed = compressed;
		}
	}
}
