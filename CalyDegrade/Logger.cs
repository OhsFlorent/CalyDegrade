using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CalyDegrade
{
    public static class Logger
    {
        private static StreamWriter LogFile;

        public static void Initialize()
        {
            try
            {
                LogFile = new StreamWriter("error.log", true);
                LogFile.AutoFlush = true;
            }
            catch
            {
                LogFile = null;
            }
        }

        public static void Out(string mess, bool NotInMail = false, params object[] arguments)
        {
            if (LogFile == null)
                return;

            string message = string.Format(mess, arguments);
            string Time = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss > ");
            LogFile.WriteLine(Time + message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Time + message);
            Console.ResetColor();

            if (!NotInMail)
                Program.SaveError = true;
        }
    }
}
