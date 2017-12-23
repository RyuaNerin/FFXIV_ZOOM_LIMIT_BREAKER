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
        public string Signiture = "8B****85C074**83F8**75**488B05********83F8**74**4898488D0D********";
        public int current = 0x128;
        public int zoommin = 0x12C;
        public int zoommax = 0x130;
        public int fofvcur = 0x134;
        public int fofvmin = 0x138;
        public int fofvmax = 0x13C;
        public Form1()
        {
            Console.WriteLine("test");
            Debug.WriteLine("test");
            InitializeComponent();
            getValues();
        }

        private IntPtr m_hProcess;

        private void Form1_Load(object sender, EventArgs e)
        {
            ProcessLoad(0);
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

        private Dictionary<int, IntPtr> sig = new Dictionary<int, IntPtr>();

        private void ProcessLoad(int isFov)
        {
            foreach (var i in Process.GetProcesses())
            {
                Console.WriteLine(i.ProcessName);
                if (i.ProcessName == "ffxiv_dx11")
                {
                    this.m_hProcess = OpenProcess(ProcessAccessFlags.All, false, i.Id);
                    var n = IntPtr.Zero;

                    if (!sig.ContainsKey(i.Id))
                        n = Scan(this.m_hProcess, Signiture, i.MainModule.BaseAddress, i.MainModule.ModuleMemorySize);
                    else
                        n = sig[i.Id];

                    float dfov = 0.78f;
                    float dzoom = 20.0f;
                    float dzooms = 20.0f;
                    float dzoommin = 1.5f;

                    Invoke((MethodInvoker)delegate
                    {
                        dfov = (float)numericUpDown1.Value;
                        dzoom = dzooms = (float)numericUpDown2.Value;
                        dzoommin = (float)numericUpDown3.Value;
                    });

                    bool isfov = false;
                    bool iszoom = false;
                    bool iszoomcur = false;
                    bool iszoommin = false;

                    int[] vx = new int[] { (int)(n.ToInt64() - i.MainModule.BaseAddress.ToInt64()) };

                    switch (isFov)
                    {
                        case 0:
                            isfov = iszoom = iszoomcur = true;
                            break;
                        case 1:
                            isfov = true;
                            break;
                        case 2:
                            iszoom = true;
                            break;
                        case 3:
                            iszoommin = true;
                            break;
                        case 4:
                            iszoomcur = true;
                            break;
                    }

                    if(iszoom)
                    {
                        if (isFov == 0)
                            dzoom = 20.0f;

                        var addr = GetAddress(8, i, this.m_hProcess, vx, zoommax);
                        Write(dzoom, this.m_hProcess, addr);
                    }

                    if (isFov == 0)
                    {
                        var addr = GetAddress(8, i, this.m_hProcess, vx, zoommax);
                        Write(dzooms, this.m_hProcess, addr);
                    }

                    if (iszoomcur)
                    {
                        var addr = GetAddress(8, i, this.m_hProcess, vx, current);
                        Write(dzoom, this.m_hProcess, addr);
                    }

                    if(iszoommin)
                    {
                        var addr = GetAddress(8, i, this.m_hProcess, vx, zoommin);
                        Write(dzoommin, this.m_hProcess, addr);
                    }

                    if (isfov)
                    {
                        var addr = GetAddress(8, i, this.m_hProcess, vx, fofvcur);
                        Write(dfov, this.m_hProcess, addr);
                        addr = GetAddress(8, i, this.m_hProcess, vx, fofvmax);
                        Write(dfov, this.m_hProcess, addr);
                    }
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

        private static IntPtr GetAddress(byte size, Process process, IntPtr ptr, IEnumerable<int> offsets, int finalOffset)
        {
            var addr = process.MainModule.BaseAddress;
            var buffer = new byte[size];
            foreach (var offset in offsets)
            {
                IntPtr read;
                if (!(ReadProcessMemory(ptr, IntPtr.Add(addr, offset), buffer, buffer.Length, out read)))
                {
                    throw new Exception("Unable to read process memory");
                }
                addr = (size == 8)
                    ? new IntPtr(BitConverter.ToInt64(buffer, 0))
                    : new IntPtr(BitConverter.ToInt32(buffer, 0));
            }
            return IntPtr.Add(addr, finalOffset);
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

        public static IntPtr Scan(IntPtr hProcess, string pattern, IntPtr baseAddr, long maxsize = 0)
        {
            var patArray = GetPatternArray(pattern);

            var posPtr = baseAddr;
            var maxPtr = new IntPtr(baseAddr.ToInt64() + maxsize - patArray.Length);

            var buff = new byte[4096];
            IntPtr read;
            int index;

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

        public static IntPtr ReadPointer(IntPtr handle, bool isX64, IntPtr address)
        {
            int size_t = isX64 ? 8 : 4;

            byte[] lpBuffer = new byte[size_t];
            IntPtr read;
            if (!ReadProcessMemory(handle, address, lpBuffer, new IntPtr(size_t), out read) || read.ToInt64() != size_t)
                return IntPtr.Zero;

            if (isX64)
                return new IntPtr(BitConverter.ToInt64(lpBuffer, 0));
            else
                return new IntPtr(BitConverter.ToInt32(lpBuffer, 0));
        }

        public static byte[] ReadBytes(IntPtr handle, IntPtr address, int length)
        {
            if (length <= 0 || address == IntPtr.Zero)
                return null;

            byte[] lpBuffer = new byte[length];

            IntPtr read = IntPtr.Zero;

            ReadProcessMemory(handle, address, lpBuffer, new IntPtr(length), out read);

            return lpBuffer;
        }

        private void Default_Click(object sender, EventArgs e)
        {
            numericUpDown1.Value = (decimal)0.78;
            numericUpDown2.Value = (decimal)20.0;
            numericUpDown3.Value = (decimal)1.5;
        }

        private void RunSetZoom(int i)
        {
            try
            {
                new Thread(new ThreadStart(() =>
                {
                    ProcessLoad(i);
                })).Start();
            }
            catch { }
            Thread.Sleep(30);
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            RunSetZoom(1);
            saveValues();
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            RunSetZoom(2);
            saveValues();
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            RunSetZoom(3);
            saveValues();
        }

        private void numericUpDown1_KeyUp(object sender, KeyEventArgs e)
        {
            RunSetZoom(1);
            saveValues();
        }

        private void numericUpDown2_KeyUp(object sender, KeyEventArgs e)
        {
            RunSetZoom(2);
            saveValues();
        }

        private void numericUpDown3_KeyUp(object sender, KeyEventArgs e)
        {
            RunSetZoom(3);
            saveValues();
        }
    }
}
