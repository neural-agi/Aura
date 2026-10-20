using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;

namespace Aura.Tests.Unit;

public sealed class SingleInstanceStartupTests
{
    [Xunit.Fact]
    public async Task SecondLaunchRedirectsToTheFirstAuraWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var configuration = typeof(SingleInstanceStartupTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var executable = Path.Combine(repositoryRoot, "src", "app", "Aura.App", "bin", configuration,
            "net9.0-windows10.0.22621.0", "Aura.App.exe");
        File.Exists(executable).Should().BeTrue($"Aura executable must be built at {executable}");

        using var first = Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)! })!;
        try
        {
            var firstWindow = await WaitForAuraWindowAsync(first.Id);
            firstWindow.Should().NotBe(IntPtr.Zero, "the first launch should create its Aura window");
            (await GetAuraProcessIdsAsync()).Should().BeEquivalentTo([first.Id]);
            await WaitForProcessToExitAsync(first.Id);
            first.HasExited.Should().BeFalse("the first launch must remain authoritative");

            using var second = Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)! })!;
            await second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            second.ExitCode.Should().Be(0);

            (await GetAuraProcessIdsAsync()).Should().BeEquivalentTo([first.Id]);
            var windows = GetAuraWindows();
            windows.Should().ContainSingle(window => window.ProcessId == first.Id && window.Title == "Aura");
            windows.Should().NotContain(window => window.ProcessId == second.Id);
        }
        finally
        {
            if (!first.HasExited)
            {
                first.Kill(entireProcessTree: true);
                await first.WaitForExitAsync();
            }
        }
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aura.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the Aura repository root containing Aura.sln.");
    }

    private static async Task<IntPtr> WaitForAuraWindowAsync(int processId)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            var window = GetAuraWindows().FirstOrDefault(candidate => candidate.ProcessId == processId && candidate.Title == "Aura");
            if (window.Handle != IntPtr.Zero)
            {
                return window.Handle;
            }

            await Task.Delay(100);
        }

        return IntPtr.Zero;
    }

    private static async Task WaitForProcessToExitAsync(int processId)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private static async Task<int[]> GetAuraProcessIdsAsync()
    {
        await Task.Yield();
        return Process.GetProcessesByName("Aura.App").Select(process =>
        {
            using (process) return process.Id;
        }).ToArray();
    }

    private static List<(int ProcessId, IntPtr Handle, string Title)> GetAuraWindows()
    {
        var windows = new List<(int, IntPtr, string)>();
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var processId);
            var title = new System.Text.StringBuilder(256);
            GetWindowText(handle, title, title.Capacity);
            if (IsWindowVisible(handle) && title.ToString() == "Aura")
            {
                windows.Add(((int)processId, handle, title.ToString()));
            }

            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, System.Text.StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
}
