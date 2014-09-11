using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Net;

namespace CalyDegrade
{
    public static class Save
    {
        public enum FicheType : uint
        {
            PANSEMENT = 1,
            GLYCEMIE = 2,
        }


        //Structure de la base de donné Calystene. Nom des champs
        private const string field_nummot = "num_mot";
        private const string field_ippcegi = "ipp_adm";
        private const string field_nom = "nomm";
        private const string field_prenom = "prenom";
        private const string field_typemot = "type_mot";
        private const string field_resumemot = "resume_mot";

        private const string ReqCalystene = "SELECT mot_suite_suiv_sej.num_mot, patient.ipp_adm, patient.nomm, patient.prenom, mot_suite_suiv_sej.type_mot, mot_suite_suiv_sej.resume_mot FROM patient JOIN sejour ON patient.num_dos_ad = sejour.num_dos_ad JOIN mot_suite_suiv_sej ON sejour.num = mot_suite_suiv_sej.num_sej WHERE (sejour.date_sort_reel is null OR sejour.date_cre > sejour.date_sort_reel) AND (mot_suite_suiv_sej.type_mot LIKE '%diabete%' OR mot_suite_suiv_sej.type_mot LIKE '%glycemie%' OR mot_suite_suiv_sej.type_mot LIKE '%pansement%' OR mot_suite_suiv_sej.resume_mot LIKE '%diabete%' OR mot_suite_suiv_sej.resume_mot LIKE '%glycemie%' OR mot_suite_suiv_sej.resume_mot LIKE '%pansement%')";
        private const string ReqPrepareSave = "REPLACE INTO {0} VALUES ('{1}', '{2}', {3}, {4}, 1, '{5}')";

        public static void PrepareSave(DBConnector DataBase, string dir) //prépare la sauvegarde sur les clients : liste les fichiers à copier et à supprimer
        {
            Console.WriteLine("Creation de la liste des fichiers a copier et supprimer pour : " + DataBase.GetBaseName());

            DataTable ListMot;
            string BaseName = DataBase.GetBaseName();

            ListMot = DataBase.Query(ReqCalystene); 


            foreach (string FileName in Directory.GetFiles(dir))    //Boucle sur les fichiers sur le serveur Calystene
            {
                FileInfo fInfo = new FileInfo(FileName);
                if ((fInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)    //On ignore les fichiers cachés
                    continue;

                string fName = Path.GetFileName(FileName);
                if (fName.ToLower().Contains("copie") || !fName.Contains("-IN"))      //On ignore les fichiers non liés aux mots de suite
                    continue;

                string sNumFile = fName.Split(new string[] {"IN"}, StringSplitOptions.None)[1].Split(new string[] {".xls"}, StringSplitOptions.None)[0];
                int NumFile;

                if (!int.TryParse(sNumFile, out NumFile))       //Si la le numéro contient des lettres, on ignore
                    continue;

                foreach (DataRow Row in ListMot.Rows)  //Boucle sur les mots de suite dans Calystene (Resultat requête SQL)
                {
                    string sNumMot = Row[field_nummot].ToString();
                    int NumMot;
                    if (!int.TryParse(sNumMot, out NumMot))
                        continue;

                    if (NumMot != NumFile)      //On sauvegarde le fichier si son numéro est dans le resultat de la requête
                        continue;

                    //sauvegarde du fichier
                    //On regarde si il y a les mots "pansement", "diabete" ou "glycemie" dans le type de mot ou dans le resumé du mot. On converti tout en miniscule et on supprime les accents pour minimiser les erreurs.
                    FicheType MotType;
                    string TypeMot = Tools.RemoveDiacritics(Row[field_typemot].ToString().ToLower());
                    string ResumeMot = Tools.RemoveDiacritics(Row[field_resumemot].ToString().ToLower());
                    if (TypeMot.Contains("pansement") || ResumeMot.Contains("pansement"))
                        MotType = FicheType.PANSEMENT;
                    else if (TypeMot.Contains("diabete") || ResumeMot.Contains("diabete") || TypeMot.Contains("glycemie") || ResumeMot.Contains("glycemie"))
                        MotType = FicheType.GLYCEMIE;
                    else
                        continue;

                    long LastModifDate = Tools.DateToTimestamp(fInfo.LastWriteTime);
                    string PatientName = Row[field_nom].ToString().Replace("'", "''");      // On doubles les quotes pour éviter les erreurs SQL
                    string PatientSubName = Row[field_prenom].ToString().Replace("'", "''");

                    string Req = string.Format("SELECT last_modif FROM {0} WHERE name = '{1}'", BaseName, fName);
                    DataTable Query = Program.DbFile.Query(Req);
                    if (Query.Rows.Count == 0 || (long)Query.Rows[0][0] < Tools.DateToTimestamp(fInfo.LastWriteTime))      //On sauvegarde si le fichier n'existe pas ou si il a été modifié
                    {
                        string ReqSave = string.Format(ReqPrepareSave, BaseName, fName, MakeNewName(PatientName, PatientSubName, Row[field_ippcegi].ToString(), NumMot), (int)MotType, LastModifDate, dir);
                        Program.DbFile.ExecuteQuery(ReqSave);
                    }
                    else if (Query.Rows.Count > 0)      //Si le fichier existe et qu'il n'a pas été modifié, on indique qu'il ne faut pas le supprimer
                    {
                        string ReqUpdated = string.Format("UPDATE {0} SET updated = 2 WHERE name = '{1}'", BaseName, fName);
                        Program.DbFile.ExecuteQuery(ReqUpdated);
                    }
                }
            }

            Program.DbFile.ExecuteQuery("DELETE FROM " + BaseName + " WHERE updated = 0");
            Program.DbFile.ExecuteQuery("UPDATE " + BaseName + " SET updated = 0");
        }

        public static void ProcessSaveToClient()
        {
            Console.WriteLine("Mise a jour des postes clients : copie et suppression des fichiers...");

            foreach (Client Cl in Program.GetClientsList())
            {
                if (!Cl.Connect())
                {
                    Logger.Out("Impossible de trouver l'emplacement : " + Cl.GetAddress());
                    continue;
                }

                string FullDestinationDir = Cl.GetAddress() + @"\" + Cl.GetBase();
                string FullDestionationDirPansements = Cl.GetAddress() + @"\" + Cl.GetBase() + @"\" + Program.DirPansements;
                string FullDestionationDirGlycemie = Cl.GetAddress() + @"\" + Cl.GetBase() + @"\" + Program.DirGlycemie;
                string sFilesList = "";

                /*Creation des dossiers sur le postes clients*/
                if (!Directory.Exists(FullDestinationDir))
                {
                    try
                    {
                        Directory.CreateDirectory(FullDestinationDir);
                    }
                    catch
                    {
                        Logger.Out("Erreur lors de la creation de dossier sur : " + Cl.GetAddress());
                        continue;
                    }
                }

                if (!Directory.Exists(FullDestionationDirPansements))
                {
                    try
                    {
                        Directory.CreateDirectory(FullDestionationDirPansements);
                    }
                    catch
                    {
                        Logger.Out("Erreur lors de la creation de dossier sur : " + Cl.GetAddress());
                        continue;
                    }
                }

                if (!Directory.Exists(FullDestionationDirGlycemie))
                {
                    try
                    {
                        Directory.CreateDirectory(FullDestionationDirGlycemie);
                    }
                    catch
                    {
                        Logger.Out("Erreur lors de la creation de dossier sur : " + Cl.GetAddress());
                        continue;
                    }
                }
                /*Fin de la creation des dossiers*/

                foreach (string FileName in Directory.GetFiles(FullDestinationDir, "*.*", SearchOption.AllDirectories))
                {
                    string FinaleDir = "";
                    int fType;
                    string fName = Path.GetFileName(FileName);
                    string Req = string.Format("SELECT * FROM {0} WHERE new_name = '{1}'", Cl.GetBase(), fName.Replace("'", "''"));
                    DataTable Result = Program.DbFile.Query(Req);

                    sFilesList = sFilesList + "'" + fName.Replace("'", "''") + "',";

                    if (Result.Rows.Count == 0)     //Si le fichier n'est pas dans la base du serveur, c'est qu'il doit être supprimé du client
                        File.Delete(FileName);
                    else
                    {
                        int.TryParse(Result.Rows[0]["type"].ToString(), out fType);
                        if (fType == (int)FicheType.PANSEMENT)
                            FinaleDir = FullDestionationDirPansements;
                        else if (fType == (int)FicheType.GLYCEMIE)
                            FinaleDir = FullDestionationDirGlycemie;
                        else
                            continue;
                        FileInfo fInfo = new FileInfo(FileName);
                        if ((long)Result.Rows[0]["last_modif"] > Tools.DateToTimestamp(fInfo.LastWriteTime))
                            File.Copy(Result.Rows[0]["directory"].ToString() + @"\" + Result.Rows[0]["name"].ToString(), FinaleDir + @"\" + Result.Rows[0]["new_name"].ToString(), true);
                    }
                }

                string DeleteReq;
                if (sFilesList == "")
                    DeleteReq = "SELECT * FROM " + Cl.GetBase();
                else
                    DeleteReq = "SELECT * FROM " + Cl.GetBase() + " WHERE new_name NOT IN (" + sFilesList.Remove(sFilesList.Length -1) + ")";

                DataTable DeleteResult = Program.DbFile.Query(DeleteReq);

                foreach (DataRow Row in DeleteResult.Rows)
                {
                    string FinaleDir = "";
                    int fType;
                    int.TryParse(Row["type"].ToString(), out fType);
                    if (fType == (int)FicheType.PANSEMENT)
                        FinaleDir = FullDestionationDirPansements;
                    else if (fType == (int)FicheType.GLYCEMIE)
                        FinaleDir = FullDestionationDirGlycemie;
                    else
                        continue;

                    File.Copy(Row["directory"].ToString() + @"/" + Row["name"].ToString(), FinaleDir + @"\" + Row["new_name"].ToString(), true);
                }
                Cl.Disconnect();
            }
        }

        private static string MakeNewName(string nom, string prenom, string ipp, int num)
        {
            return nom + "_" + prenom + "_" + ipp + "_" + num + ".xls";
        }

        public static void CleanSaveClientsTable()
        {
            Console.WriteLine("Nettoyage des sauvegardes echouees...");
            string sClientsList = "";
            foreach (Client Cl in Program.GetClientsList())
            {
                sClientsList = sClientsList + "'" + Cl.GetAddress() + "',";
            }

            string Req = "DELETE FROM save_list WHERE destination NOT IN (" + sClientsList.Remove(sClientsList.Length -1) +")";
            Program.DbFile.ExecuteQuery(Req);
        }
    }
}
