using System;
using System.Runtime.Serialization;

namespace GameLauncher
{
	[Serializable]
	public class VerificationException : Exception
	{
		private ulong mSizeRequired;

		private ulong mCompressedSizeRequired;

		private string mLanguagePackage;

		public ulong SizeRequired => mSizeRequired;

		public ulong CompressedSizeRequired => mCompressedSizeRequired;

		public string LanguagePackage => mLanguagePackage;

		public VerificationException(ulong sizeRequired, ulong CompressedSizeRequired, string languagePackage)
		{
			mSizeRequired = sizeRequired;
			mCompressedSizeRequired = CompressedSizeRequired;
			mLanguagePackage = languagePackage;
		}

		public VerificationException(ulong sizeRequired, ulong compessedSizeRequired, string languagePackage, string message)
			: base(message)
		{
			mSizeRequired = sizeRequired;
			mCompressedSizeRequired = CompressedSizeRequired;
			mLanguagePackage = languagePackage;
		}

		public VerificationException(ulong sizeRequired, ulong compessedSizeRequired, string languagePackage, string message, Exception innerException)
			: base(message, innerException)
		{
			mSizeRequired = sizeRequired;
			mCompressedSizeRequired = CompressedSizeRequired;
			mLanguagePackage = languagePackage;
		}

		protected VerificationException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}
