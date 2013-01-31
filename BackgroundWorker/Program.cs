using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using TaskScheduler;

namespace BackgroundWorker
{
    public class Program
    {
        public static bool Is64BitProcess
        {
            get { return IntPtr.Size == 8; }
        }

        public static bool Is64BitOperatingSystem
        {
            get
            {
                // Clearly if this is a 64-bit process we must be on a 64-bit OS.
                if (Is64BitProcess)
                    return true;
                // Ok, so we are a 32-bit process, but is the OS 64-bit?
                // If we are running under Wow64 than the OS is 64-bit.
                bool isWow64;
                return ModuleContainsFunction("kernel32.dll", "IsWow64Process") && IsWow64Process(GetCurrentProcess(), out isWow64) && isWow64;
            }
        }

        static bool ModuleContainsFunction(string moduleName, string methodName)
        {
            IntPtr hModule = GetModuleHandle(moduleName);
            if (hModule != IntPtr.Zero)
                return GetProcAddress(hModule, methodName) != IntPtr.Zero;
            return false;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        extern static bool IsWow64Process(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool isWow64);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        extern static IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        extern static IntPtr GetModuleHandle(string moduleName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        extern static IntPtr GetProcAddress(IntPtr hModule, string methodName);
        [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyEx")]
            public static extern int RegOpenKeyEx_DllImport(
                UIntPtr hKey,
                string subKey,
                uint options,
                int sam,
                out IntPtr phkResult);

        [DllImport("advapi32.dll", EntryPoint = "RegSetValueEx")]
        static extern int RegQueryValueEx_DllImport(
            IntPtr hKey,
            string lpValueName,
            int lpReserved,
            out uint lpType,
            System.Text.StringBuilder lpData,
            ref uint lpcbData);
        
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern int RegSetValueEx(
            IntPtr hKey,
            [MarshalAs(UnmanagedType.LPStr)] string lpValueName, 
            int Reserved, 
            Microsoft.Win32.RegistryValueKind dwType,
            [MarshalAs(UnmanagedType.LPStr)] string lpData, 
            int cbData);

        public static void Main(string[] args)
        {
            /// "Hi, I'm Inumedia from http://inumedia.net  Email: inumedia@inumedia.net
            /// Feel free to contact me if you have any questions, comments, 
            /// or just feel like talking to someone.";
            string systemfolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string processor_architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            if (Is64BitOperatingSystem)
                systemfolder = Environment.ExpandEnvironmentVariables("%WINDIR%\\sysnative");
            string backgrounddirectory = string.Format("{0}\\oobe\\info\\backgrounds", systemfolder);
            string homedirectory = string.Format("{0}\\CustomLogonBackgrounds", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

            string[] data;
            if (File.Exists(string.Format("{0}\\{1}.txt", backgrounddirectory, "backgroundworkeruserdata")))
                if ((data = File.ReadAllLines(string.Format("{0}\\{1}.txt", backgrounddirectory, "backgroundworkeruserdata"))).Length == 1)
                    homedirectory = data[0];
            if (!Directory.Exists(homedirectory))
                Directory.CreateDirectory(homedirectory);
            if (!Directory.Exists(backgrounddirectory))
                Directory.CreateDirectory(backgrounddirectory);
            Install(backgrounddirectory, homedirectory);

            List<string> history;
            List<string> files = new List<string>(Directory.GetFiles(homedirectory, "*.jpg"));
            if (File.Exists(string.Format("{0}\\history.txt", backgrounddirectory)))
            {
                history = new List<string>(File.ReadAllLines(string.Format("{0}\\history.txt", backgrounddirectory)));
                if (history.Count >= files.Count)
                    history = new List<string>();
                else
                    RemoveHistoryItems(files, history);
            }else history = new List<string>();
            if (files.Count == 0) return;
            Random rand = new Random();
            int id = 0;
            if(history.Count == 0)
            while (File.Exists(string.Format("{0}\\{1}.jpg", homedirectory, id.ToString()))) id = rand.Next();

            if (File.Exists(string.Format(@"{0}\backgroundDefault.jpg", backgrounddirectory)))
                File.Move(string.Format(@"{0}\backgroundDefault.jpg", backgrounddirectory), history.Count == 0 ? string.Format("{0}\\{1}.jpg", homedirectory, id.ToString()) : history[history.Count - 1]);
            string newbackground = null;
            while (newbackground == null)
            {
                newbackground = files[rand.Next(files.Count)];
                if (new FileInfo(newbackground).Length > 0x3fc00L)
                    newbackground = null;
            }

            history.Add(newbackground);
            File.Move(newbackground, string.Format(@"{0}\backgroundDefault.jpg", backgrounddirectory));

            File.WriteAllLines(string.Format("{0}\\history.txt", backgrounddirectory), history.ToArray());
        }

        public static void RemoveHistoryItems(List<string> pFiles, List<string> pHistory)
        {
            for (int i = 0; i < pHistory.Count; ++i)
                pFiles.RemoveAll((a) => a == pHistory[i]);
        }
        
        /// <summary>
        /// Handles the initial install of the program
        /// </summary>
        /// <param name="pBackgroundDirectory">The directory where the backgrounds are originally stored.</param>
        /// <param name="pHomeDirectory">The target directory .</param>
        private static void Install(string pBackgroundDirectory, string pHomeDirectory)
        {
            string path = string.Format("{0}\\BackgroundWorker.exe", Environment.GetEnvironmentVariable("WINDIR"));
            string name = Process.GetCurrentProcess().MainModule.FileName;
            if (name.Equals(path, StringComparison.CurrentCultureIgnoreCase))
                return;
            File.WriteAllLines(string.Format(@"{0}\{1}.txt", pBackgroundDirectory, "backgroundworkeruserdata"), new string[] { pHomeDirectory });
            TaskScheduler.TaskScheduler mScheduler = new TaskScheduler.TaskScheduler();

            mScheduler.Connect();

            ITaskFolder mFolder = mScheduler.GetFolder("");

            File.WriteAllBytes(path, File.ReadAllBytes(name));
            IRegisteredTask mTask = mFolder.RegisterTask("BackgroundWorker", string.Format(Properties.Resources.XML, path), 0x6, null, null, _TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN);

            UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);
            IntPtr KeyPtr = IntPtr.Zero;
            RegOpenKeyEx_DllImport(HKEY_LOCAL_MACHINE, @"Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\Background\\", 0, 2 | 0x100, out KeyPtr);
            if (KeyPtr != IntPtr.Zero)
                RegSetValueEx(KeyPtr, "OEMBackground", 0, RegistryValueKind.DWord, ((char)(byte)1).ToString(), 4);
        }

        static bool TaskExists(ITaskFolder mFolder)
        {
            bool mFound = false;
            IRegisteredTaskCollection mTasks = mFolder.GetTasks(0);
            foreach (IRegisteredTask mTask in mTasks)
                mFound |= mTask.Name == "BackgroundWorker";
            return mFound;
        }
    }
}