using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Ladeco.Diag.App.Services;

public sealed class LocalUserAccountService : ILocalUserAccountService
{
    private const uint UserPrivilegeUser = 1;
    private const uint AccountNormal = 0x0201;

    public Task<AccountOperationResult> CreateAdministratorAsync(string userName, string password)
    {
        return Task.Run(() => CreateAdministrator(userName, password));
    }

    public void LogoffCurrentSession()
    {
        if (!ExitWindowsEx(0, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static AccountOperationResult CreateAdministrator(string userName, string password)
    {
        var user = new UserInfo1
        {
            Name = userName,
            Password = password,
            Privilege = UserPrivilegeUser,
            Flags = AccountNormal
        };

        var result = NetUserAdd(null, 1, ref user, out _);
        if (result != 0)
        {
            return new AccountOperationResult(false, new Win32Exception(result).Message);
        }

        var member = new LocalGroupMemberInfo3 { DomainAndName = $"{Environment.MachineName}\\{userName}" };
        result = NetLocalGroupAddMembers(null, "Administrators", 3, ref member, 1, out _);
        if (result != 0)
        {
            _ = NetUserDel(null, userName);
            return new AccountOperationResult(false, new Win32Exception(result).Message);
        }

        return new AccountOperationResult(true, string.Empty);
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int NetUserAdd(string? serverName, int level, ref UserInfo1 userInfo, out uint parameterError);

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int NetUserDel(string? serverName, string userName);

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int NetLocalGroupAddMembers(string? serverName, string groupName, int level, ref LocalGroupMemberInfo3 members, int totalEntries, out uint parameterError);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ExitWindowsEx(uint flags, uint reason);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct UserInfo1
    {
        public string Name;
        public string Password;
        public uint PasswordAge;
        public uint Privilege;
        public string HomeDirectory;
        public string Comment;
        public uint Flags;
        public string ScriptPath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LocalGroupMemberInfo3
    {
        public string DomainAndName;
    }
}