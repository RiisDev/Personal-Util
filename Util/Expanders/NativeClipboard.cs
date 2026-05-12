using System.Runtime.InteropServices;

namespace Script.Util.Expanders
{
	public static class NativeClipboard
	{
		private const uint CF_UNICODETEXT = 13;
		private const uint GMEM_MOVEABLE = 0x0002;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool OpenClipboard(IntPtr hWndNewOwner);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool CloseClipboard();

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool EmptyClipboard();

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GlobalLock(IntPtr hMem);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GlobalUnlock(IntPtr hMem);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GlobalFree(IntPtr hMem);

		public static void SetText(string text)
		{
			ArgumentNullException.ThrowIfNull(text);

			bool openResult = OpenClipboard(IntPtr.Zero);
			if (!openResult)
			{
				throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
			}

			try
			{
				bool emptyResult = EmptyClipboard();
				if (!emptyResult)
				{
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}

				byte[] textBytes = Encoding.Unicode.GetBytes(text + "\0");
				UIntPtr byteCount = (UIntPtr)textBytes.Length;

				IntPtr hGlobalMemory = GlobalAlloc(GMEM_MOVEABLE, byteCount);
				if (hGlobalMemory == IntPtr.Zero)
				{
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}

				IntPtr lockedMemory = GlobalLock(hGlobalMemory);
				if (lockedMemory == IntPtr.Zero)
				{
					GlobalFree(hGlobalMemory);
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}

				try
				{
					Marshal.Copy(textBytes, 0, lockedMemory, textBytes.Length);
				}
				finally
				{
					bool unlockResult = GlobalUnlock(hGlobalMemory);
				}

				IntPtr setResult = SetClipboardData(CF_UNICODETEXT, hGlobalMemory);
				if (setResult == IntPtr.Zero)
				{
					GlobalFree(hGlobalMemory);
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}

				// IMPORTANT:
				// After SetClipboardData succeeds, the system owns the memory.
				// Do NOT free hGlobalMemory here.
			}
			finally
			{
				bool closeResult = CloseClipboard();
			}
		}
	}
}