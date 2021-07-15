using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SS14.Changelog
{
    public static class Utility
    {
        // Taken from https://github.com/aspnet/AspLabs/blob/41de6d7a808742c5db72f537018dee85bcdafca8/src/WebHooks/src/Microsoft.AspNetCore.WebHooks.Receivers/Filters/WebHookVerifySignatureFilter.cs#L158
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static bool SecretEqual(ReadOnlySpan<byte> inputA, ReadOnlySpan<byte> inputB)
        {
            if (inputA.Length != inputB.Length)
            {
                return false;
            }

            var areSame = true;
            for (var i = 0; i < inputA.Length; i++)
            {
                areSame &= inputA[i] == inputB[i];
            }

            return areSame;
        }

        public static byte[] FromHex(ReadOnlySpan<char> hex)
        {
            var arr = new byte[hex.Length >> 1];

            for (var i = 0; i < arr.Length; i++)
            {
                arr[i] = byte.Parse(hex.Slice(i << 1, 2), NumberStyles.HexNumber);
            }

            return arr;
        }

        // From https://stackoverflow.com/a/64901122
        public static void Kill(this Process process, Signum sig)
        {
            if (!OperatingSystem.IsLinux())
                throw new NotSupportedException();
            
            _ = sys_kill(process.Id, (int) sig);
        }
        
        // ReSharper disable once StringLiteralTypo
        [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
        private static extern int sys_kill(int pid, int sig);

        [SuppressMessage("ReSharper", "IdentifierTypo")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "CA1069")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        [SuppressMessage("ReSharper", "EnumUnderlyingTypeIsInt")]
        [SuppressMessage("ReSharper", "CommentTypo")]
        public enum Signum : int
        {
            SIGHUP = 1, // Hangup (POSIX).
            SIGINT = 2, // Interrupt (ANSI).
            SIGQUIT = 3, // Quit (POSIX).
            SIGILL = 4, // Illegal instruction (ANSI).
            SIGTRAP = 5, // Trace trap (POSIX).
            SIGABRT = 6, // Abort (ANSI).
            SIGIOT = 6, // IOT trap (4.2 BSD).
            SIGBUS = 7, // BUS error (4.2 BSD).
            SIGFPE = 8, // Floating-point exception (ANSI).
            SIGKILL = 9, // Kill, unblockable (POSIX).
            SIGUSR1 = 10, // User-defined signal 1 (POSIX).
            SIGSEGV = 11, // Segmentation violation (ANSI).
            SIGUSR2 = 12, // User-defined signal 2 (POSIX).
            SIGPIPE = 13, // Broken pipe (POSIX).
            SIGALRM = 14, // Alarm clock (POSIX).
            SIGTERM = 15, // Termination (ANSI).
            SIGSTKFLT = 16, // Stack fault.
            SIGCLD = SIGCHLD, // Same as SIGCHLD (System V).
            SIGCHLD = 17, // Child status has changed (POSIX).
            SIGCONT = 18, // Continue (POSIX).
            SIGSTOP = 19, // Stop, unblockable (POSIX).
            SIGTSTP = 20, // Keyboard stop (POSIX).
            SIGTTIN = 21, // Background read from tty (POSIX).
            SIGTTOU = 22, // Background write to tty (POSIX).
            SIGURG = 23, // Urgent condition on socket (4.2 BSD).
            SIGXCPU = 24, // CPU limit exceeded (4.2 BSD).
            SIGXFSZ = 25, // File size limit exceeded (4.2 BSD).
            SIGVTALRM = 26, // Virtual alarm clock (4.2 BSD).
            SIGPROF = 27, // Profiling alarm clock (4.2 BSD).
            SIGWINCH = 28, // Window size change (4.3 BSD, Sun).
            SIGPOLL = SIGIO, // Pollable event occurred (System V).
            SIGIO = 29, // I/O now possible (4.2 BSD).
            SIGPWR = 30, // Power failure restart (System V).
            SIGSYS = 31, // Bad system call.
            SIGUNUSED = 31
        }
    }
}