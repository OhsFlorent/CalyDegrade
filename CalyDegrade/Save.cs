﻿using System;
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
        private const string field_datemodif = "date_der_modif";
        private const string field_nomauteur = "nom_pre";
        private const string field_motdbhandle = "mot_dbhandle";

        private const string ReqCalystene = "SELECT mot_suite_suiv_sej.num_mot, patient.ipp_adm, patient.nomm, patient.prenom, mot_suite_suiv_sej.type_mot, mot_suite_suiv_sej.resume_mot, mot_suite_suiv_sej.date_der_modif, personnel1.nom_pre, mot_suite_suiv_sej.mot_dbhandle FROM patient JOIN sejour ON patient.num_dos_ad = sejour.num_dos_ad JOIN mot_suite_suiv_sej ON sejour.num = mot_suite_suiv_sej.num_sej JOIN personnel personnel1 ON personnel1.num = mot_suite_suiv_sej.num_redacteur WHERE (sejour.date_sort_reel is null OR sejour.date_cre > sejour.date_sort_reel) AND (mot_suite_suiv_sej.type_mot LIKE '%diab%' OR mot_suite_suiv_sej.type_mot LIKE '%glyc%' OR mot_suite_suiv_sej.type_mot LIKE '%pansement%' OR mot_suite_suiv_sej.resume_mot LIKE '%diab%' OR mot_suite_suiv_sej.resume_mot LIKE '%glyc%' OR mot_suite_suiv_sej.resume_mot LIKE '%pansement%')";
        private const string ReqMotCalystene = "SELECT mot_suite_suiv_sej.num_mot, patient.ipp_adm, patient.nomm, patient.prenom, mot_suite_suiv_sej.type_mot, mot_suite_suiv_sej.resume_mot, mot_suite_suiv_sej.date_der_modif, personnel1.nom_pre, mot_suite_suiv_sej.mot_dbhandle FROM patient JOIN sejour ON patient.num_dos_ad = sejour.num_dos_ad JOIN mot_suite_suiv_sej ON sejour.num = mot_suite_suiv_sej.num_sej JOIN personnel personnel1 ON personnel1.num = mot_suite_suiv_sej.num_redacteur WHERE (sejour.date_sort_reel is null OR sejour.date_cre > sejour.date_sort_reel)  AND (date_der_modif >= GETDATE()-3)";
        private const string ReqPrepareSave = "REPLACE INTO {0} VALUES ('{1}', '{2}', {3}, {4}, 1, '{5}')";
        private const string ReqPrepareMotSave = "REPLACE INTO {0} VALUES ({1},         '{2}',        '{3}',          '{4}',              '{5}',          '{6}',          '{7}',              '{8}',          '{9}',      '{10}',         {11},               1)";
        /*                                                      base        numero mot  ipp adm     nom patient     prenom patient      type mot        resume mot      date modif mot      nom redacteur   text mot    nom fichier     date modif fichier  updated*/
        private const string ReqGetTextMot = "SELECT text_value FROM mot_suite_strings WHERE string_id = {0} ORDER BY row_sequence ASC";

        public static void PrepareSave(DBConnector DataBase, string dir) //prépare la sauvegarde sur les clients : liste les fichiers à copier et à supprimer
        {
            
            Console.WriteLine("Creation de la liste des fichiers a copier et supprimer pour : " + DataBase.GetBaseName());

            DataTable ListMot;
            string BaseName = DataBase.GetBaseName();

            ListMot = DataBase.Query(ReqMotCalystene); 

            /*TRAITEMENT DES MOTS DE SUITE*/
            foreach (DataRow Row in ListMot.Rows)
            {
                
                int num_mot = int.Parse(Row[field_nummot].ToString());
                string ipp_adm = Row[field_ippcegi].ToString();
                string nom_patient = Row[field_nom].ToString();
                string prenom_patient = Row[field_prenom].ToString();
                string type_mot = Row[field_typemot].ToString();
                string resume_mot = Row[field_resumemot].ToString();
                string nom_auteur = Row[field_nomauteur].ToString();
                DateTime DateLastModif = DateTime.Parse(Row[field_datemodif].ToString());
                string sDateLastModif = DateLastModif.ToString().Split(new string[] { " " }, StringSplitOptions.None)[0];

                //Parce que Calystene c'est de la mer... c'est pas bien, il faut découper le champ mot_dbhandle pour trouver le texte complet du mot. 
                //Ce texte ce trouve dans la table mot_suite_strings.
                string FullText = "";
                int StringID;

                if (!String.IsNullOrEmpty(Row[field_motdbhandle].ToString()))
                {
                    string sStringID = Row[field_motdbhandle].ToString().Split(new string[] { ":" }, StringSplitOptions.None)[1];
                    if (!int.TryParse(sStringID, out StringID))       //Si le numéro contient des lettres, on ignore.
                        continue;

                    DataTable ListText;
                    ListText = DataBase.Query(string.Format(ReqGetTextMot, StringID));
                    foreach (DataRow Text in ListText.Rows)
                    {
                        FullText += Text[0].ToString();
                    }
                }
                else
                {
                    FullText += "--Aucun Texte--";
                }
                
                /* On doubles les quotes pour éviter les erreurs SQL */
                string NomPatient = nom_patient.Replace("'", "''");
                string PrenomPatient = prenom_patient.Replace("'", "''");
                string NomAuteur = nom_auteur.Replace("'", "''");
                string ResumeMot = resume_mot.Replace("'", "''");
                string FinalFullText = FullText.Replace("'", "''");
                string IppAdm = ipp_adm.Replace("'", "''");
                string MotFileName = MakeNewName(nom_patient, prenom_patient, ipp_adm, ".txt").Replace("'", "''");

                string Req = string.Format("SELECT file_last_modif FROM {0} WHERE num_mot = '{1}'", BaseName + "_mots", num_mot);
                DataTable Query = Program.DbFile.Query(Req);
                if (Query.Rows.Count == 0 || (long)Query.Rows[0][0] < Tools.DateToTimestamp(DateLastModif))      //On sauvegarde si le mot n'existe pas ou si il a été modifié
                {
                    string ReqSaveMot = string.Format(ReqPrepareMotSave, BaseName + "_mots", num_mot, IppAdm, NomPatient, PrenomPatient, type_mot, ResumeMot, Row[field_datemodif].ToString(), NomAuteur, FinalFullText, MotFileName, Tools.DateToTimestamp(DateTime.Now));
                    Program.DbFile.ExecuteQuery(ReqSaveMot);
                }
                else if (Query.Rows.Count > 0)      //Si le mot existe et qu'il n'a pas été modifié, on indique qu'il ne faut pas le supprimer
                {
                    string ReqUpdated = string.Format("UPDATE {0} SET updated = 2 WHERE num_mot = '{1}'", BaseName + "_mots", num_mot);
                    Program.DbFile.ExecuteQuery(ReqUpdated);
                }
            }

            /* TRAITEMENT DES FICHES EXCEL*/
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
                    else if (TypeMot.Contains("diab") || ResumeMot.Contains("diab") || TypeMot.Contains("glyc") || ResumeMot.Contains("glyc"))
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
                        string ReqSave = string.Format(ReqPrepareSave, BaseName, fName, MakeNewName(PatientName, PatientSubName, Row[field_ippcegi].ToString(), NumMot, ".xls"), (int)MotType, LastModifDate, dir);
                        Program.DbFile.ExecuteQuery(ReqSave);
                    }
                    else if (Query.Rows.Count > 0)      //Si le fichier existe et qu'il n'a pas été modifié, on indique qu'il ne faut pas le supprimer
                    {
                        string ReqUpdated = string.Format("UPDATE {0} SET updated = 2 WHERE name = '{1}'", BaseName, fName);
                        Program.DbFile.ExecuteQuery(ReqUpdated);
                    }
                }
            }
            
            /* Lorsque le champ "updated" est à 1, le fichier ou le mot a été modifié
             Lorsqu'il est à 2, le fichier ou le mot n'a pas été modifié mais existe toujours (patient en cours etc...)
             Donc, lorsqu'il est à 0, c'est que le fichier ou le mot peut être supprimé. 
             
            On supprime toutes les lignes où ce champ est à 0, puis on le remet à cette valeur pour la prochaine execution du programme. */
            Program.DbFile.ExecuteQuery("DELETE FROM " + BaseName + "_mots WHERE updated = 0");
            Program.DbFile.ExecuteQuery("UPDATE " + BaseName + "_mots SET updated = 0");

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
                string FullDestinationMotsDir = Cl.GetAddress() + @"\" + Cl.GetBase() + @"\" + Program.DirMots;
                string FullDestionationDirPansements = Cl.GetAddress() + @"\" + Cl.GetBase() + @"\" + Program.DirPansements;
                string FullDestionationDirGlycemie = Cl.GetAddress() + @"\" + Cl.GetBase() + @"\" + Program.DirGlycemie;
                string sFilesList = "";
                string DeleteReq;
                DataTable DeleteResult;

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

                if (!Directory.Exists(FullDestinationMotsDir))
                {
                    try
                    {
                        Directory.CreateDirectory(FullDestinationMotsDir);
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

                /*SAUVEGARDES DES MOTS*/
                /*Etape 1 :boucle sur les fichiers du client. Suppresion des fichiers inutiles. recreation des fichiers qui ont été modifiés sur le client*/
                foreach (string FileName in Directory.GetFiles(FullDestinationDir, "*.txt", SearchOption.AllDirectories))
                {
                    string fName = Path.GetFileName(FileName);
                    string Req = string.Format("SELECT * FROM {0} WHERE file_name = '{1}'", Cl.GetBase() + "_mots", fName.Replace("'", "''"));
                    DataTable Result = Program.DbFile.Query(Req);

                    sFilesList = sFilesList + "'" + fName.Replace("'", "''") + "',";

                    if (Result.Rows.Count == 0)     //Si le fichier n'est pas dans la base du serveur, c'est qu'il doit être supprimé du client
                        File.Delete(FileName);
                    else
                    {
                        FileInfo fInfo = new FileInfo(FileName);
                        foreach (DataRow Row in Result.Rows)        //On cherche si le fichier doit être recréé
                        {
                            if ((long)Row["file_last_modif"] > Tools.DateToTimestamp(fInfo.LastWriteTime))      //si le fichier a été modifié sur le client
                            {
                                File.Delete(FileName);
                                CreateTxtFile(Result, FileName);
                                break;          //On sait que le fichier doit être recréé, on sort donc de la boucle
                            }
                        }
                    }
                }
                /*Etape 2 : on cherche et on créé les fichiers qui ne sont pas sur le client (nouveaux fichiers)*/
                if (sFilesList == "")
                    DeleteReq = "SELECT * FROM " + Cl.GetBase() + "_mots";
                else
                    DeleteReq = "SELECT * FROM " + Cl.GetBase() + "_mots" + " WHERE file_name NOT IN (" + sFilesList.Remove(sFilesList.Length - 1) + ")";

                DeleteResult = Program.DbFile.Query(DeleteReq);

                foreach (DataRow Row in DeleteResult.Rows)
                {
                    CreateTxtFile(Row, FullDestinationMotsDir + @"\" + Row["file_name"].ToString());
                }


                /*SAUVEGARDE DES FICHES*/
                /*Etape 1 :boucle sur les fichiers du client. Suppresion des fichiers inutiles. recopie des fichiers qui ont été modifiés sur le client*/
                sFilesList = "";
                DeleteReq = "";
                foreach (string FileName in Directory.GetFiles(FullDestinationDir, "*.xls", SearchOption.AllDirectories))
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

                /*Etape 2 : on cherche et on copie les fichiers qui ne sont pas sur le client (nouveaux fichiers)*/
                if (sFilesList == "")
                    DeleteReq = "SELECT * FROM " + Cl.GetBase();
                else
                    DeleteReq = "SELECT * FROM " + Cl.GetBase() + " WHERE new_name NOT IN (" + sFilesList.Remove(sFilesList.Length -1) + ")";

                DeleteResult = Program.DbFile.Query(DeleteReq);

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

        private static string MakeNewName(string nom, string prenom, string ipp, int num, string extension)
        {
            return nom + "_" + prenom + "_" + ipp + "_" + num + extension;
        }

        private static string MakeNewName(string nom, string prenom, string ipp, string extension)
        {
            return nom + "_" + prenom + "_" + ipp + extension;
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

        /* MOT DE SUITE : Creation des fichiers txt depuis la liste provenant du resultat de la requete*/
        private static void CreateTxtFile(DataTable InfoTab, string DestFile)
        {
            StreamWriter TxtFile;
            try
            {
                TxtFile = new StreamWriter(DestFile, false);
                TxtFile.AutoFlush = true;
            }
            catch
            {
                TxtFile = null;
                Logger.Out("Erreur lors de la création du fichier : " + DestFile);
                return;
            }

            foreach (DataRow Row in InfoTab.Rows)
            {
                TxtFile.WriteLine("Numéro du mot : " + Row["num_mot"]);
                TxtFile.WriteLine("IPP administratif : " + Row["ipp_adm"]);
                TxtFile.WriteLine("Patient : " + Row["nomm"] + " " + Row["prenom"]);
                TxtFile.WriteLine("Type du mot : " + Row["type_mot"]);
                TxtFile.WriteLine("Résumé : " + Row["resume_mot"]);
                TxtFile.WriteLine("Date : " + Row["date"]);
                TxtFile.WriteLine("Auteur : " + Row["nom_pre"]);
                TxtFile.WriteLine(Row["text"]);
                TxtFile.WriteLine("==============================================================");
                TxtFile.WriteLine("");
                TxtFile.WriteLine("");
            }

            TxtFile.Close();
        }

        /* MOT DE SUITE : Creation du fichier txt depuis une unique ligne du resultat de la requete*/ 
        private static void CreateTxtFile(DataRow Row, string DestFile)
        {
            StreamWriter TxtFile;
            try
            {
                TxtFile = new StreamWriter(DestFile, true);
                TxtFile.AutoFlush = true;
            }
            catch
            {
                TxtFile = null;
                Logger.Out("Erreur lors de la création du fichier : " + DestFile);
                return;
            }

            TxtFile.WriteLine("Numéro du mot : " + Row["num_mot"]);
            TxtFile.WriteLine("IPP administratif : " + Row["ipp_adm"]);
            TxtFile.WriteLine("Patient : " + Row["nomm"] + " " + Row["prenom"]);
            TxtFile.WriteLine("Type du mot : " + Row["type_mot"]);
            TxtFile.WriteLine("Résumé : " + Row["resume_mot"]);
            TxtFile.WriteLine("Date : " + Row["date"]);
            TxtFile.WriteLine("Auteur : " + Row["nom_pre"]);
            TxtFile.WriteLine(Row["text"]);
            TxtFile.WriteLine("==============================================================");
            TxtFile.WriteLine("");
            TxtFile.WriteLine("");

            TxtFile.Close();
        }
    }
}
