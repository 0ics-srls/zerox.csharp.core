using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Zerox.Core.Avalonia.Tests.TestApp))]

namespace Zerox.Core.Avalonia.Tests;

// The plain Avalonia application on the headless platform: the library has no App of its own.
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
