using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class Office97OptionTests
{
    [Fact]
    public void OptionPersistsAcrossSchemesThemesAndLayouts()
    {
        using var manager = new CommandBarManager();
        Assert.DoesNotContain(manager.Themes, t => t.Key == CommandBarThemeKeys.Office97);
        manager.Theme = CommandBarTheme.Office2000;
        Assert.False(((Office2000Renderer)manager.Renderer).UseOffice97Gripper);
        manager.UseOffice97Gripper = true;
        manager.ColorScheme = CommandBarColorScheme.Blue;
        Assert.True(((Office2000Renderer)manager.Renderer).UseOffice97Gripper);
        manager.Theme = CommandBarTheme.Fluent;
        manager.Theme = CommandBarTheme.Office2000;
        Assert.True(((Office2000Renderer)manager.Renderer).UseOffice97Gripper);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream);
        stream.Position = 0;
        using var restored = new CommandBarManager();
        restored.LoadLayout(stream);
        Assert.Equal(CommandBarTheme.Office2000, restored.Theme);
        Assert.True(((Office2000Renderer)restored.Renderer).UseOffice97Gripper);
        restored.UseOffice97Gripper = false;
        Assert.False(((Office2000Renderer)restored.Renderer).UseOffice97Gripper);
    }

    [Fact]
    public void LegacyThemeBecomesOffice2000WithOptionEnabled()
    {
        using var manager = new CommandBarManager();
        manager.Theme = CommandBarTheme.Office97;
        Assert.Equal(CommandBarTheme.Office2000, manager.Theme);
        Assert.Equal(CommandBarThemeKeys.Office2000, manager.ActiveThemeKey);
        Assert.True(manager.UseOffice97Gripper);
    }
}