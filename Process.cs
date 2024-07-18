using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GttrcrGist
{
    public static class Process
    {
        public struct OSCommand
        {
            public OSPlatform OSPlatform { get; set; }
            public string? Executable { get; set; }
            public string? Command { get; set; }
            public Action ProcessStartedCallback { get; set; }
        }

        public static List<string> Run(string? executable = null, string? command = null, Action? processStartedCallback = null)
        {
            System.Diagnostics.Process process = new();
            if (executable == null)
            {
                OSPlatform os = GetOS();
                if (os.Equals(OSPlatform.Linux))
                {
                    executable = "/bin/bash";
                    command = "-c \"" + command + "\"";
                }
                else if (os.Equals(OSPlatform.Windows))
                {
                    executable = "C:\\Windows\\system32\\cmd.exe";
                    command = "/c \"" + command + "\"";
                }
                else
                    throw new PlatformNotSupportedException();
            }

            process.StartInfo.FileName = executable;
            if (!string.IsNullOrEmpty(command))
                process.StartInfo.Arguments = command;

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;

            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            StringBuilder stdOutput = new();
            process.OutputDataReceived += (sender, args) => stdOutput.AppendLine(args.Data); // Use AppendLine rather than Append since args.Data is one line of output, not including the newline character.

            string? stdError = null;
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                processStartedCallback?.Invoke();
                stdError = process.StandardError.ReadToEnd();
                process.WaitForExit();
            }
            catch (Exception e)
            {
                throw new Exception("OS error while executing " + Format(executable, command) + ": " + e.Message, e);
            }

            if (process.ExitCode == 0)
                return [.. stdOutput.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)];
            else
            {
                StringBuilder message = new();
                if (!string.IsNullOrEmpty(stdError))
                    message.AppendLine(stdError);

                if (stdOutput.Length != 0)
                {
                    message.AppendLine("Std output:");
                    message.AppendLine(stdOutput.ToString());
                }

                throw new Exception(Format(executable, command) + " finished with exit code = " + process.ExitCode + ": " + message);
            }
        }

        public static bool Exists(string command)
        {
            OSPlatform os = GetOS();
            if (os.Equals(OSPlatform.Linux))
                command = "command -v " + command;
            else if (os.Equals(OSPlatform.Windows))
                command = "WHERE " + command;
            else
                throw new PlatformNotSupportedException();

            try
            {
                List<string>? output = Run(null, command);
                if (output != null && output.Count == 1)
                    return File.Exists(output[0]);
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static List<List<string>> Run(List<OSCommand> oSCommands)
        {
            OSPlatform os = GetOS();
            if (!oSCommands.Any(x => x.OSPlatform.Equals(os)))
                throw new PlatformNotSupportedException();

            List<OSCommand> oSCmds = oSCommands.Where(x => x.OSPlatform.Equals(os)).ToList();
            return oSCmds.Select(x => Run(x.Executable, x.Command, x.ProcessStartedCallback)).ToList();
        }

        private static string Format(string filename, string? arguments)
        {
            return "[" + filename + (string.IsNullOrEmpty(arguments) ? string.Empty : " " + arguments) + "]";
        }

        public static OSPlatform GetOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                return OSPlatform.FreeBSD;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OSPlatform.Linux;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OSPlatform.OSX;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OSPlatform.Windows;

            throw new PlatformNotSupportedException();
        }
    }
}