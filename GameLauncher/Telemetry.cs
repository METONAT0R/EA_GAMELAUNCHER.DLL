using System.ComponentModel;

namespace GameLauncher
{
	public class Telemetry
	{
		private SendTelemetry mSendTelemetry;

		private ISynchronizeInvoke mFE;

		public SendTelemetry SendTelemetry
		{
			get
			{
				return mSendTelemetry;
			}
			set
			{
				mSendTelemetry = value;
			}
		}

		public Telemetry(ISynchronizeInvoke fe)
		{
			mFE = fe;
		}

		public void Call(string action)
		{
			mFE.BeginInvoke(mSendTelemetry, new object[1]
			{
				action
			});
		}
	}
}
