using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCliAsync(string repoRoot, params string[] cliArgs)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoRoot
            };

            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(Path.Combine(repoRoot, "src", "WWA.Cli", "WWA.Cli.csproj"));
            startInfo.ArgumentList.Add("--");
            foreach (var arg in cliArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var proc = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process");
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(30000))
            {
                try { proc.Kill(); } catch { }
                throw new TimeoutException("dotnet run timed out");
            }

            return (proc.ExitCode, await stdoutTask, await stderrTask);
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

                var (exitCode, stdout, stderr) = await RunCliAsync(repoRoot, "export-pdf", sample, outPath, "--packer", "full");

                Assert.True(exitCode == 0, $"CLI failed. Exit {exitCode}. Stdout: {stdout}\nStderr: {stderr}");
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

                var (exitCode, stdout, stderr) = await RunCliAsync(repoRoot, "export-pdf", cutlistPath, outPath, "--inventory", inventoryPath, "--packer", "full");

                Assert.True(exitCode == 0, $"CLI failed. Exit {exitCode}. Stdout: {stdout}\nStderr: {stderr}");
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

                var (exitCode, stdout, stderr) = await RunCliAsync(repoRoot, "export-pdf", cutlistPath, outPath, "--inventory");

                Assert.Equal(2, exitCode);
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

        [Fact]
        public async Task ExportPdf_Command_Default_FullPacker_Strategy_Remains_BestFitDecreasing()
        {
            var repoRoot = FindRepoRoot();
            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);
            var cutlistPath = Path.Combine(artifacts, $"cli_strategy_default_cutlist_{Guid.NewGuid()}.txt");
            var inventoryPath = Path.Combine(artifacts, $"cli_strategy_default_inventory_{Guid.NewGuid()}.txt");
            var outPath = Path.Combine(artifacts, $"cli_strategy_default_export_{Guid.NewGuid()}.html");
            var cutlist = string.Join(Environment.NewLine, Enumerable.Repeat("10in x 2in # spacer", 5).Concat(new[]
            {
                "20in x 2in # large-a",
                "20in x 2in # large-b"
            })) + Environment.NewLine;

            try
            {
                await File.WriteAllTextAsync(cutlistPath, cutlist);
                await File.WriteAllTextAsync(inventoryPath, "30in x 4in x 3 # stock\r\n");

                var (exitCode, stdout, stderr) = await RunCliAsync(repoRoot, "export-pdf", cutlistPath, outPath, "--inventory", inventoryPath, "--packer", "full");

                Assert.Equal(0, exitCode);
                Assert.True(File.Exists(outPath), $"Expected output file at {outPath}. Stdout: {stdout}\nStderr: {stderr}");

                var content = await File.ReadAllTextAsync(outPath);
                Assert.DoesNotContain("Unplaced items:", content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (File.Exists(cutlistPath)) File.Delete(cutlistPath);
                if (File.Exists(inventoryPath)) File.Delete(inventoryPath);
                if (File.Exists(outPath)) File.Delete(outPath);
            }
        }

        [Fact]
        public async Task ExportPdf_Command_Applies_Selected_FullPacker_Strategy()
        {
            var repoRoot = FindRepoRoot();
            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);
            var cutlistPath = Path.Combine(artifacts, $"cli_strategy_cutlist_{Guid.NewGuid()}.txt");
            var inventoryPath = Path.Combine(artifacts, $"cli_strategy_inventory_{Guid.NewGuid()}.txt");
            var outPath = Path.Combine(artifacts, $"cli_strategy_export_{Guid.NewGuid()}.html");
            var cutlist = string.Join(Environment.NewLine, Enumerable.Repeat("10in x 2in # spacer", 5).Concat(new[]
            {
                "20in x 2in # large-a",
                "20in x 2in # large-b"
            })) + Environment.NewLine;

            try
            {
                await File.WriteAllTextAsync(cutlistPath, cutlist);
                await File.WriteAllTextAsync(inventoryPath, "30in x 4in x 3 # stock\r\n");

                var (exitCode, stdout, stderr) = await RunCliAsync(repoRoot, "export-pdf", cutlistPath, outPath, "--inventory", inventoryPath, "--packer", "full", "--strategy", "first-fit");

                Assert.Equal(0, exitCode);
                Assert.True(File.Exists(outPath), $"Expected output file at {outPath}. Stdout: {stdout}\nStderr: {stderr}");

                var content = await File.ReadAllTextAsync(outPath);
                Assert.Contains("Unplaced items:", content, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("large-b", content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (File.Exists(cutlistPath)) File.Delete(cutlistPath);
                if (File.Exists(inventoryPath)) File.Delete(inventoryPath);
                if (File.Exists(outPath)) File.Delete(outPath);
            }
        }

        [Fact]
        public async Task ExportPdf_Command_Fails_When_Strategy_Value_Is_Invalid()
        {
            var repoRoot = FindRepoRoot();
            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);
            var cutlistPath = Path.Combine(artifacts, $"cli_invalid_strategy_cutlist_{Guid.NewGuid()}.txt");
            var outPath = Path.Combine(artifacts, $"cli_invalid_strategy_export_{Guid.NewGuid()}.html");

            try
            {
                await File.WriteAllTextAsync(cutlistPath, "12in x 2in # leg\r\n");

                var (exitCode, stdout, stderr) = await RunCliAsync(repoRoot, "export-pdf", cutlistPath, outPath, "--strategy", "best-fit-ish");

                Assert.Equal(2, exitCode);
                Assert.Contains("--strategy must be one of: best-fit-decreasing|first-fit-decreasing|first-fit.", stderr, StringComparison.Ordinal);
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
