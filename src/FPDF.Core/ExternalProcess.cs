using System.Diagnostics;

namespace FPDF.Core;

internal static class ExternalProcess
{
    public static async Task<string> RunAsync(string executable, IEnumerable<string> arguments, string engineName, CancellationToken cancellation, IProgress<string>? progress = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!File.Exists(executable)) throw new FileNotFoundException($"{engineName} não encontrado. Localize seu executável.");
        var args = arguments.ToArray();
        if (args.Sum(arg => (long)arg.Length + 3) + executable.Length > 30_000)
            throw new ArgumentException("Há muitos arquivos ou intervalos para uma operação. Divida a seleção em grupos menores.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = true };
        if (engineName == "Ghostscript")
        {
            info.Environment.Remove("GS_OPTIONS");
            var root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(executable))!);
            if (root is not null && Directory.Exists(Path.Combine(root, "Resource", "Init")))
            {
                info.Environment["GS_LIB"] = string.Join(Path.PathSeparator, new[] { "Resource/Init", "lib", "Resource", "Resource/Font", "Resource/CMap", "iccprofiles" }.Select(p => Path.Combine(root, p.Replace('/', Path.DirectorySeparatorChar))));
                info.Environment["GS_FONTPATH"] = Path.Combine(root, "Resource", "Font");
            }
        }
        foreach (var arg in args) info.ArgumentList.Add(arg);
        if (environment is not null) foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        using var process = new Process { StartInfo = info };
        process.Start();
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        async Task<string> ReadErrors()
        {
            var messages = new System.Text.StringBuilder();
            while (await process.StandardError.ReadLineAsync() is string line)
                if (line.StartsWith("FPDF:", StringComparison.Ordinal)) progress?.Report(line[5..]);
                else if (messages.Length < 8_000) messages.AppendLine(line);
            return messages.ToString();
        }
        var stderr = ReadErrors();
        using var registration = cancellation.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
        try { await process.WaitForExitAsync(cancellation); }
        catch (OperationCanceledException)
        {
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }
        var output = await stdout;
        var error = await stderr;
        cancellation.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"O {engineName} não conseguiu processar o documento. Ele pode estar protegido por senha, danificado ou usar recursos incompatíveis.\n\n" + error.Trim());
        return output;
    }
}
