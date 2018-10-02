using System;

namespace MatlabApp
{
    public class Logger
    {
        static private int _level = 3;

        static internal void Error(string message, Exception ex)
        {
            Logger.Write(0, "**** EXCEPTION : " + message +
                "\n\t" + ex.Message +
                "\nSOURCE:" + ex.Source + 
                "\nSTACK:\n" + ex.StackTrace);
        }
        static internal void Write(int level, string message)
        {
            if (level > Logger._level)
            {
                return;
            }
            Console.Out.WriteAsync(DateTime.Now.ToString("yyyyMMddTHHmmsszzz"));
            Console.Out.WriteAsync('\t');
            for (var i = 2; i < level; i +=3)
            {
                Console.Out.WriteAsync("... ");
            }
            Console.Out.WriteLineAsync(message);
        }

        static internal int Level
        {
            get
            {
                return Logger._level;
            }
            set
            {
                if (value < 0)
                {
                    Logger._level = 0;
                }
                else if(value > 9)
                {
                    Logger._level = 9;
                }
                else
                {
                    Logger._level = value;
                }
                Logger.Write(0, "Set logging level : " + Logger._level);
            }
        }
    }
}
