using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartLedger.Infrastructure.Security;

namespace SmartLedger.App.Security;

public sealed class RememberedLoginStore
{
    private const string FileName = "remembered-login.bin";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SmartLedger.RememberMe.v1");
    private readonly string filePath = Path.Combine(LocalAppPaths.Root, FileName);

    public RememberedLogin? Load()
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var protectedData = File.ReadAllBytes(filePath);
            var value = JsonSerializer.Deserialize<RememberedLogin>(Unprotect(protectedData));
            if (value is null || string.IsNullOrWhiteSpace(value.UserName) || string.IsNullOrEmpty(value.Password))
            {
                Clear();
                return null;
            }
            return value;
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public void Save(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            Clear();
            return;
        }

        Directory.CreateDirectory(LocalAppPaths.Root);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new RememberedLogin(userName.Trim(), password));
        var protectedData = Protect(payload);
        var tempPath = filePath + ".tmp";
        File.WriteAllBytes(tempPath, protectedData);
        File.Move(tempPath, filePath, true);
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch
        {
            // Cleanup must never block login.
        }
    }

    private static byte[] Protect(byte[] data)
    {
        var input = new DataBlob(data);
        var entropy = new DataBlob(Entropy);
        try
        {
            if (!CryptProtectData(ref input.Blob, null, ref entropy.Blob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbid, out var output))
                throw new CryptographicException(Marshal.GetLastWin32Error());
            return ToArrayAndFree(output);
        }
        finally
        {
            input.Dispose();
            entropy.Dispose();
        }
    }

    private static byte[] Unprotect(byte[] data)
    {
        var input = new DataBlob(data);
        var entropy = new DataBlob(Entropy);
        try
        {
            if (!CryptUnprotectData(ref input.Blob, IntPtr.Zero, ref entropy.Blob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbid, out var output))
                throw new CryptographicException(Marshal.GetLastWin32Error());
            return ToArrayAndFree(output);
        }
        finally
        {
            input.Dispose();
            entropy.Dispose();
        }
    }

    private const uint CryptProtectUiForbid = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    private sealed class DataBlob : IDisposable
    {
        public NativeDataBlob Blob;
        private IntPtr allocated;

        public DataBlob(byte[] data)
        {
            allocated = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, allocated, data.Length);
            Blob = new NativeDataBlob { Size = data.Length, Data = allocated };
        }

        public void Dispose()
        {
            if (allocated == IntPtr.Zero) return;
            Marshal.FreeHGlobal(allocated);
            allocated = IntPtr.Zero;
            Blob = default;
        }
    }

    private static byte[] ToArrayAndFree(NativeDataBlob blob)
    {
        try
        {
            if (blob.Data == IntPtr.Zero || blob.Size <= 0) return Array.Empty<byte>();
            var result = new byte[blob.Size];
            Marshal.Copy(blob.Data, result, 0, blob.Size);
            return result;
        }
        finally
        {
            if (blob.Data != IntPtr.Zero) Marshal.FreeHGlobal(blob.Data);
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref NativeDataBlob dataIn, string? description, ref NativeDataBlob optionalEntropy, IntPtr reserved, IntPtr promptStruct, uint flags, out NativeDataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref NativeDataBlob dataIn, IntPtr description, ref NativeDataBlob optionalEntropy, IntPtr reserved, IntPtr promptStruct, uint flags, out NativeDataBlob dataOut);
}

public sealed record RememberedLogin(string UserName, string Password);

