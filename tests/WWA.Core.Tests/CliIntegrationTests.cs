using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace WWA.Core.Tests
{
    public class CliIntegrationTests
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

        [Fact]
        public async Task ExportPdf_Command_Produces_Output_File()
        {
            var repoRoot = FindRepoRoot();
            var sample = Path.Combine(Path.GetTempPath(), $"cli_sample_cutlist_{Guid.NewGuid()}.txt");
            File.WriteAllText(sample, "12in x 2in # leg\r\n24in x 6in # shelf\r\n");

            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);
            var outPath = Path.Combine(artifacts, $"cli_export_{Guid.NewGuid()}.html");

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
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(30000))
            {
                try { proc.Kill(); } catch { }
                throw new TimeoutException("dotnet run timed out");
            }

            Assert.True(proc.ExitCode == 0, $"CLI failed. Exit {proc.ExitCode}. Stdout: {stdout}\nStderr: {stderr}");
            Assert.True(File.Exists(outPath), $"Expected output file at {outPath}. Stdout: {stdout}\nStderr: {stderr}");
            Assert.True(new FileInfo(outPath).Length > 0, "Output file is empty");
        }
    }
}
