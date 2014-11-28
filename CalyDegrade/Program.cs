using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Mail;
using System.Data;

namespace CalyDegrade
{
    class Program
    {
        private const string LIST_FILE_NAME = "CalyDegrade.list";
        private const string CONF_FILE_NAME = "CalyDegrade.conf";
        private const string DnsBainville = "SS_Bainville";
        private const string DnsFlavigny = "SS_Flavigny";
        private const string DbUser = "calystene";
        private const string DbPassword = "calohs";

        public const string BaFilesDir = "D:/Calystene/Bainville/Bilans";
        public const string FlFilesDir = "D:/Calystene/Flavigny/Bilans";
        public const string DirPansements = "Fiches_Pansements";
        public const string DirGlycemie = "Fiches_Glycemies";
        public const string DirMots = "Mot_de_suite";

        private static Hashtable m_ServerConfig = new Hashtable();
        public static bool SaveError = false;

        private static DBConnector DbBainville;     //représente la connexion à la base de donnée de Bainville
        private static DBConnector DbFlavigny;      //représente la connexion à la base de donnée de Flavigny
        public static DBConnector DbFile;           //représente la connexion au fichier .db

        private static List<Client> ClientsList;    //Contient la liste des PC clients ainsi que leurs infos (IP, nom d'utilisateur...)

        static void Main(string[] args)
        {
            Console.WriteLine("Demarrage de la sauvegarde...");

            Logger.Initialize(); //On initialise la classe permettant de logger les erreurs dans un fichiers textes

            ClientsList = new List<Client>();

            if (!Directory.Exists(BaFilesDir))
            {
                Logger.Out("Repertoire introuvable : " + BaFilesDir);
                return;
            }
            if (!Directory.Exists(FlFilesDir))
            {
                Logger.Out("Repertoire introuvable : " + FlFilesDir);
                return;
            }


            //On initialise les connections aux bases de données (Y compris le fichier .db)
            DbBainville = new DBConnector(DnsBainville, DbUser, DbPassword);
            DbFlavigny = new DBConnector(DnsFlavigny, DbUser, DbPassword);
            DbFile = new DBConnector("CalyDegrade.db");

            //On vérifie que les connexions aux DB sont bien ouvertes
            if (!DbBainville.IsOpen() || !DbFlavigny.IsOpen())
            {
                Logger.Out("Connection impossible a la BD.");
                return;
            }

            Console.WriteLine("Connections aux BDD reussies.");

            if (!DbFile.IsOpen())
            {
                Logger.Out("Connection impossible au fichier .db");
                return;
            }

            Console.WriteLine("Connection au fichier .db reussie...");

            //On lit le fichier de conf (la liste des PC)
            if (!ReadListFile())
            {
                Logger.Out("Impossible de lire le fichier liste");
                return;
            }

            if (!Conf.Init(CONF_FILE_NAME))
            {
                return;
            }
            LoadConfig();

            Console.WriteLine("Lecture du fichier liste terminee.");

            //Save.CleanSaveClientsTable();
            Save.PrepareSave(DbBainville, BaFilesDir);
            Save.PrepareSave(DbFlavigny, FlFilesDir);
            Save.ProcessSaveToClient();

            if (SaveError)
            {
                DbFile.ExecuteQuery("UPDATE mail SET send = 1");
            }

            //Envoi d'un mail trois fois par jour
            DataTable Result = DbFile.Query("SELECT send FROM mail");
            if (Result.Rows.Count == 0)
                Logger.Out("Fichier .DB corrompu.", true);
            else
            {
                int send;
                int.TryParse(Result.Rows[0]["send"].ToString(), out send);
                if (send == 1)
                {
                    if ((DateTime.Now.Hour >= 8 && DateTime.Now.Hour < 9 && DateTime.Now.Minute <= 14) || (DateTime.Now.Hour >= 13 && DateTime.Now.Hour < 14 && DateTime.Now.Minute <= 14) || (DateTime.Now.Hour >= 16 && DateTime.Now.Hour < 17 && DateTime.Now.Minute <= 14))
                    {
                        SendMail();
                        DbFile.ExecuteQuery("UPDATE mail SET send = 0");
                    }
                }
            }

            DbBainville.Close();
            DbFlavigny.Close();
            DbFile.Close();
        }

        private static bool ReadListFile()
        {
            string[] Lines, ClientsInfos;

            try
            {
                Lines = File.ReadAllLines(LIST_FILE_NAME);      //On lit le fichier

            }
            catch
            {
                return false;
            }

            foreach (string Line in Lines)
            {
                if (Line.StartsWith("#") || String.IsNullOrWhiteSpace(Line))    //On ignore les commentaires et les lignes vides
                    continue;

                //On supprime de la ligne les espaces blancs et les tab en trop
                string CleanLine = Line.Trim();
                CleanLine = CleanLine.Replace('\t', ' ');
                while (CleanLine.Contains("  "))
                {
                    CleanLine = CleanLine.Replace("  ", " ");
                }

                //On découpe la ligne et on récupère chaque information
                ClientsInfos = CleanLine.Split(new Char[]{' '}, 4);

                if (ClientsInfos.Length < 4)
                {
                    Logger.Out("Valeur incorrect dans le fichier liste.");
                    continue;
                }

                string Address = ClientsInfos[0];
                string Base = ClientsInfos[1];
                string User = ClientsInfos[2];
                string Password = ClientsInfos[3];

                ClientsList.Add(new Client(Address, User, Password, Base));     //On ajoute le PC et ses infos dans la liste
            }

            return true;
        }

        public static List<Client> GetClientsList()
        {
            return ClientsList;
        }

        private static void LoadConfig()
        {
            Console.WriteLine("Lecture du fichier de configuration.");
            Conf.Load();
            m_ServerConfig.Add("Mail.Enable", Conf.GetBoolValue("Mail.Enable", false));
            m_ServerConfig.Add("Mail.SMTPServer", Conf.GetStringIpAddressValue("Mail.SMTPServer", "127.0.0.1"));
            m_ServerConfig.Add("Mail.SenderMailAddress", Conf.GetStringEmailValue("Mail.SenderMailAddress", "Calystene.Degrade@ohs.asso.fr"));
            m_ServerConfig.Add("Mail.AddressList", Conf.GetStringValue("Mail.AddressList", ""));
            Conf.EndLoading();
            Console.WriteLine("Lecture du fichier de configuration terminee.");
        }

        public static bool GetBoolConfig(string conf)
        {
            bool val = true;

            if (m_ServerConfig[conf].ToString() == "True")
                val = true;
            else if (m_ServerConfig[conf].ToString() == "False")
                val = false;

            return val;
        }

        public static int GetIntConfig(string conf)
        {
            return int.Parse(m_ServerConfig[conf].ToString());
        }

        public static string GetStringConfig(string conf)
        {
            return m_ServerConfig[conf].ToString();
        }

        private static void SendMail()
        {
            if (!GetBoolConfig("Mail.Enable"))
                return;

            string AddressList = GetStringConfig("Mail.AddressList");
            string SenderAddress = GetStringConfig("Mail.SenderMailAddress");
            string SmtpIpAddress = GetStringConfig("Mail.SMTPServer");

            if (SmtpIpAddress == "127.0.0.1" || String.IsNullOrWhiteSpace(SmtpIpAddress))
            {
                Logger.Out("Fichier de CONF : Erreur dans l'adresse du serveur.");
                return;
            }
            if (String.IsNullOrWhiteSpace(SenderAddress))
            {
                Logger.Out("Fichier de CONF : Erreur dans l'adresse mail d'envoi.");
                return;
            }

            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(SenderAddress);

            foreach (string address in AddressList.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Tools.IsValidEmail(address))
                {
                    Logger.Out("Fichier de CONF : la liste des destinataires contient des erreurs.");
                    continue;
                }

                mail.To.Add(address);
            }

            if (mail.To.Count == 0)
            {
                Logger.Out("Envoi de mail actif. Mais la liste des destinataires est vide.");
                return;
            }
            mail.Subject = "Mode degrade Calystene : Notification d'erreurs";
            mail.Body = "Des erreurs sont survenues lors d'une sauvegarde recente des fichiers. Consultez le fichier 'error.log' pour plus d'informations. \n \n" +  @"Repertoire : \\v-caly-01\Calystene\Degrade";

            SmtpClient MailClient = new SmtpClient(SmtpIpAddress);

            try
            {
                MailClient.Send(mail);
            }
            catch
            {
                Logger.Out("Erreur inconnue lors de l'envoi de mail.");
            }
        }
    }


}
