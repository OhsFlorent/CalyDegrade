using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CalyDegrade
{
    public static class Conf
    {
        private static Hashtable Configs = new Hashtable();
        private static string FileName;

        public static bool Init(string file)
        {
            if (!File.Exists(file))
            {
                Logger.Out("Le fichier de configuration est introuvable !");
                return false;
            }

            FileName = file;
            return true;
        }

        public static void Load()
        {
            string[] file = File.ReadAllLines(FileName);
            char[] del = new char[1] { '=' };

            foreach (string str in file)
            {
                if (str.StartsWith("#") || str == "")
                    continue;


                string[] split = str.Split(del, 2);
                string str1 = split[0].Replace(" ", string.Empty);
                string str2;
                if (split[1].StartsWith(" "))
                    str2 = split[1].TrimStart(' ');

                else str2 = split[1];

                Configs.Add(str1, str2);
            }
        }

        public static void EndLoading()
        {
            Configs.Clear();
        }

        public static int GetIntValue(string name, int def, int miniValue, int maxValue)
        {
            int val;
            try
            {
                val = int.Parse(Configs[name].ToString());
                if (val < miniValue || val > maxValue)
                {
                    Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                    val = def;
                }
            }
            catch
            {
                Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                val = def;
            }
            return val;
        }

        public static string GetStringValue(string name, string def)
        {
            string val;
            if (Configs.ContainsKey(name))
            {
                val = Configs[name].ToString();
            }
            else
            {
                Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                val = def;
            }

            return val;
        }

        public static string GetStringIpAddressValue(string name, string def)
        {
            string val;
            if (Configs.ContainsKey(name))
            {
                val = Configs[name].ToString();
                if (!Tools.IsValidIpAddress(val))
                {
                    Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                    val = def;
                }
            }
            else
            {
                Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                val = def;
            }

            return val;
        }

        public static string GetStringEmailValue(string name, string def)
        {
            string val;
            if (Configs.ContainsKey(name))
            {
                val = Configs[name].ToString();
                if (!Tools.IsValidEmail(val))
                {
                    Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                    val = def;
                }
            }
            else
            {
                Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                val = def;
            }

            return val;
        }

        public static bool GetBoolValue(string name, bool def)
        {
            bool val;
            if (Configs.ContainsKey(name))
            {
                if (Configs[name].ToString() == "0")
                    val = false;
                else if (Configs[name].ToString() == "1")
                    val = true;
                else
                {
                    Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                    val = def;
                }
            }
            else
            {
                Logger.Out("Configuration '" + name + "' est introuvable ou invalide dans le fichier de conf, utilisation de la valeur par defaut : " + def.ToString());
                val = def;
            }

            return val;
        }
    }
}
