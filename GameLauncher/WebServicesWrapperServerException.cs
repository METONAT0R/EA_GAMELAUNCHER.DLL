using System;
using System.Runtime.Serialization;

namespace GameLauncher
{
	[Serializable]
	public class WebServicesWrapperServerException : Exception
	{
		private int _errorCode;

		public int ErrorCode => _errorCode;

		public WebServicesWrapperServerException(int errorCode)
		{
			_errorCode = errorCode;
		}

		public WebServicesWrapperServerException(int errorCode, string message)
			: base(message)
		{
			_errorCode = errorCode;
		}

		public WebServicesWrapperServerException(int errorCode, string message, Exception innerException)
			: base(message, innerException)
		{
			_errorCode = errorCode;
		}

		protected WebServicesWrapperServerException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}
