using System.Linq;
using Mercurius.Common;
using Xunit;

namespace Mercurius.Tests;

public class PermissionTreeTests
{
    private static string[] AllPermissions() => PermissionTree.Roots.SelectMany(r => r.Permissions).ToArray();

    // A permission missing from the tree couldn't be granted from the Roles screen.
    [Fact]
    public void Tree_ContainsEveryModuleExactlyOnce()
    {
        var inTree = AllPermissions();

        Assert.Empty(ModuleRegistry.Modules.Except(inTree));
        Assert.Empty(inTree.Except(ModuleRegistry.Modules));
        Assert.Equal(inTree.Length, inTree.Distinct().Count());
    }

    [Fact]
    public void ForCaller_HidesDatabasePermissionsFromAnyoneButPlatformAdmins()
    {
        var tenantAdminView = PermissionTree.ForCaller(isPlatformAdmin: false).SelectMany(r => r.Permissions).ToArray();
        var platformAdminView = PermissionTree.ForCaller(isPlatformAdmin: true).SelectMany(r => r.Permissions).ToArray();

        Assert.Empty(tenantAdminView.Intersect(ModuleRegistry.DatabaseModules));
        Assert.Equal(ModuleRegistry.DatabaseModules.Count, platformAdminView.Intersect(ModuleRegistry.DatabaseModules).Count());
    }
}
