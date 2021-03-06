﻿using Newtonsoft.Json;
using System.IO;

namespace InCollege
{
    public class ClientConfiguration
    {
        public static ClientConfiguration Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                if (File.Exists(CommonVariables.ConfigFileName))
                    return _instance = JsonConvert.DeserializeObject<ClientConfiguration>(File.ReadAllText(CommonVariables.ConfigFileName));

                _instance = new ClientConfiguration();
                _instance.Save();

                return _instance;
            }

            set
            {
                if (value != null)
                {
                    _instance = value;
                    _instance.Save();
                }
            }
        }
        private static ClientConfiguration _instance;

        public string HostName { get; set; } = File.Exists("host.txt") ? File.ReadAllText("host.txt") : "127.0.0.1";
        public int Port { get; set; } = 80;
        public bool AutoFillStudents { get; set; } = true;
        public bool AutoFillTicketNumbers { get; set; } = false;

        [JsonIgnore]
        public string AuthHandlerPath => $"http://{HostName}:{Port}/Auth";
        [JsonIgnore]
        public string DataHandlerPath => $"http://{HostName}:{Port}/Data";

        public bool Save()
        {
            try
            {
                File.WriteAllText(CommonVariables.ConfigFileName, JsonConvert.SerializeObject(this, Formatting.Indented));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
