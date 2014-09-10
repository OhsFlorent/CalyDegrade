using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace CalyDegrade
{
    public class Client
    {
        private string m_adress;
        private string m_user;
        private string m_password;
        private string m_base;

        public Client(string Address, string User, string Password, string Base)
        {
            m_adress = Address.ToLower();
            m_user = User;
            m_password = Password;
            m_base = Base.ToLower();
        }

        public string GetAddress()
        {
            return m_adress;
        }

        public string GetUserName()
        {
            return m_user;
        }

        public string GetUserPassword()
        {
            return m_password;
        }

        public string GetBase()
        {
            return m_base;
        }

        public bool Connect()
        {
            string ConnectionString = GetAddress() + " /user:" + GetUserName() + " " + GetUserPassword();
            Process myProcess = new Process();
            ProcessStartInfo myProcessStartInfo = new ProcessStartInfo("net ", "use " + ConnectionString);

            myProcessStartInfo.UseShellExecute = false;
            myProcessStartInfo.RedirectStandardError = true;
            myProcess.StartInfo = myProcessStartInfo;
            myProcess.Start();

            StreamReader myStreamReader = myProcess.StandardError;
            string test = myStreamReader.ReadLine();
            if (test != null)
                return false;

            myProcess.Close();
            return true;
        }

        public void Disconnect()
        {
            Process myProcess = new Process();
            ProcessStartInfo myProcessStartInfo = new ProcessStartInfo("net ", "use /delete " + GetAddress());

            myProcessStartInfo.UseShellExecute = false;
            myProcessStartInfo.RedirectStandardError = true;
            myProcess.StartInfo = myProcessStartInfo;
            myProcess.Start();
            myProcess.Close();
        }
    }
}
