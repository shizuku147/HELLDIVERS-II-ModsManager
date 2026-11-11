using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HD2MM.Services;

/// <summary>Stores the Nexus API key with Windows DPAPI (per-user, no plaintext on disk).</summary>
public static class SecretStore
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob pDataIn, string? description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DataBlob pDataIn, IntPtr description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static string FilePath => Path.Combine(AppState.Dir, "secret.bin");

    public static bool HasKey => File.Exists(FilePath);

    public static void Save(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var plain = Encoding.UTF8.GetBytes(apiKey.Trim());
        var input = new DataBlob { Size = plain.Length, Data = Marshal.AllocHGlobal(plain.Length) };
        Marshal.Copy(plain, 0, input.Data, plain.Length);

        try
        {
            if (!CryptProtectData(ref input, "HD2MM", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
                throw new InvalidOperationException("DPAPI 加密失败。");

            var cipher = new byte[output.Size];
            Marshal.Copy(output.Data, cipher, 0, output.Size);
            LocalFree(output.Data);

            Directory.CreateDirectory(AppState.Dir);
            File.WriteAllBytes(FilePath, cipher);
        }
        finally
        {
            Marshal.FreeHGlobal(input.Data);
        }
    }

    public static string? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;

            var cipher = File.ReadAllBytes(FilePath);
            var input = new DataBlob { Size = cipher.Length, Data = Marshal.AllocHGlobal(cipher.Length) };
            Marshal.Copy(cipher, 0, input.Data, cipher.Length);

            try
            {
                if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
                    return null;

                var plain = new byte[output.Size];
                Marshal.Copy(output.Data, plain, 0, output.Size);
                LocalFree(output.Data);
                return Encoding.UTF8.GetString(plain);
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
            }
        }
        catch
        {
            return null;
        }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }
}
