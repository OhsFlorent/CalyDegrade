using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CalyDegrade
{
    class Program
    {
        private const string LIST_FILE_NAME = "CalyDegrade.list";
        private const string DnsBainville = "SS_Bainville";
        private const string DnsFlavigny = "SS_Flavigny";
        private const string DbUser = "calystene";
        private const string DbPassword = "calohs";

        public const string BaFilesDir = "D:/Calystene/Bainville/Bilans";
        public const string FlFilesDir = "D:/Calystene/Flavigny/Bilans";
        public const string DirPansements = "Fiches_Pansements";
        public const string DirGlycemie = "Fiches_Glycemies";
        public const string DirMots = "Mot_de_suite";

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
            if (!ReadConfFile())
            {
                Logger.Out("Impossible de lire le fichier de configuration");
                return;
            }

            Console.WriteLine("Lecture du fichier de configuration terminee.");


            //Save.CleanSaveClientsTable();
            Save.PrepareSave(DbBainville, BaFilesDir);
            Save.PrepareSave(DbFlavigny, FlFilesDir);
            Save.ProcessSaveToClient();

            DbBainville.Close();
            DbFlavigny.Close();
            DbFile.Close();
        }

        private static bool ReadConfFile()
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
                    Logger.Out("Valeur incorrect dans le fichier de configuration.");
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
    }


}
