#if !NET6_0_OR_GREATER
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace magika;
static class SymLinkResolver
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern SafeFileHandle CreateFile(
        string lpFileName,
        [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
        [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        [Out] StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    public static FileSystemInfo? ResolveLinkTarget(this FileSystemInfo file, bool returnFinalTarget = true)
    {
        string linkPath = file.FullName;
        SafeFileHandle fileHandle = CreateFile(linkPath, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
        if (fileHandle.IsInvalid)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        try
        {
            StringBuilder path = new(512);
            uint mResult = GetFinalPathNameByHandle(fileHandle, path, (uint)path.Capacity, 0);

            if (mResult <= 0)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            string finalPath = path.ToString();

            if (finalPath.Length >= 4 && finalPath.StartsWith(@"\\?\"))
            {
                finalPath = finalPath.Substring(4);
            }

            if (File.Exists(finalPath))
            {
                return new FileInfo(finalPath);
            }
            else if (Directory.Exists(finalPath))
            {
                return new DirectoryInfo(finalPath);
            }
            else
            {
                return null;
            }
        }
        finally
        {
            fileHandle.Close();
        }
    }
}
#endif
