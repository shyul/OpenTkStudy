using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Nitride.EE;
using Nitride.WindowsNativeMethods;

// associated with an assembly.

[assembly: AssemblyDescription("")]
[assembly: AssemblyCopyright("Copyright ©  2022")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: Guid("b48088a3-f803-47cd-ac76-e6a616ab19c8")]

namespace OpenTkStudy
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Process.PriorityClass = ProcessPriorityClass.High;

            ExeFileLoation = ExecutingAssembly.Location;
            ExeFileDirectory = System.IO.Path.GetDirectoryName(ExeFileLoation);
            Console.WriteLine("ExeDir = " + ExeFileDirectory);

            if (InstanceMutex.WaitOne(TimeSpan.Zero, true))
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    //Application.RunFetch(new MainForm());

                    Application.Run(new MainForm()
                    {
                        Text = TitleText
                    });
                }
                else
                {
                    MessageBox.Show("Minimum Windows 10 64-bit is required to run this application :)");
                }

                InstanceMutex.ReleaseMutex();
            }
            else
            {
                // send our Win32 message to make the currently running instance
                // jump on top of all the other windows if the pacmio window is registered.
                User32.PostMessage(HWND.BROADCAST, SHOW_WINDOW_MSG, IntPtr.Zero, IntPtr.Zero);
            }
        }

        internal static Process Process { get; } = Process.GetCurrentProcess();
        internal static Assembly ExecutingAssembly { get; } = typeof(Program).Assembly;  // Assembly.GetExecutingAssembly();
        internal static string GUID { get; } = ExecutingAssembly.GetCustomAttribute<GuidAttribute>().Value;
        internal static Mutex InstanceMutex { get; } = new(true, GUID);
        internal static int SHOW_WINDOW_MSG { get; } = User32.RegisterWindowMessage("SHOW_TEST_APP " + GUID);
        internal static string TitleText => ExecutingAssembly.GetCustomAttribute<AssemblyTitleAttribute>().Title + " - Rev " + Application.ProductVersion;

        internal static string ExeFileLoation { get; private set; }

        internal static string ExeFileDirectory { get; private set; }
    }
}

