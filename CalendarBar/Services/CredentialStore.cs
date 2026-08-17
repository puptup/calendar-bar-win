using System.Runtime.InteropServices;
using System.Text;

namespace CalendarBar;

public static class CredentialStore
{
    private const string Target = "CalendarBar/exchange-password";

    public static void SavePassword(string password)
    {
        DeletePassword();
        var blob = Encoding.Unicode.GetBytes(password);
        var credential = new Native.Credential
        {
            Type = Native.CredTypeGeneric,
            Persist = Native.CredPersistLocalMachine,
            UserName = "exchange-password",
            TargetName = Target,
            CredentialBlob = Marshal.AllocHGlobal(blob.Length),
            CredentialBlobSize = (uint)blob.Length
        };
        Marshal.Copy(blob, 0, credential.CredentialBlob, blob.Length);
        try
        {
            if (!Native.CredWrite(ref credential, 0))
                throw new InvalidOperationException($"Не удалось сохранить пароль (код {Marshal.GetLastWin32Error()})");
        }
        finally
        {
            Marshal.FreeHGlobal(credential.CredentialBlob);
        }
    }

    public static string? LoadPassword()
    {
        if (!Native.CredRead(Target, Native.CredTypeGeneric, 0, out var ptr)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<Native.Credential>(ptr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0) return null;
            return Marshal.PtrToStringUni(cred.CredentialBlob, (int)cred.CredentialBlobSize / 2);
        }
        finally
        {
            Native.CredFree(ptr);
        }
    }

    public static void DeletePassword()
    {
        Native.CredDelete(Target, Native.CredTypeGeneric, 0);
    }

    private static class Native
    {
        public const int CredTypeGeneric = 1;
        public const int CredPersistLocalMachine = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct Credential
        {
            public uint Flags;
            public uint Type;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
            [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredWrite(ref Credential credential, uint flags);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredDelete(string target, int type, int flags);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern void CredFree(IntPtr credentialPtr);
    }
}
