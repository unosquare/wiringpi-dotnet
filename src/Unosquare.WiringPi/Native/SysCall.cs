namespace Unosquare.WiringPi.Native
{
    using System;
    using System.Runtime.InteropServices;

    internal static class SysCall
    {
        internal const string LibCLibrary = "libc";

        [DllImport(LibCLibrary, EntryPoint = "chmod", SetLastError = true)]
        public static extern int Chmod(string filename, uint mode);

        [DllImport(LibCLibrary, EntryPoint = "strtol", SetLastError = true)]
        public static extern int StringToInteger(string numberString, IntPtr endPointer, int numberBase);

        [DllImport(LibCLibrary, EntryPoint = "write", SetLastError = true)]
        public static extern int Write(int fd, byte[] buffer, int count);
    }
}
