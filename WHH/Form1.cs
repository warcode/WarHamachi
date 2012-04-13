using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.IO;
using System.Threading;
using KeyboardHooks;

namespace WHH
{
    public partial class WarHamachi : Form
    {
        //warhamachi
        public IPAddress tIP;
        System.Timers.Timer timer1;
        RegistryKey gameport;
        Int16 port;
        byte[] rawData;
        IPEndPoint iep;
        Socket raw;
        IPAddress hamIP;
        ContextMenu menu;

        //listen server
        byte[] recData;
        Socket listenSocket;
        IAsyncResult m_asynResult;
        public AsyncCallback pfnCallBack;
        public List<GameInfo> gamelist;
        byte[] rawData2;
        bool LGMEnabled;
        RegistryKey LGME;
        RegistryKey FOREVERKEY;
        int FOREVER;

        public WarHamachi()
        {
            InitializeComponent();
            //Check registry for game port
            gameport = Registry.CurrentUser.OpenSubKey("Software\\Blizzard Entertainment\\Warcraft III\\Gameplay", true);
            if (gameport != null)
            {   
                port = Int16.Parse(gameport.GetValue("netgameport").ToString());
                gameport.Close();
            }
            else { port = 6112; }

            //check registry for LGMEnabled/Disabled or set it default enabled if first run.
            LGME = Registry.CurrentUser.OpenSubKey("Software\\WarHamachi", true);
            if (LGME != null)
            {
                int e = Int16.Parse(LGME.GetValue("LanGameMonEnabled").ToString());
                if (e == 1) { LGMEnabled = true; }
                else { LGMEnabled = false; }
                LGME.Close();
            }
            else
            {
                Registry.CurrentUser.CreateSubKey("Software\\WarHamachi");
                LGME = Registry.CurrentUser.OpenSubKey("Software\\WarHamachi", true);
                LGME.SetValue("LanGameMonEnabled", 1);
                LGME.Close();
                LGMEnabled = true;
            }

            //check registry for FOREVERKEY
            FOREVERKEY = Registry.CurrentUser.OpenSubKey("Software\\WarHamachi", true);
            if (FOREVERKEY.GetValue("ForeverKey", null) != null)
            {
                int e = Int16.Parse(FOREVERKEY.GetValue("ForeverKey").ToString());
                FOREVER = e;
                FOREVERKEY.Close();
            }
            else
            {
                FOREVERKEY.SetValue("ForeverKey", 25);
                FOREVERKEY.Close();
                FOREVER = 25;
            }

            //Write custom udp packet
            byte[] rawport = new byte[4];
            BitConverter.GetBytes(port).CopyTo(rawport, 0);
            //spoof the UDP packet dest/source ports to gameport
            rawData = new byte[] { rawport[1], rawport[0], 0x17, 0xe0,    //source port, destination port
                                      0x00, 0x18, 0x00, 0x00,             //Length,	Checksum
                                      0xf7, 0x2f, 0x10, 0x00, 0x50, 0x58, //data
                                      0x33, 0x57, (byte)FOREVER, 0x00, 0x00, 0x00, //??, ??, version?
                                      0x00, 0x00, 0x00, 0x00};
            //spoof the UDP packet dest/source ports to 6112/6120
            rawData2 = new byte[] { 0x17, 0xe8, 0x17, 0xe0,         //source port, destination port
                                      0x00, 0x18, 0x00, 0x00,             //Length,	Checksum
                                      0xf7, 0x2f, 0x10, 0x00, 0x50, 0x58, //data
                                      0x33, 0x57, (byte)FOREVER, 0x00, 0x00, 0x00, //??, ??, version?
                                      0x00, 0x00, 0x00, 0x00};
            tIP = IPAddress.Parse("5.255.255.255");
            iep = new IPEndPoint(tIP, 0);

            this.ShowInTaskbar = false;
            this.Visible = false;
            menu = new ContextMenu();
            menu.MenuItems.Add(0, new MenuItem("Port: "+port+""));
            menu.MenuItems[0].Enabled = false;
            menu.MenuItems.Add(1, new MenuItem("GameMonitor", new System.EventHandler(LGM_Click)));
            if (LGMEnabled) { menu.MenuItems[1].Checked = true; }
            menu.MenuItems.Add(2, new MenuItem("About", new System.EventHandler(About_Click)));
            menu.MenuItems.Add(3, new MenuItem("Exit", new System.EventHandler(Exit_Click)));
            notifyIcon1.ContextMenu = menu;
            notifyIcon1.ShowBalloonTip(100, "WarHamachi Forever started!", "Requesting games using port: " + port + "." , ToolTipIcon.Info);
            timer1 = new System.Timers.Timer(5000);
            timer1.Elapsed += new ElapsedEventHandler(timer1_Elapsed);
            timer1.AutoReset = true;
            startTimer();

            //set up hotkeys
            /*
            KeyboardHook hook = new KeyboardHook();
            hook.KeyPressed += new EventHandler<KeyPressedEventArgs>(hook_KeyPressed);
            hook.RegisterHotKey(ModifierKeys2.Control, Keys.Home);
            */

            //get my hamachi IP
            string myHost = System.Net.Dns.GetHostName();
            System.Net.IPHostEntry myIPs = System.Net.Dns.GetHostEntry(myHost);
            foreach (System.Net.IPAddress myIP in myIPs.AddressList)
            {
                if (myIP.ToString().StartsWith("5."))
                { hamIP = myIP; break; }
            }

            //start listen server
            Thread thread = new Thread(new ThreadStart(this.listen));
            thread.IsBackground = true;
            thread.Start();
            gamelist = new List<GameInfo>(10);

        }

        //Listen Server functions

        private void listen()
        {
            this.recData = new byte[256];
            this.listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint localEP = new IPEndPoint(hamIP, 6120);
            this.listenSocket.Bind(localEP);
            this.WaitForData();
        }

        public void WaitForData()
        {
            if (this.pfnCallBack == null)
            {
                this.pfnCallBack = new AsyncCallback(this.decode);
            }
            this.m_asynResult = this.listenSocket.BeginReceive(this.recData, 0, this.recData.Length, SocketFlags.None, this.pfnCallBack, null);
            listenSocket.Poll(0, SelectMode.SelectRead);
        }

        public void decode(IAsyncResult asyn)
        {
            if ((recData[0] == 0xf7))
            {

                int read = listenSocket.EndReceive(asyn);
                GameInfo game = ExtractGameInfo(recData, read);
                string[] map = game.Map.ToString().Split('\\');
                string str = "[" + game.CurrentPlayers.ToString() + "/" + game.PlayerSlots.ToString() + "] " + game.Name.ToString() + " (" + map[map.Length - 1] + ")";
                //notifyIcon1.ShowBalloonTip(500, "LanGM", str, ToolTipIcon.Info);
                //listView1.Items.Add(str);
                bool found = false;
                for (int i = 0; i < gamelist.Count; i++)
                {
                    if (gamelist[i].Name.Equals(game.Name.ToString()))
                    {
                        if ((gamelist[i].CurrentPlayers != game.CurrentPlayers) || (!gamelist[i].Map.Equals(game.Map)) || (gamelist[i].GameId != game.GameId))
                        {
                            gamelist[i] = game;
                            if (gamelist[i].CurrentPlayers == gamelist[i].PlayerSlots)
                            {
                                notifyIcon1.ShowBalloonTip(50000, "GameMon (" + DateTime.Now.ToShortTimeString() + ")", str, ToolTipIcon.Info);
                            }
                            else
                            {
                                notifyIcon1.ShowBalloonTip(500, "GameMon (" + DateTime.Now.ToShortTimeString() + ")", str, ToolTipIcon.Info);
                            }
                        }
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    if (gamelist.Count > 25) { gamelist.Clear(); }
                    gamelist.Add(game);
                    notifyIcon1.ShowBalloonTip(500, "GameMon (" + DateTime.Now.ToShortTimeString() + ")", str, ToolTipIcon.Info);
                }
            }
            WaitForData();

        }

        //[StructLayout(LayoutKind.Sequential)]
        public struct GameInfo
        {
            public int GameId;
            public string Name;
            public string Map;
            public int Port;
            public int SlotCount;
            public int CurrentPlayers;
            public int PlayerSlots;
        }

        private byte[] Decrypt(byte[] Data, int Offset)
        {
            MemoryStream stream = new MemoryStream();
            int num = 0;
            byte num2 = 0;
            while (true)
            {
                byte num3 = Data[num + Offset];
                if (num3 == 0)
                {
                    return stream.ToArray();
                }
                if ((num % 8) == 0)
                {
                    num2 = num3;
                }
                else if ((num2 & (((int)1) << (num % 8))) == 0)
                {
                    stream.WriteByte((byte)(num3 - 1));
                }
                else
                {
                    stream.WriteByte(num3);
                }
                num++;
            }
        }

        private GameInfo ExtractGameInfo(byte[] response, int Length)
        {
            GameInfo server = new GameInfo();
            server.GameId = BitConverter.ToInt32(response, 12);
            server.Name = this.StringFromArray(response, 20);
            int offset = ((20 + server.Name.Length) + 1) + 1;
            byte[] data = this.Decrypt(response, offset);
            server.Map = this.StringFromArray(data, 13);
            server.Port = BitConverter.ToUInt16(response, Length - 2);
            server.SlotCount = BitConverter.ToInt32(response, Length - 0x16);
            server.CurrentPlayers = BitConverter.ToInt32(response, Length - 14);
            server.PlayerSlots = BitConverter.ToInt32(response, Length - 10);
            return server;
        }

        private string StringFromArray(byte[] Data, int Offset)
        {
            StringBuilder builder = new StringBuilder();
            while (true)
            {
                char ch = (char)Data[Offset++];
                if (ch == '\0')
                {
                    break;
                }
                builder.Append(ch);
            }
            return builder.ToString();
        }


        void hook_KeyPressed(object sender, KeyPressedEventArgs e)
        {
        }


        protected void Exit_Click(Object sender, System.EventArgs e)
        {
            LGME = Registry.CurrentUser.OpenSubKey("Software\\WarHamachi", true);
            if (LGMEnabled) { LGME.SetValue("LanGameMonEnabled", 1); }
            else { LGME.SetValue("LanGameMonEnabled", 0); }
            LGME.Close();
            Application.Exit();
        }

        protected void About_Click(Object sender, System.EventArgs e)
        {
            AboutBox1 AboutWH = new AboutBox1();
            AboutWH.Visible = true;
        }

        protected void LGM_Click(Object sender, System.EventArgs e)
        {
            if (LGMEnabled)
            {
                LGMEnabled = false;
                //write to registry? Or on exit?
                menu.MenuItems[1].Checked = false;
            }
            else
            {
                LGMEnabled = true;
                //write to registry? Or on exit?
                menu.MenuItems[1].Checked = true;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            sendPackets();
            notifyIcon1.ShowBalloonTip(100, "WarHamachi Forever", "Manual game request...", ToolTipIcon.Info);
        }

        void startTimer()
        {
            sendPackets();
            timer1.Interval = 5000;
            timer1.Start();
        }

        private void timer1_Elapsed(object sender, ElapsedEventArgs e)
        {
            sendPackets();
        }

        void sendPackets()
        {
            raw = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Udp);
            raw.SendTo(rawData, iep);
            if (LGMEnabled) { raw.SendTo(rawData2, iep); }
            raw.Close();
        }

        private void WarHamachi_Load(object sender, EventArgs e)
        {
            this.Hide();
        }
    }
}
