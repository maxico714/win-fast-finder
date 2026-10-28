using Wff.Setup;

namespace Wff.Tests;

// Regression tests for the recursive-delete guard added after the audit:
// UninstallAll may only erase a directory that looks like our install.
public sealed class InstallerSafetyTests
{
    private static string NewParent() =>
        Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "wff-uninstall-" + Guid.NewGuid().ToString("N"))).FullName;

    private static string MakeFakeInstall(string parent, string leaf = "win-fast-finder")
    {
        var dir = Path.Combine(parent, leaf);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "Uninstall.exe"), new byte[] { 0 });
        File.WriteAllBytes(Path.Combine(dir, "Wff.App.exe"), new byte[] { 0 });
        return dir;
    }

    [Fact]
    public void Safe_When_Leaf_And_Both_Executables_Match()
    {
        var parent = NewParent();
        try
        {
            var dir = MakeFakeInstall(parent);
            Assert.True(Installer.IsSafeUninstallDirectory(dir));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void Unsafe_When_Leaf_Is_Not_AppName()
    {
        var parent = NewParent();
        try
        {
            var dir = MakeFakeInstall(parent, "some-other-app");
            Assert.False(Installer.IsSafeUninstallDirectory(dir));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    [Theory]
    [InlineData("Uninstall.exe")]
    [InlineData("Wff.App.exe")]
    public void Unsafe_When_An_Expected_Executable_Is_Missing(string missing)
    {
        var parent = NewParent();
        try
        {
            var dir = MakeFakeInstall(parent);
            File.Delete(Path.Combine(dir, missing));
            Assert.False(Installer.IsSafeUninstallDirectory(dir));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void Unsafe_When_Parent_Contains_Shell_Metacharacters()
    {
        var parent = NewParent();
        try
        {
            var dir = MakeFakeInstall(Path.Combine(parent, "bad&dir"));
            Assert.False(Installer.IsSafeUninstallDirectory(dir));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void Unsafe_For_Drive_Root_And_Missing_Directories()
    {
        Assert.False(Installer.IsSafeUninstallDirectory(""));
        Assert.False(Installer.IsSafeUninstallDirectory(@"C:\"));
        Assert.False(Installer.IsSafeUninstallDirectory(
            Path.Combine(Path.GetTempPath(), "wff-missing-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void InstallTo_Refuses_Unsafe_Directory_Before_Writing()
    {
        var parent = NewParent();
        try
        {
            Assert.Throws<ArgumentException>(() =>
                Installer.InstallTo(@"C:\", Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() =>
                Installer.InstallTo(parent, Array.Empty<byte>()));
            Assert.Empty(Directory.GetFileSystemEntries(parent));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }
}
