using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WWA.Core.Tests
{
    public class E2eAcceptanceTests
    {
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory!);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                dir = dir.Parent;
            }
            if (dir == null) throw new InvalidOperationException("Could not locate repository root (.git folder)");
            return dir.FullName;
        }

        [Theory]
        [InlineData("12in x 2in # leg\n24in x 6in # shelf\n")]
        [InlineData("48in x 24in # tabletop\n12in x 2in # leg\n24in x 6in # shelf\n")]
        public async Task Cli_Export_Produces_Artifact_For_Sample_Data(string cutlistContent)
        {
            var repoRoot = FindRepoRoot();
            var sample = Path.Combine(Path.GetTempPath(), $"e2e_cutlist_{Guid.NewGuid()}.txt");
            File.WriteAllText(sample, cutlistContent);

            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);
            var outPath = Path.Combine(artifacts, $"e2e_export_{Guid.NewGuid()}.html");

            var startInfo = new ProcessStartInfo("dotnet")
            {
                Arguments = $"run --project \"{Path.Combine(repoRoot, "src", "WWA.Cli", "WWA.Cli.csproj")}\" -- export-pdf \"{sample}\" \"{outPath}\" --packer full",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoRoot
            };

            using var proc = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process");
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(30000))
            {
                try { proc.Kill(); } catch { }
                throw new TimeoutException("dotnet run timed out");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            Assert.Equal(0, proc.ExitCode);
            Assert.True(File.Exists(outPath), $"Expected output file at {outPath}. Stdout: {stdout}\nStderr: {stderr}");
            var content = File.ReadAllText(outPath);
            Assert.Contains("<svg", content, StringComparison.OrdinalIgnoreCase);
            Assert.True(content.Length > 100, "Output seems too small");
        }
    }
}
