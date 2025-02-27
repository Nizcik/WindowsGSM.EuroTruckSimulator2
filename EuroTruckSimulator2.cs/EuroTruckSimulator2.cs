﻿using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WindowsGSM.Plugins
{
    public class EuroTruckSimulator2 : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.EuroTruckSimulator2", // WindowsGSM.XXXX
            author = "ohmcodes",
            description = "WindowsGSM plugin for supporting Euro Truck Simulator 2 Dedicated Server",
            version = "1.2.1",
            url = "https://github.com/ohmcodes/WindowsGSM.EuroTruckSimulator", // Github repository link (Best practice)
            color = "#FFD700" // Color Hex
        };

        // - Standard Constructor and properties
        public EuroTruckSimulator2(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => false;
        public override string AppId => "1948160"; /* taken via https://steamdb.info/app/1948160/info/ */

        // - Game server Fixed variables
        public override string StartPath => "bin\\win_x64\\eurotrucks2_server.exe"; // Game server start path
        public string FullName = "Euro Truck Simulator 2 Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 0; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string ServerName = "wgsm_eurotrucks2_dedicated";
        public string Defaultmap = ""; // Original (MapName)
        public string Maxplayers = "8"; // WGSM reads this as string but originally it is number or int (MaxPlayers)
        public string Port = "27015"; // WGSM reads this as string but originally it is number or int
        public string QueryPort = "27016"; // WGSM reads this as string but originally it is number or int (SteamQueryPort)

        public string Additional = string.Empty;

        // - Create a default cfg for the game server after installation
        public void CreateServerCFG()
        {
            modifyConfigFile();
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }
            
            string param = " -server -nosingle";
            //param += $" -server_cfg {Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID),"server_config.sii")}";

            //param += $" -homedir {ServerPath.GetServersServerFiles(_serverData.ServerID)}";
            param += $" {_serverData.ServerParam}";

            await Task.Delay(1000);
            modifyConfigFile();

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (AllowsEmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }

                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
            
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                ServerConsole.SetMainWindow(p.MainWindowHandle);
                ServerConsole.SendWaitToMainWindow("^c");
            });
            await Task.Delay(20000);
        }

        // - Update server function
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, "PackageInfo.bin");
            Error = $"Invalid Path! Fail to find {Path.GetFileName(exePath)}";
            return File.Exists(exePath);
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }

        public async void modifyConfigFile()
        {
            // Get the path to the My Documents folder for the current user
            string documentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // Specify the path to your server_config.sii file
            string ets = "Euro Truck Simulator 2";
            string configFile = "server_config.sii";

            string filePath = Path.Combine(documentsFolderPath, ets, configFile);

            string serverPath = ServerPath.GetServersServerFiles(_serverData.ServerID);
            string sii = "server_packages.sii";
            string dat = "server_packages.dat";
            string savePath = Path.Combine(serverPath, "save");

            // Specify the new values
            string ServerName = _serverData.ServerName;
            string Port = _serverData.ServerPort;
            string QueryPort = _serverData.ServerQueryPort;
            string GLST = _serverData.ServerGSLT;

            try
            {
                // Read all lines from the file
                string[] lines = File.ReadAllLines(filePath);

                // Use a regular expression to find and replace the values
                for (int i = 0; i < lines.Length; i++)
                {
                    // Modify lobby_name
                    if (lines[i].Contains("lobby_name"))
                    {
                        lines[i] = Regex.Replace(lines[i], @"lobby_name: "".*""", $"lobby_name: \"{ServerName}\"");
                    }
 
                    // Modify connection_dedicated_port
                    if (lines[i].Contains("connection_dedicated_port"))
                    {
                        lines[i] = Regex.Replace(lines[i], @"\d+", Port);
                    }

                    // Modify query_dedicated_port
                    if (lines[i].Contains("query_dedicated_port"))
                    {
                        lines[i] = Regex.Replace(lines[i], @"\d+", QueryPort);
                    }

                    // Modify GSLT
                    if (lines[i].Contains("server_logon_token"))
                    {
                        lines[i] = Regex.Replace(lines[i], @"server_logon_token: "".*""|server_logon_token: .*|server_logon_token: """"", $"server_logon_token: {GLST}");
                    }
                }

                // Write the modified lines back to the file
                File.WriteAllLines(filePath, lines);

                Notice = "File modified successfully!";
            }
            catch (Exception ex)
            {
                Error = $"Error: {ex.Message}";
            }

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            // Automatically copy server_packages.sii and server_packages.dat
            if (File.Exists(Path.Combine(documentsFolderPath, ets,sii)) && File.Exists(Path.Combine(documentsFolderPath, ets, dat)))
            {
                try
                {
                    File.Copy(Path.Combine(documentsFolderPath, ets, sii), Path.Combine(savePath, sii), true);
                    File.Copy(Path.Combine(documentsFolderPath, ets, dat), Path.Combine(savePath, dat), true);
                    Notice = $"{sii} and {dat} File has been copied to {savePath}";
                }
                catch (Exception ex)
                {
                    Error = $"Error: {ex.Message}";
                }
            }

            await Task.Delay(1000);
        }
    }
}
