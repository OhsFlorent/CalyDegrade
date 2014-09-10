using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Data.Odbc;
using System.Data.SQLite;

namespace CalyDegrade
{
    public class DBConnector
    {
        private OdbcConnection SqlConnection;
        private SQLiteConnection FileConnection;
        private ConnectionType Type;

        private enum ConnectionType : uint
        {
            ODBC        =   0,
            SQLITE      =   1,
        }

        public DBConnector(string dsn, string User, string Password)        // Constructeur pour la connection au serveur SQL
        {
            Type = ConnectionType.ODBC;
            SqlConnection = new OdbcConnection("DSN=" + dsn + ";UID=" + User + ";PWD=" + Password + ";");

            try
            {
                SqlConnection.Open();
            }
            catch(Exception e)
            {
                Logger.Out(e.Message);
            }
        }

        public DBConnector(string FileName)     //Constructeur pour la connection au fichier .db
        {
            Type = ConnectionType.SQLITE;
            FileConnection = new SQLiteConnection("Data Source=" + FileName);
            try
            {
                FileConnection.Open();
            }
            catch
            {
            }
        }

        public void Close()
        {
            switch (Type)
            {
                case ConnectionType.ODBC:
                    {
                        if (SqlConnection.State != ConnectionState.Open)
                            return;

                        SqlConnection.Close();
                    }
                    break;
                case ConnectionType.SQLITE:
                    {
                        if (FileConnection.State != ConnectionState.Open)
                            return;

                        FileConnection.Close();
                    }
                    break;
            }


        }

        public bool IsOpen()
        {
            switch (Type)
            {
                case ConnectionType.ODBC:
                    {
                        return SqlConnection.State == ConnectionState.Open;
                    }
                    break;
                case ConnectionType.SQLITE:
                    {
                        return FileConnection.State == ConnectionState.Open;
                    }
                    break;
                default:
                    {
                        return false;
                    }
            }
        }

        public string GetBaseName()
        {
            switch (Type)
            {
                case ConnectionType.ODBC:
                    {
                        return SqlConnection.Database.ToLower();
                    }
                    break;
                case ConnectionType.SQLITE:
                    {
                        return FileConnection.Database.ToLower();
                    }
                    break;
                default:
                    {
                        return "";
                    }
            }
        }

        public DataTable Query(string req)      //requête de lecture (select)
        {
            DataTable ResultTab = new DataTable();

            if (Type == ConnectionType.ODBC)
            {
                OdbcCommand query = SqlConnection.CreateCommand();
                query.CommandText = req;
                OdbcDataReader reader = query.ExecuteReader();
                ResultTab.Load(reader);
                reader.Close();
            }
            else if (Type == ConnectionType.SQLITE)
            {
                SQLiteCommand query = FileConnection.CreateCommand();
                query.CommandText = req;
                SQLiteDataReader reader = query.ExecuteReader();
                ResultTab.Load(reader);
                reader.Close();
            }

            return ResultTab;
        }

        public int ExecuteQuery(string req)        //Requete d'écriture (insert, delete, update...)
        {
            if (Type == ConnectionType.ODBC)        //On interdit la mise à jour des bases Calystene
                return 0;

            SQLiteCommand query = FileConnection.CreateCommand();
            query.CommandText = req;
            return query.ExecuteNonQuery();
        }
    }
}
