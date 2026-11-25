using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ThereforeLauncher;

// Keep Therefore's opaque remembered-login token in the current Windows user's
// DPAPI envelope. Never decode the client's token or store a plaintext password.
public static class SavedLogin
{
    public sealed record Login(string User, string Token);
    public static string Capture(TenantProfile profile, Func<string, object?> read)
    {
        if (profile.AuthenticationMode != 0)
            throw new InvalidOperationException("Saved logins are available for username and password profiles only.");
        if (!ConnectionConfiguration.Matches(ConnectionConfiguration.ValuesFor(profile with { ProtectedLogin = null }),
                name => name is "User" or "Token" ? "" : read(name)))
            throw new InvalidOperationException("The current Therefore connection must match this profile before saving its login.");
        var login = new Login(read("User") as string ?? "", read("Token") as string ?? "");
        if (string.IsNullOrWhiteSpace(login.User) || string.IsNullOrWhiteSpace(login.Token))
            throw new InvalidOperationException("Sign in to Therefore with Remember password enabled first, then save the current login here.");
        return Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(login)), false));
    }
    public static Login Read(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<Login>(Encoding.UTF8.GetString(Protect(Convert.FromBase64String(value), true)))
                ?? throw new InvalidOperationException();
        }
        catch (Exception ex) { throw new InvalidOperationException("Unable to read the saved login for this Windows account. Save the current Therefore login again.", ex); }
    }
    [StructLayout(LayoutKind.Sequential)]
    struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptProtectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
    static byte[] Protect(byte[] bytes, bool decrypt)
    {
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        var output = new Blob();
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var ok = decrypt
                ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
            {
                for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                LocalFree(output.Data);
            }
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
