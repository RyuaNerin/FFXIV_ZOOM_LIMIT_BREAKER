using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FFXIV_ZoomLimitBreaker
{
    public partial class Form1 : Form
    {
        [Flags]
        enum Fovs
        {
            All = Fov | Zoom | ZoomCur | Init,
            Fov = 1,
            Zoom = 2,
            ZoomCur = 4,
            ZoomMin = 8,
            Init = 16
        }
        struct Fdata
        {
            public float Fov;
            public float Zoom;
            public float ZoomMin;
        }
        private struct SigInfo
        {
            public int Pid;
            public IntPtr hProcess;
            public IntPtr AddrFromSignature;
        }
        private Dictionary<int, SigInfo> sig = new Dictionary<int, SigInfo>();


        public const string Signiture = "8B****85C074**83F8**75**488B05********83F8**74**4898488D0D********";
        public const int current = 0x128;
        public const int zoommin = 0x12C;
        public const int zoommax = 0x130;
        public const int fofvcur = 0x134;
        public const int fofvmin = 0x138;
        public const int fofvmax = 0x13C;

        public Form1()
        {
            Console.WriteLine("test");
            Debug.WriteLine("test");
            InitializeComponent();
            getValues();
        }

        private void getValues()
        {
            var a = Convert.ToDecimal(Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\ZOOMHACK", "FOV", 0.78f));
            var b = Convert.ToDecimal(Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\ZOOMHACK", "ZOOMMAX", 20.0f));
            var c = Convert.ToDecimal(Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\ZOOMHACK", "ZOOMMIN", 1.5f));
            numericUpDown1.Value = a;
            numericUpDown2.Value = b;
            numericUpDown3.Value = c;
        }

        private void saveValues()
        {
            Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\ZOOMHACK", "FOV", numericUpDown1.Value);
            Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\ZOOMHACK", "ZOOMMAX", numericUpDown2.Value);
            Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\ZOOMHACK", "ZOOMMIN", numericUpDown3.Value);
        }

        private void Default_Click(object sender, EventArgs e)
        {
            numericUpDown1.Value = (decimal)0.78;
            numericUpDown2.Value = (decimal)20.0;
            numericUpDown3.Value = (decimal)1.5;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetAndSave(Fovs.All);
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            SetAndSave(Fovs.Fov);
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            SetAndSave(Fovs.Zoom);
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            SetAndSave(Fovs.ZoomMin);
        }

        private void numericUpDown1_KeyUp(object sender, KeyEventArgs e)
        {
            SetAndSave(Fovs.Fov);
        }

        private void numericUpDown2_KeyUp(object sender, KeyEventArgs e)
        {
            SetAndSave(Fovs.Zoom);
        }

        private void numericUpDown3_KeyUp(object sender, KeyEventArgs e)
        {
            SetAndSave(Fovs.ZoomMin);
        }

        private void SetAndSave(Fovs fovs)
        {
            var data = new Fdata
            {
                Fov = (float)this.numericUpDown1.Value,
                Zoom = (float)this.numericUpDown2.Value,
                ZoomMin = (float)this.numericUpDown3.Value,
            };

            try
            {
                new Thread(new ThreadStart(() =>
                {
                    ProcessLoad(fovs, data);
                })).Start();
            }
            catch { }
            Thread.Sleep(30);

            saveValues();
        }

        private void ProcessLoad(Fovs fov, Fdata data)
        {
            foreach (var i in Process.GetProcessesByName("ffxiv_dx11"))
            {
                SigInfo n;

                lock (sig)
                {
                    if (!sig.ContainsKey(i.Id))
                    {
                        n = new SigInfo
                        {
                            Pid = i.Id,
                            hProcess = OpenProcess(ProcessAccessFlags.All, false, i.Id)
                        };
                        if (n.hProcess == IntPtr.Zero)
                            continue;
                        n.AddrFromSignature = Scan(n.hProcess, Signiture, i.MainModule.BaseAddress, i.MainModule.ModuleMemorySize);

                        sig.Add(n.Pid, n);
                    }
                    else
                        n = sig[i.Id];
                }

                int[] vx = new int[] { (int)(n.AddrFromSignature.ToInt64() - i.MainModule.BaseAddress.ToInt64()) };
                
                var addr = ReadPointer(n.hProcess, n.AddrFromSignature);
                if (addr == IntPtr.Zero)
                    continue;

                if (fov.HasFlag(Fovs.Zoom))
                {
                    if (fov.HasFlag(Fovs.Init))
                        data.Zoom = 20.0f;
                    
                    Write(data.Zoom, n.hProcess, addr + zoommax);
                }

                if (fov.HasFlag(Fovs.ZoomCur))
                {
                    Write(data.Zoom, n.hProcess, addr + current);
                }

                if (fov.HasFlag(Fovs.ZoomMin))
                {
                    Write(data.ZoomMin, n.hProcess, addr + zoommin);
                }

                if (fov.HasFlag(Fovs.Fov))
                {
                    Write(data.Fov, n.hProcess, addr + fofvcur);
                    Write(data.Fov, n.hProcess, addr + fofvmax);
                }
            }
        }

        private static void Write(float value, IntPtr handle, IntPtr address)
        {
            var buffer = BitConverter.GetBytes(value);
            IntPtr written;
            if (!(WriteProcessMemory(handle, address, buffer, buffer.Length, out written)))
            {
                
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
            ProcessAccessFlags dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)]
                bool bInheritHandle,
            int dwProcessId);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            IntPtr nSize,
            [Out]
                out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hHandle);

        public static IntPtr Scan(IntPtr hProcess, string pattern, IntPtr baseAddr, long maxsize = 0)
        {
            var patArray = GetPatternArray(pattern);

            var posPtr = baseAddr;
            var maxPtr = new IntPtr(baseAddr.ToInt64() + maxsize - patArray.Length);

            var buff = new byte[4096];
            IntPtr read;
            int index;

            try
            {
                while (posPtr.ToInt64() < maxPtr.ToInt64())
                {
                    read = new IntPtr(Math.Min(maxPtr.ToInt64() - posPtr.ToInt64(), 4096));
                    if (ReadProcessMemory(hProcess, posPtr, buff, read, out read))
                    {
                        index = FindArray(buff, patArray, 0, read.ToInt32());
                        if (index != -1)
                        {
                            long ptr = posPtr.ToInt64() + index;
                            ptr += patArray.Length;
                            ptr += BitConverter.ToInt32(buff, index + patArray.Length - 4);
                            return (IntPtr)ptr;
                        }
                        posPtr = new IntPtr(posPtr.ToInt64() + read.ToInt64() - patArray.Length + 1);
                    }
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        private static byte?[] GetPatternArray(string pattern)
        {
            byte?[] arr = new byte?[pattern.Length / 2];

            for (int i = 0; i < (pattern.Length / 2); i++)
            {
                string str = pattern.Substring(i * 2, 2);
                if (str == "**")
                    arr[i] = null;
                else
                    arr[i] = new byte?(Convert.ToByte(str, 0x10));
            }

            return arr;
        }

        private static int FindArray(byte[] buff, byte?[] pattern, int startIndex, int len)
        {
            len = Math.Min(buff.Length, len);

            int i, j;
            for (i = startIndex; i < (len - pattern.Length); i++)
            {
                for (j = 0; j < pattern.Length; j++)
                    if (pattern[j].HasValue && buff[i + j] != pattern[j].Value)
                        break;

                if (j == pattern.Length)
                    return i;
            }

            return -1;
        }

        public static IntPtr ReadPointer(IntPtr handle,IntPtr address)
        {
            byte[] lpBuffer = new byte[8];
            if (!ReadProcessMemory(handle, address, lpBuffer, 8, out IntPtr read) || read.ToInt64() != 8)
                return IntPtr.Zero;

            return new IntPtr(BitConverter.ToInt64(lpBuffer, 0));
        }

        static class NativeMethods
        {

        }
    }
}
