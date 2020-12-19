using System;
using System.Runtime.InteropServices;

namespace GameLauncher
{
	internal static class UnsafeNativeMethods
	{
		[DllImport("LZMA.dll")]
		public static extern int LzmaUncompress(byte[] dest, ref IntPtr destLen, byte[] src, ref IntPtr srcLen, byte[] outProps, IntPtr outPropsSize);

		[DllImport("LZMA.dll")]
		public static extern int LzmaUncompressBuf2File(string destFile, ref IntPtr destLen, byte[] src, ref IntPtr srcLen, byte[] outProps, IntPtr outPropsSize);
	}
}
