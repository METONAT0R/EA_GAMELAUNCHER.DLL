using System;
using System.Runtime.Serialization;

namespace GameLauncher
{
	[Serializable]
	public class HashManagerException : Exception
	{
		public HashManagerException()
		{
		}

		public HashManagerException(string message)
			: base(message)
		{
		}

		public HashManagerException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		protected HashManagerException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}
