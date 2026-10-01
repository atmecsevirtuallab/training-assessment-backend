using System.Diagnostics;
using System.Text.RegularExpressions;
using TrainingAndAssessmentWebAPI.Models;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class ProgramExecutionService
{
    private readonly string _cCompilerPath;
    private readonly string _javaCompilerPath;
    private readonly string _javaRuntimePath;
    private readonly string _pythonPath;
    private readonly int _compileTimeoutMs;
    private readonly int _executionTimeoutMs;

    public ProgramExecutionService(IConfiguration configuration)
    {
        var section = configuration.GetSection("CodeExecution");
        _cCompilerPath = ResolveToolPath(section["CCompilerPath"], "gcc");
        _javaCompilerPath = ResolveToolPath(section["JavaCompilerPath"], "javac");
        _javaRuntimePath = ResolveToolPath(section["JavaRuntimePath"], "java");
        _pythonPath = ResolveToolPath(section["PythonPath"], OperatingSystem.IsWindows() ? "python" : "python3");
        _compileTimeoutMs = Math.Max(1, section.GetValue("CompileTimeoutSeconds", 15)) * 1000;
        _executionTimeoutMs = Math.Max(1, section.GetValue("ExecutionTimeoutSeconds", 5)) * 1000;
    }

    public async Task<ProgramExecutionResponseDto> ExecuteAsync(ExecuteProgramDto request)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "TrainingAssessment", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var language = request.Language.Trim().ToLowerInvariant();
            string command;
            string arguments;
            if (language == "python")
            {
                var source = Path.Combine(workDir, "solution.py");
                await File.WriteAllTextAsync(source, request.Code);
                command = _pythonPath; arguments = $"\"{source}\"";
            }
            else if (language == "java")
            {
                var classMatch = Regex.Match(request.Code, @"\bpublic\s+(?:final\s+)?class\s+([A-Za-z_$][A-Za-z0-9_$]*)");
                if (!classMatch.Success)
                    classMatch = Regex.Match(request.Code, @"\bclass\s+([A-Za-z_$][A-Za-z0-9_$]*)");
                var className = classMatch.Success ? classMatch.Groups[1].Value : "Main";
                var source = Path.Combine(workDir, $"{className}.java");
                await File.WriteAllTextAsync(source, request.Code);
                var compile = await RunAsync(_javaCompilerPath, $"\"{source}\"", string.Empty, workDir, _compileTimeoutMs);
                if (compile.ExitCode != 0) return new(compile.Error, new());
                command = _javaRuntimePath; arguments = $"-cp \"{workDir}\" {className}";
            }
            else if (language == "c")
            {
                var source = Path.Combine(workDir, "solution.c");
                var executableName = OperatingSystem.IsWindows() ? "solution.exe" : "solution";
                var executable = Path.Combine(workDir, executableName);
                await File.WriteAllTextAsync(source, request.Code);
                var compile = await RunAsync(_cCompilerPath, $"\"{source}\" -o \"{executable}\"", string.Empty, workDir, _compileTimeoutMs);
                if (compile.ExitCode != 0) return new(compile.Error, new());
                command = executable; arguments = string.Empty;
            }
            else return new("Unsupported programming language.", new());

            var results = new List<ExecutionResultDto>();
            foreach (var test in request.TestCases)
            {
                var run = await RunAsync(command, arguments, test.Input, workDir, _executionTimeoutMs);
                var actual = Normalize(run.Output);
                var expected = Normalize(test.Output);
                results.Add(new(test.TestCaseId, test.Input, test.Output,
                    run.TimedOut ? "Execution timed out." : (run.ExitCode == 0 ? run.Output.TrimEnd() : run.Error.TrimEnd()),
                    run.TimedOut || run.ExitCode != 0 ? "Error" : actual == expected ? "Passed" : "Failed"));
            }
            return new(null, results);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or DirectoryNotFoundException)
        {
            return new($"The {request.Language} compiler/runtime is unavailable on the execution server. Please contact the administrator. ({ex.Message})", new());
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { }
        }
    }

    private static string ResolveToolPath(string? configuredPath, string fallbackCommand)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return fallbackCommand;
        if (!OperatingSystem.IsWindows() && Regex.IsMatch(configuredPath, @"^[A-Za-z]:[\\/]")) return fallbackCommand;
        if (!Path.IsPathRooted(configuredPath) || File.Exists(configuredPath)) return configuredPath;
        return fallbackCommand;
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n").Trim();

    private static async Task<(int ExitCode, string Output, string Error, bool TimedOut)> RunAsync(
        string fileName, string arguments, string input, string workingDirectory, int timeoutMs)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(fileName, arguments) {
            WorkingDirectory = workingDirectory, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        process.Start();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(timeoutMs);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { try { process.Kill(true); } catch { } return (-1, await outputTask, await errorTask, true); }
        return (process.ExitCode, await outputTask, await errorTask, false);
    }
}
