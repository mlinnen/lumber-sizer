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
            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);
            var outPath = Path.Combine(artifacts, $"cli_export_{Guid.NewGuid()}.html");
            try
            {
                using (var fs = new System.IO.FileStream(sample, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
                using (var sw = new System.IO.StreamWriter(fs))
                {
                    sw.Write("12in x 2in # leg\r\n24in x 6in # shelf\r\n");
                }

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

                Assert.True(proc.ExitCode == 0, $"CLI failed. Exit {proc.ExitCode}. Stdout: {stdout}\nStderr: {stderr}");
                Assert.True(File.Exists(outPath), $"Expected output file at {outPath}. Stdout: {stdout}\nStderr: {stderr}");
                Assert.True(new FileInfo(outPath).Length > 0, "Output file is empty");
            }
            finally
            {
                if (File.Exists(sample)) File.Delete(sample);
                if (File.Exists(outPath)) File.Delete(outPath);
            }
        }

        [Fact]
        public async Task ExportPdf_Command_Uses_Inventory_File_When_Provided()
        {
            var repoRoot = FindRepoRoot();
            var cutlistPath = Path.Combine(Path.GetTempPath(), $"cli_inventory_cutlist_{Guid.NewGuid()}.txt");
            var inventoryPath = Path.Combine(Path.GetTempPath(), $"cli_inventory_{Guid.NewGuid()}.txt");
            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);
            var outPath = Path.Combine(artifacts, $"cli_inventory_export_{Guid.NewGuid()}.html");
            try
            {
                await File.WriteAllTextAsync(cutlistPath, "60in x 2in # long rail\r\n");
                await File.WriteAllTextAsync(inventoryPath, "48in x 4in x 1 # short stock\r\n");

                var startInfo = new ProcessStartInfo("dotnet")
                {
                    Arguments = $"run --project \"{Path.Combine(repoRoot, "src", "WWA.Cli", "WWA.Cli.csproj")}\" -- export-pdf \"{cutlistPath}\" \"{outPath}\" --inventory \"{inventoryPath}\" --packer full",
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

                Assert.True(proc.ExitCode == 0, $"CLI failed. Exit {proc.ExitCode}. Stdout: {stdout}\nStderr: {stderr}");
                Assert.True(File.Exists(outPath), $"Expected output file at {outPath}. Stdout: {stdout}\nStderr: {stderr}");

                var content = await File.ReadAllTextAsync(outPath);
                Assert.Contains("Unplaced items:", content, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("long rail", content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (File.Exists(cutlistPath)) File.Delete(cutlistPath);
                if (File.Exists(inventoryPath)) File.Delete(inventoryPath);
                if (File.Exists(outPath)) File.Delete(outPath);
            }
        }

        [Fact]
        public async Task ExportPdf_Command_Fails_When_Inventory_Value_Is_Missing()
        {
            var repoRoot = FindRepoRoot();
            var cutlistPath = Path.Combine(Path.GetTempPath(), $"cli_missing_inventory_cutlist_{Guid.NewGuid()}.txt");
            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);
            var outPath = Path.Combine(artifacts, $"cli_missing_inventory_export_{Guid.NewGuid()}.html");
            try
            {
                await File.WriteAllTextAsync(cutlistPath, "12in x 2in # leg\r\n");

                var startInfo = new ProcessStartInfo("dotnet")
                {
                    Arguments = $"run --project \"{Path.Combine(repoRoot, "src", "WWA.Cli", "WWA.Cli.csproj")}\" -- export-pdf \"{cutlistPath}\" \"{outPath}\" --inventory",
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

                Assert.Equal(2, proc.ExitCode);
                Assert.Contains("--inventory requires a file path.", stderr, StringComparison.Ordinal);
                Assert.Contains("Usage: export-pdf", stderr, StringComparison.Ordinal);
                Assert.False(File.Exists(outPath), $"Unexpected output file at {outPath}. Stdout: {stdout}\nStderr: {stderr}");
            }
            finally
            {
                if (File.Exists(cutlistPath)) File.Delete(cutlistPath);
                if (File.Exists(outPath)) File.Delete(outPath);
            }
        }
    }
}
