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

    [Fact]
    public void Accepts_Custom_User_Folder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wffwiz_" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(Installer.ValidateDirectory(dir));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir);
        }
    }
}
