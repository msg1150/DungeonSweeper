using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Windows DPAPI encrypts and authenticates the entire save with OS-managed user keys.
/// There is no shared secret embedded in the game or plain-text key file.
/// The slot is part of DPAPI's additional entropy, so moving a save to another slot is rejected.
/// </summary>
public static class SaveFileProtection
{
    public const int MaximumPlaintextBytes = 4 * 1024 * 1024;
    public const int MaximumFileBytes = MaximumPlaintextBytes + 64 * 1024;
    private const byte FormatVersion = 1;
    private const int HeaderBytes = 13;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DSWPSAVE");
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    public static byte[] Protect(string json, int slot)
    {
        if (json == null || Utf8.GetByteCount(json) > MaximumPlaintextBytes)
            throw new InvalidDataException("Save exceeds the supported size.");
        byte[] plaintext = Utf8.GetBytes(json);
        try
        {
            byte[] protectedBytes = Transform(plaintext, slot, true);
            if (protectedBytes.Length > MaximumFileBytes - HeaderBytes)
                throw new InvalidDataException("Protected save exceeds the supported size.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(protectedBytes.Length);
                writer.Write(protectedBytes);
                return stream.ToArray();
            }
        }
        finally { Array.Clear(plaintext, 0, plaintext.Length); }
    }

    public static string Unprotect(byte[] file, int slot)
    {
        if (file == null || file.Length <= HeaderBytes || file.Length > MaximumFileBytes)
            throw new InvalidDataException("Invalid protected save size.");
        using (var stream = new MemoryStream(file, false))
        using (var reader = new BinaryReader(stream))
        {
            for (int i = 0; i < Magic.Length; i++)
                if (reader.ReadByte() != Magic[i]) throw new InvalidDataException("Unrecognized save format.");
            if (reader.ReadByte() != FormatVersion) throw new InvalidDataException("Unsupported save format.");
            int length = reader.ReadInt32();
            if (length <= 0 || length != file.Length - HeaderBytes)
                throw new InvalidDataException("Invalid protected save length.");
            byte[] plaintext = Transform(reader.ReadBytes(length), slot, false);
            try
            {
                if (plaintext.Length > MaximumPlaintextBytes) throw new InvalidDataException("Save exceeds the supported size.");
                return Utf8.GetString(plaintext);
            }
            finally { Array.Clear(plaintext, 0, plaintext.Length); }
        }
    }

    private static byte[] Transform(byte[] input, int slot, bool protect)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // This is a public purpose/slot identifier, not an encryption key.
        // Keep it stable across releases so existing protected saves remain readable.
        byte[] entropy = Encoding.UTF8.GetBytes("DungeonSweeper.SaveProtection.v1/slot/"
            + slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
        GCHandle inputHandle = default, entropyHandle = default;
        NativeBlob output = default;
        try
        {
            inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            entropyHandle = GCHandle.Alloc(entropy, GCHandleType.Pinned);
            var inputBlob = new NativeBlob { length = input.Length, data = inputHandle.AddrOfPinnedObject() };
            var entropyBlob = new NativeBlob { length = entropy.Length, data = entropyHandle.AddrOfPinnedObject() };
            // Never use CRYPTPROTECT_LOCAL_MACHINE: saves belong to the current Windows user.
            const uint noUi = 1; // CRYPTPROTECT_UI_FORBIDDEN, safe in batch mode and players.
            bool success = protect
                ? CryptProtectData(ref inputBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, noUi, out output)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, noUi, out output);
            if (!success) throw new CryptographicException(Marshal.GetLastWin32Error());
            if (output.length <= 0 || output.length > MaximumFileBytes)
                throw new InvalidDataException("Invalid DPAPI output size.");
            byte[] result = new byte[output.length];
            Marshal.Copy(output.data, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (output.data != IntPtr.Zero)
            {
                // Unprotect returns plaintext in native memory. Clear it before releasing it.
                if (!protect && output.length > 0 && output.length <= MaximumFileBytes)
                {
                    byte[] zeros = new byte[Math.Min(4096, output.length)];
                    for (int offset = 0; offset < output.length; offset += zeros.Length)
                        Marshal.Copy(zeros, 0, IntPtr.Add(output.data, offset), Math.Min(zeros.Length, output.length - offset));
                }
                LocalFree(output.data);
            }
            if (inputHandle.IsAllocated) inputHandle.Free();
            if (entropyHandle.IsAllocated) entropyHandle.Free();
        }
#else
        // Never silently downgrade to plain text on an unsupported target.
        throw new PlatformNotSupportedException("Protected saves currently support Windows only.");
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBlob { public int length; public IntPtr data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref NativeBlob input, IntPtr description,
        ref NativeBlob entropy, IntPtr reserved, IntPtr prompt, uint flags, out NativeBlob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref NativeBlob input, IntPtr description,
        ref NativeBlob entropy, IntPtr reserved, IntPtr prompt, uint flags, out NativeBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
#endif
}
