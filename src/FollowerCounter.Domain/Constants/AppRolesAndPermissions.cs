namespace FollowerCounter.Domain.Constants;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Support = "Support";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin,
        Admin,
        Support,
        Customer
    ];
}

public static class AppPermissions
{
    public const string ClaimType = "Permission";

    // User Management
    public const string UsersView = "Permissions.Users.View";
    public const string UsersCreate = "Permissions.Users.Create";
    public const string UsersEdit = "Permissions.Users.Edit";
    public const string UsersDelete = "Permissions.Users.Delete";

    // Device Management
    public const string DevicesView = "Permissions.Devices.View";
    public const string DevicesProvision = "Permissions.Devices.Provision";
    public const string DevicesClaim = "Permissions.Devices.Claim";
    public const string DevicesManage = "Permissions.Devices.Manage";

    // Instagram Management
    public const string InstagramView = "Permissions.Instagram.View";
    public const string InstagramConnect = "Permissions.Instagram.Connect";
    public const string InstagramRefresh = "Permissions.Instagram.Refresh";
    public const string InstagramDisconnect = "Permissions.Instagram.Disconnect";

    // Platform & Audit
    public const string AuditLogsView = "Permissions.AuditLogs.View";
    public const string SecurityEventsView = "Permissions.SecurityEvents.View";
    public const string SystemHealthView = "Permissions.SystemHealth.View";
    public const string SystemManage = "Permissions.System.Manage";

    public static readonly IReadOnlyList<string> All =
    [
        UsersView,
        UsersCreate,
        UsersEdit,
        UsersDelete,
        DevicesView,
        DevicesProvision,
        DevicesClaim,
        DevicesManage,
        InstagramView,
        InstagramConnect,
        InstagramRefresh,
        InstagramDisconnect,
        AuditLogsView,
        SecurityEventsView,
        SystemHealthView,
        SystemManage
    ];
}
