using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TrainingAndAssessmentWebAPI.Models;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class InteractiveProgramService : IDisposable
{
    private sealed class Session(Process process, string workDirectory)
    {
        public Process Process { get; } = process;
        public string WorkDirectory { get; } = workDirectory;
        public StringBuilder Output { get; } = new();
        public StringBuilder Error { get; } = new();
        public object SyncRoot { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, byte> _remoteSessions = new();
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string? _remoteBaseUrl;

    public InteractiveProgramService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(90);
        _remoteBaseUrl = configuration.GetSection("CodeExecution")["RemoteBaseUrl"]?.TrimEnd('/');
    }

    public async Task<InteractiveProgramResponseDto> StartAsync(StartInteractiveProgramDto request)
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), "TrainingAssessment", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        try
        {
            var section = _configuration.GetSection("CodeExecution");
            var language = request.Language.Trim().ToLowerInvariant();
            string command;
            var arguments = new List<string>();

            if (language == "python")
            {
                var source = Path.Combine(workDirectory, "solution.py");
                await File.WriteAllTextAsync(source, request.Code);
                command = ResolveToolPath(section["PythonPath"], OperatingSystem.IsWindows() ? "python" : "python3");
                arguments.Add(source);
            }
            else if (language == "java")
            {
                var match = Regex.Match(request.Code, @"\bpublic\s+(?:final\s+)?class\s+([A-Za-z_$][A-Za-z0-9_$]*)");
                if (!match.Success) match = Regex.Match(request.Code, @"\bclass\s+([A-Za-z_$][A-Za-z0-9_$]*)");
                var className = match.Success ? match.Groups[1].Value : "Main";
                var source = Path.Combine(workDirectory, $"{className}.java");
                await File.WriteAllTextAsync(source, request.Code);
                var compile = await RunToCompletionAsync(ResolveToolPath(section["JavaCompilerPath"], "javac"), [source], workDirectory);
                if (compile.ExitCode != 0)
                {
                    Directory.Delete(workDirectory, true);
                    return new(null, string.Empty, compile.Error, false);
                }
                command = ResolveToolPath(section["JavaRuntimePath"], "java");
                arguments.AddRange(["-cp", workDirectory, className]);
            }
            else if (language == "c")
            {
                var source = Path.Combine(workDirectory, "solution.c");
                var executableName = OperatingSystem.IsWindows() ? "solution.exe" : "solution";
                var executable = Path.Combine(workDirectory, executableName);
                await File.WriteAllTextAsync(source, request.Code);
                var compile = await RunToCompletionAsync(ResolveToolPath(section["CCompilerPath"], "gcc"), [source, "-o", executable], workDirectory);
                if (compile.ExitCode != 0)
                {
                    Directory.Delete(workDirectory, true);
                    return new(null, string.Empty, compile.Error, false);
                }
                command = executable;
            }
            else
            {
                Directory.Delete(workDirectory, true);
                return new(null, string.Empty, "Unsupported programming language.", false);
            }

            var startInfo = CreateStartInfo(command, arguments, workDirectory);
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var session = new Session(process, workDirectory);
            process.OutputDataReceived += (_, e) => AppendLine(session.Output, session.SyncRoot, e.Data);
            process.ErrorDataReceived += (_, e) => AppendLine(session.Error, session.SyncRoot, e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var sessionId = Guid.NewGuid().ToString("N");
            _sessions[sessionId] = session;
            await Task.Delay(150);
            return ReadResponse(sessionId, session);
        }
        catch (Exception ex)
        {
            try { Directory.Delete(workDirectory, true); } catch { }
            if (!string.IsNullOrWhiteSpace(_remoteBaseUrl) && ex is System.ComponentModel.Win32Exception or FileNotFoundException or DirectoryNotFoundException)
                return await StartRemoteAsync(request);
            return new(null, string.Empty, ex.Message, false);
        }
    }

    public async Task<InteractiveProgramResponseDto> SendInputAsync(string sessionId, string input)
    {
        if (_remoteSessions.ContainsKey(sessionId))
            return await SendRemoteInputAsync(sessionId, input);
        if (!_sessions.TryGetValue(sessionId, out var session))
            return new(null, string.Empty, "Execution session has ended. Click Execute to start again.", false);
        if (session.Process.HasExited) return ReadResponse(sessionId, session);

        await session.Process.StandardInput.WriteLineAsync(input);
        await session.Process.StandardInput.FlushAsync();
        await Task.Delay(200);
        return ReadResponse(sessionId, session);
    }

    public void Stop(string sessionId)
    {
        if (_remoteSessions.TryRemove(sessionId, out _))
        {
            _ = StopRemoteAsync(sessionId);
            return;
        }
        if (_sessions.TryRemove(sessionId, out var session)) DisposeSession(session);
    }

    private async Task<InteractiveProgramResponseDto> StartRemoteAsync(StartInteractiveProgramDto request)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync($"{_remoteBaseUrl}/interactive/start", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<InteractiveProgramResponseDto>()
                ?? new(null, string.Empty, "The remote execution service returned an empty response.", false);
            if (result.IsRunning && !string.IsNullOrWhiteSpace(result.SessionId)) _remoteSessions[result.SessionId] = 0;
            return result;
        }
        catch (Exception ex)
        {
            return new(null, string.Empty, $"The remote execution service is unavailable. Please try again. ({ex.Message})", false);
        }
    }

    private async Task<InteractiveProgramResponseDto> SendRemoteInputAsync(string sessionId, string input)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync($"{_remoteBaseUrl}/interactive/{Uri.EscapeDataString(sessionId)}/input", new InteractiveProgramInputDto(input));
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<InteractiveProgramResponseDto>()
                ?? new(null, string.Empty, "The remote execution service returned an empty response.", false);
            if (!result.IsRunning) _remoteSessions.TryRemove(sessionId, out _);
            return result;
        }
        catch (Exception ex)
        {
            _remoteSessions.TryRemove(sessionId, out _);
            return new(null, string.Empty, $"Remote execution failed. ({ex.Message})", false);
        }
    }

    private async Task StopRemoteAsync(string sessionId)
    {
        try { await _httpClient.DeleteAsync($"{_remoteBaseUrl}/interactive/{Uri.EscapeDataString(sessionId)}"); }
        catch { }
    }

    private InteractiveProgramResponseDto ReadResponse(string sessionId, Session session)
    {
        Thread.Sleep(25);
        string output;
        string error;
        lock (session.SyncRoot)
        {
            output = session.Output.ToString().TrimEnd();
            error = session.Error.ToString().TrimEnd();
            session.Output.Clear();
            session.Error.Clear();
        }
        var running = !session.Process.HasExited;
        if (!running && _sessions.TryRemove(sessionId, out var completed)) DisposeSession(completed);
        return new(sessionId, output, error, running);
    }

    private static void AppendLine(StringBuilder buffer, object syncRoot, string? value)
    {
        if (value is null) return;
        lock (syncRoot) buffer.AppendLine(value);
    }

    private static ProcessStartInfo CreateStartInfo(string command, IEnumerable<string> arguments, string workDirectory)
    {
        var info = new ProcessStartInfo(command) {
            WorkingDirectory = workDirectory, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }

    private static string ResolveToolPath(string? configuredPath, string fallbackCommand)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return fallbackCommand;
        if (!OperatingSystem.IsWindows() && Regex.IsMatch(configuredPath, @"^[A-Za-z]:[\\/]")) return fallbackCommand;
        if (!Path.IsPathRooted(configuredPath) || File.Exists(configuredPath)) return configuredPath;
        return fallbackCommand;
    }

    private static async Task<(int ExitCode, string Error)> RunToCompletionAsync(string command, IEnumerable<string> arguments, string workDirectory)
    {
        using var process = new Process { StartInfo = CreateStartInfo(command, arguments, workDirectory) };
        process.Start();
        process.StandardInput.Close();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await errorTask);
    }

    private static void DisposeSession(Session session)
    {
        try { if (!session.Process.HasExited) session.Process.Kill(true); } catch { }
        session.Process.Dispose();
        try { Directory.Delete(session.WorkDirectory, true); } catch { }
    }

    public void Dispose()
    {
        foreach (var sessionId in _sessions.Keys) Stop(sessionId);
    }
}
