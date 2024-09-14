using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace Win32Api
{
    [SupportedOSPlatform("Windows")]
    public static class HardLinkHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle, out BY_HANDLE_FILE_INFORMATION lpFileInformation);


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr FindFirstFileNameW(
            string lpFileName,
            uint dwFlags,
            ref int stringLength,
            StringBuilder fileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool FindNextFileNameW(
            IntPtr hFindStream,
            ref int stringLength,
            StringBuilder fileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FindClose(IntPtr fFindHandle);

        [DllImport("kernel32.dll")]
        static extern bool GetVolumePathName(string lpszFileName,
            [Out] StringBuilder lpszVolumePathName, int cchBufferLength);

        public static int GetFileLinkCount(string filepath)
        {
            int result = 0;
            using SafeFileHandle handle = CreateFile(filepath, FileAccess.Read, FileShare.Read|FileShare.Write, IntPtr.Zero, FileMode.Open, FileAttributes.Archive, IntPtr.Zero);
            if (handle.IsInvalid)
                return 0;
            BY_HANDLE_FILE_INFORMATION fileInfo = new BY_HANDLE_FILE_INFORMATION();
            if (GetFileInformationByHandle(handle, out fileInfo))
                result = (int)fileInfo.NumberOfLinks;
            return result;
        }

       
        public static string GetFileFirstHardLink(string filepath)
        {
            Interlocked.Increment(ref Counters.HardlinkChecks);

            if (GetFileLinkCount(filepath) < 2)
                return filepath;

            var sb = new StringBuilder(256);
            GetVolumePathName(filepath, sb, sb.Capacity);
            string volume = sb.ToString();
            sb.Length = 0;
            int stringLength = sb.Capacity;
            IntPtr findHandle = FindFirstFileNameW(filepath, 0, ref stringLength, sb);
            int error = 0;
            if (findHandle == -1)
            {
                error = Marshal.GetLastWin32Error();
                if (error == 234 /*ERROR_MORE_DATA*/)
                {
                    sb.Capacity = (int)stringLength;
                    findHandle = FindFirstFileNameW(filepath, 0, ref stringLength, sb);
                }
            }
            if (findHandle == -1)
                return filepath;
            FindClose(findHandle);
            return Path.Combine(volume, sb.ToString().TrimStart(Path.DirectorySeparatorChar));
        }

    }
}