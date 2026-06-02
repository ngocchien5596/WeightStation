using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

return await UpdaterProgram.RunAsync(args);

internal static class UpdaterProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = UpdateOptions.Parse(args);
            var logger = new UpdaterLogger(options.LogFilePath);

            logger.Info("Updater started.");
            logger.Info($"AppDir={options.AppDirectory}");
            logger.Info($"PackageFile={options.PackageFilePath}");

            await WaitForProcessExitAsync(options.ProcessId, logger, CancellationToken.None);

            EnsureDirectoryClean(options.BackupDirectory, logger);
            EnsureDirectoryClean(options.ExtractDirectory, logger);

            logger.Info("Backing up current app directory.");
            CopyDirectory(options.AppDirectory, options.BackupDirectory, logger);

            logger.Info("Extracting update package.");
            ZipFile.ExtractToDirectory(options.PackageFilePath, options.ExtractDirectory, overwriteFiles: true);

            logger.Info("Merging appsettings.json.");
            MergeAppSettingsIfNeeded(options.ExtractDirectory, options.AppDirectory, logger);

            logger.Info("Replacing application files.");
            ClearTargetDirectory(options.AppDirectory, logger);
            CopyDirectory(options.ExtractDirectory, options.AppDirectory, logger);

            if (options.RunDbMigrator)
            {
                logger.Info("Running DbMigrator.");
                RunMigrator(options, logger);
            }

            logger.Info("Cleaning temp directories.");
            SafeDeleteDirectory(options.ExtractDirectory, logger);
            SafeDeleteFile(options.PackageFilePath, logger);

            logger.Info("Starting updated application.");
            Process.Start(new ProcessStartInfo
            {
                FileName = options.AppExePath,
                WorkingDirectory = options.AppDirectory,
                UseShellExecute = true
            });

            logger.Info("Updater completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                var options = UpdateOptions.TryParse(args);
                if (options != null)
                {
                    var logger = new UpdaterLogger(options.LogFilePath);
                    logger.Error("Update failed.", ex);
                    TryRollback(options, logger);
                }
            }
            catch
            {
            }

            MessageBox.Show(
                $"Cap nhat ung dung that bai:{Environment.NewLine}{ex.Message}",
                "Station App - Update Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static async Task WaitForProcessExitAsync(int processId, UpdaterLogger logger, CancellationToken ct)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            logger.Info($"Waiting for process {processId} to exit.");

            while (!process.HasExited)
            {
                await Task.Delay(300, ct);
            }
        }
        catch (ArgumentException)
        {
            logger.Info($"Process {processId} is already not running.");
        }
    }

    private static void EnsureDirectoryClean(string directoryPath, UpdaterLogger logger)
    {
        if (Directory.Exists(directoryPath))
        {
            logger.Info($"Removing existing directory: {directoryPath}");
            Directory.Delete(directoryPath, recursive: true);
        }

        Directory.CreateDirectory(directoryPath);
    }

    private static void ClearTargetDirectory(string appDirectory, UpdaterLogger logger)
    {
        foreach (var file in Directory.GetFiles(appDirectory))
        {
            var fileName = Path.GetFileName(file);
            if (ShouldPreserveFile(fileName))
            {
                continue;
            }

            File.Delete(file);
        }

        foreach (var directory in Directory.GetDirectories(appDirectory))
        {
            var name = Path.GetFileName(directory);
            if (ShouldPreserveDirectory(name))
            {
                continue;
            }

            logger.Info($"Removing directory from target: {directory}");
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool ShouldPreserveFile(string fileName)
        => fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldPreserveDirectory(string directoryName)
        => directoryName.Equals("logs", StringComparison.OrdinalIgnoreCase)
           || directoryName.Equals("_cache", StringComparison.OrdinalIgnoreCase);

    private static void MergeAppSettingsIfNeeded(string extractedDirectory, string appDirectory, UpdaterLogger logger)
    {
        var extractedPath = Path.Combine(extractedDirectory, "appsettings.json");
        var existingPath = Path.Combine(appDirectory, "appsettings.json");
        if (!File.Exists(extractedPath) || !File.Exists(existingPath))
        {
            return;
        }

        var existingRoot = JsonNode.Parse(File.ReadAllText(existingPath)) as JsonObject;
        var packageRoot = JsonNode.Parse(File.ReadAllText(extractedPath)) as JsonObject;
        if (existingRoot == null || packageRoot == null)
        {
            logger.Info("Skip appsettings merge because JSON cannot be parsed.");
            return;
        }

        MergeLocalValues(existingRoot, packageRoot);
        File.WriteAllText(extractedPath, packageRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void MergeLocalValues(JsonObject existingRoot, JsonObject packageRoot)
    {
        foreach (var kv in existingRoot)
        {
            if (kv.Value == null)
            {
                packageRoot[kv.Key] = null;
                continue;
            }

            if (!packageRoot.TryGetPropertyValue(kv.Key, out var packageValue) || packageValue == null)
            {
                packageRoot[kv.Key] = kv.Value.DeepClone();
                continue;
            }

            if (kv.Value is JsonObject existingObj && packageValue is JsonObject packageObj)
            {
                MergeLocalValues(existingObj, packageObj);
                continue;
            }

            packageRoot[kv.Key] = kv.Value.DeepClone();
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, UpdaterLogger logger)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var targetFile = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }

    private static void RunMigrator(UpdateOptions options, UpdaterLogger logger)
    {
        if (!File.Exists(options.DbMigratorExePath))
        {
            throw new FileNotFoundException("DbMigrator executable not found.", options.DbMigratorExePath);
        }

        if (!File.Exists(options.ConfigFilePath))
        {
            throw new FileNotFoundException("Config file not found.", options.ConfigFilePath);
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = options.DbMigratorExePath,
            Arguments = $"--config \"{options.ConfigFilePath}\"",
            WorkingDirectory = Path.GetDirectoryName(options.DbMigratorExePath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Cannot start DbMigrator.");

        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(stdOut))
        {
            logger.Info(stdOut.Trim());
        }

        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            logger.Info(stdErr.Trim());
        }

        if (process.ExitCode != 0)
        {
            var detailParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                detailParts.Add($"stdout: {stdOut.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                detailParts.Add($"stderr: {stdErr.Trim()}");
            }

            var detail = detailParts.Count > 0
                ? $" Details: {string.Join(" | ", detailParts)}"
                : string.Empty;

            throw new InvalidOperationException($"DbMigrator failed with exit code {process.ExitCode}.{detail}");
        }
    }

    private static void TryRollback(UpdateOptions options, UpdaterLogger logger)
    {
        try
        {
            if (!Directory.Exists(options.BackupDirectory))
            {
                logger.Info("Rollback skipped because backup directory does not exist.");
                return;
            }

            logger.Info("Starting rollback from backup.");
            ClearTargetDirectory(options.AppDirectory, logger);
            CopyDirectory(options.BackupDirectory, options.AppDirectory, logger);
            logger.Info("Rollback completed.");
        }
        catch (Exception rollbackEx)
        {
            logger.Error("Rollback failed.", rollbackEx);
        }
    }

    private static void SafeDeleteDirectory(string directoryPath, UpdaterLogger logger)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Cannot delete directory {directoryPath}.", ex);
        }
    }

    private static void SafeDeleteFile(string filePath, UpdaterLogger logger)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Cannot delete file {filePath}.", ex);
        }
    }
}

internal sealed record UpdateOptions(
    string AppDirectory,
    string AppExePath,
    string PackageFilePath,
    string ConfigFilePath,
    string DbMigratorExePath,
    string BackupDirectory,
    string ExtractDirectory,
    string LogFilePath,
    int ProcessId,
    bool RunDbMigrator)
{
    public static UpdateOptions Parse(string[] args)
        => TryParse(args) ?? throw new InvalidOperationException("Invalid updater arguments.");

    public static UpdateOptions? TryParse(string[] args)
    {
        var map = ParseArgs(args);
        if (!map.TryGetValue("app-dir", out var appDir) ||
            !map.TryGetValue("app-exe", out var appExe) ||
            !map.TryGetValue("package", out var package) ||
            !map.TryGetValue("config", out var config) ||
            !map.TryGetValue("migrator-exe", out var migratorExe) ||
            !map.TryGetValue("process-id", out var processIdRaw))
        {
            return null;
        }

        if (!int.TryParse(processIdRaw, out var processId))
        {
            return null;
        }

        var backupDir = map.GetValueOrDefault("backup-dir") ?? (appDir.TrimEnd(Path.DirectorySeparatorChar) + "._backup");
        var extractDir = map.GetValueOrDefault("extract-dir") ?? (appDir.TrimEnd(Path.DirectorySeparatorChar) + "._update");
        var logFilePath = map.GetValueOrDefault("log-file") ?? Path.Combine(appDir, "logs", "updater.log");
        var runDbMigrator = !string.Equals(map.GetValueOrDefault("run-db-migrator"), "false", StringComparison.OrdinalIgnoreCase);

        return new UpdateOptions(
            Path.GetFullPath(appDir),
            Path.GetFullPath(appExe),
            Path.GetFullPath(package),
            Path.GetFullPath(config),
            Path.GetFullPath(migratorExe),
            Path.GetFullPath(backupDir),
            Path.GetFullPath(extractDir),
            Path.GetFullPath(logFilePath),
            processId,
            runDbMigrator);
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            var value = i + 1 < args.Length ? args[i + 1] : string.Empty;
            if (!value.StartsWith("--", StringComparison.Ordinal))
            {
                result[key] = value;
                i++;
            }
            else
            {
                result[key] = "true";
            }
        }

        return result;
    }
}

internal sealed class UpdaterLogger
{
    private readonly string _logFilePath;
    private readonly object _sync = new();

    public UpdaterLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception ex) => Write("ERROR", $"{message} {ex}");

    private void Write(string level, string message)
    {
        lock (_sync)
        {
            File.AppendAllText(
                _logFilePath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
        }
    }
}
