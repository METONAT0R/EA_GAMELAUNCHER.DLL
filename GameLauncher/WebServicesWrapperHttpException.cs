using System;
using System.Runtime.Serialization;

namespace GameLauncher
{
	[Serializable]
	public class WebServicesWrapperHttpException : Exception
	{
		public WebServicesWrapperHttpException()
		{
		}

		public WebServicesWrapperHttpException(string message)
			: base(message)
		{
		}

		public WebServicesWrapperHttpException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		protected WebServicesWrapperHttpException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}
