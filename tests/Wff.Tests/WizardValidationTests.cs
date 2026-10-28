using Wff.Setup;

namespace Wff.Tests;

public sealed class WizardValidationTests
{
    [Fact]
    public void Accepts_Program_Files_Default()
    {
        Assert.Null(Installer.ValidateDirectory(Installer.DefaultDir));
        Assert.Contains("Program Files", Installer.DefaultDir);
    }

    [Fact]
    public void Rejects_Blank_Relative_WindowsFolder_TooLong()
    {
        Assert.NotNull(Installer.ValidateDirectory(""));
        Assert.NotNull(Installer.ValidateDirectory("relative\\path"));
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.NotNull(Installer.ValidateDirectory(win));
        Assert.NotNull(Installer.ValidateDirectory(new string('a', 201)));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)")]
    [InlineData(@"C:\ProgramData")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Users")]
    public void Rejects_Drive_Root_And_Protected_Folders(string path) =>
        Assert.NotNull(Installer.ValidateDirectory(path));

    [Fact]
    public void Rejects_User_Home_Desktop_Documents_Downloads_Themselves()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.NotNull(Installer.ValidateDirectory(profile));
        Assert.NotNull(Installer.ValidateDirectory(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop)));
        Assert.NotNull(Installer.ValidateDirectory(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
        Assert.NotNull(Installer.ValidateDirectory(Path.Combine(profile, "Downloads")));
    }

    [Fact]
    public void Requires_AppName_As_Leaf_Case_Insensitive()
    {
        Assert.NotNull(Installer.ValidateDirectory(@"C:\Tools\not-the-app"));
        Assert.NotNull(Installer.ValidateDirectory(@"C:\Tools\win-fast-finder-extra"));
        Assert.Null(Installer.ValidateDirectory(@"C:\Tools\WIN-FAST-FINDER"));
        Assert.Null(Installer.ValidateDirectory(@"C:\Tools\Win-Fast-Finder\"));
    }

    [Theory]
    [InlineData("&")]
    [InlineData("|")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("^")]
    [InlineData("%")]
    [InlineData("\"")]
    [InlineData("!")]
    [InlineData("(")]
    [InlineData(")")]
    public void Rejects_Shell_Metacharacters_In_Parent_Path(string meta)
    {
        string path = Path.Combine(@"C:\", "bad" + meta + "dir", "win-fast-finder");
        Assert.NotNull(Installer.ValidateDirectory(path));
    }

    [Fact]
    public void Rejects_Arbitrary_Existing_Folder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wffwiz_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.NotNull(Installer.ValidateDirectory(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Accepts_Custom_App_Folder()
    {
        var parent = Path.Combine(Path.GetTempPath(), "wffwiz_" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(parent, "win-fast-finder");
        try
        {
            Assert.Null(Installer.ValidateDirectory(dir));
        }
        finally
        {
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }
}
