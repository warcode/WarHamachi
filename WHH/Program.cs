/* ¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤
 * WarHamachi by lag
 * ¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Net;

//Auto Update
using System.Diagnostics;
using System.IO;

namespace WHH
{

    static class Program
    {
        
        static string version = "2.4"; //Auto Update
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0) 
            { 
                while(true)
                {
                    if(killOldWH(args[0]) == false)
                    {
                        MessageBox.Show("WarHamachi updated to the latest version!", "WarHamachi" + version);
                        File.Delete(args[0]);
                        break;
                    }
                }
            }
            if (checkForUpdates(version) == false)
            { //Auto Update
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new WarHamachi());
            }
        }

        //Auto Update
        static bool checkForUpdates(string _version)
        {
            bool updated = false;
            using (WebClient client = new WebClient())
            {
                //manipulate request headers (optional)
                client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                Uri webp = new Uri("http://www.warcode.net/code/whv.htm");
                //execute request and read response as string to console
                using (StreamReader reader = new StreamReader(client.OpenRead(webp)))
                {
                    string s = reader.ReadToEnd();
                    if (s.CompareTo(version) == 1) 
                    {
                        updated = startUpdate(s);
                    }
                }
            }
            return updated;

        }

        static bool startUpdate(string v)
        {
            string URL = "http://www.warcode.net/code/WH2.0/WarHamachi"+v+".exe";
            string DestinationPath = "WarHamachi" + v + ".exe";
            System.Net.WebClient Client = new WebClient();
            try
            {
                Client.DownloadFile(URL, DestinationPath);
            }
            catch (Exception)
            {
                MessageBox.Show("Could not update WarHamachi to the latest version!", "WarHamachi" + version);
                return false;
            }
            Process newwh = new Process();
            newwh.StartInfo.FileName = "WarHamachi" + v + ".exe";
            newwh.StartInfo.Arguments = "WarHamachi" + version + ".exe";
            newwh.Start();
            return true;
        }

        static bool killOldWH(string name)
        {
	      //here we're going to get a list of all running processes on
	      //the computer
	      foreach (Process clsProcess in Process.GetProcesses()) {
		            //now we're going to see if any of the running processes
		            //match the currently running processes. Be sure to not
		            //add the .exe to the name you provide, i.e: NOTEPAD,
		            //not NOTEPAD.EXE or false is always returned even if
		            //notepad is running.
		            //Remember, if you have the process running more than once, 
		            //say IE open 4 times the loop thr way it is now will close all 4,
		            //if you want it to just close the first one it finds
		            //then add a return; after the Kill
		            if (clsProcess.ProcessName.Contains(name))
		            {
			            //if the process is found to be running then we
			            //return a true
			            return true;
		            }
	            }
	            //otherwise we return a false
	            return false;
        }
    }
}

//TODO
/*
    Before Application.Run, check for updates.
    If update, download update, run new warhamachi with current executable name as console arg
    If new warhamachi is ran with a console arg, wait for that process to disappear, delete old executable.
    Show update message, victory.
*/