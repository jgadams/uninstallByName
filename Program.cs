/*
Copyright 2015 Justin Gregory Adams.

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, version 3 only.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace uninstallByName
{
    static class exitCodes
    {
        public static int success = 0;
        public static int openKeyFail = 3;
        public static int enumerateSubkeysFail = 5;
        public static int getUninstallKeyFail = 7;
        public static int usage = 11;
        public static int parseTimeout = 13;
    }

    class Program
    {
        static void diagMsg(String msg)
        {
            Console.Error.WriteLine(String.Format("uninstallByName.exe: {0}: {1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), msg));
        }

        static void snafu(int code, String msg)
        {
            diagMsg(msg);
            Environment.Exit(code);
        }

        // Looks for the value "UninstallString" and returns the first match.
        static String getUninstallStringFromKey(String match, String uninstallKeyPath)
        {
            String uninstallString;
            String displayName;
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(uninstallKeyPath, false);

            if(rk == null)
                snafu(exitCodes.openKeyFail, String.Format("Could not open key \"{0}\".", uninstallKeyPath));

            foreach(String key in rk.GetSubKeyNames())
            {
                if(key == null)
                    snafu(exitCodes.enumerateSubkeysFail, String.Format("Could not enumerate subkeys of {0}", uninstallKeyPath));

                String fullPath = String.Format("HKEY_LOCAL_MACHINE\\{0}\\{1}", uninstallKeyPath, key);

                try
                {
                    uninstallString = Microsoft.Win32.Registry.GetValue(fullPath, "UninstallString", "").ToString();
                    displayName = Microsoft.Win32.Registry.GetValue(fullPath, "DisplayName", "").ToString();
                    if(displayName.Contains(match))
                        return uninstallString;
                }
                catch(System.Exception e)
                {
                    diagMsg(e.Message);
                    diagMsg(e.StackTrace);
                    snafu(exitCodes.getUninstallKeyFail, String.Format("Could not get value of key \"{0}\\UninstallString\".", fullPath));
                }
            }

            return "";
        }

        static String getUninstallString(String match)
        {
            String ret;

            // Try the Vista+ 32 bit tree first, then the normal tree.
            ret = getUninstallStringFromKey(match, "SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            if(ret.Length > 0)
                return ret;
            ret = getUninstallStringFromKey(match, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            return ret;
        }

        static void usage()
        {
            snafu(exitCodes.usage, "Usage: uninstallByName.exe  [--timeout-minutes T]  PROGRAM_STRING");
        }

        static int Main(string[] args)
        {
            System.Diagnostics.Process p;
            String unStr;
            String unFile;
            String unArgs;
            String programString= "";
            int splitn= -1;  // where the program string spits between command and args
            int i;
            int timeoutMilliseconds= -1;

            if((args.Length != 1) && (args.Length != 3))
                usage();

            for (i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--timeout-minutes"))
                {
                    if (i > args.Length - 2)
                        usage();

                    i++;
                    try
                    {
                        timeoutMilliseconds = Int32.Parse(args[i]) * 60000;
                    }
                    catch (Exception e)
                    {
                        diagMsg(e.Message);
                        diagMsg(e.StackTrace);
                        snafu(exitCodes.parseTimeout, String.Format("Could not parse timeout argument \"{0}\".", args[i]));
                    }
                }

                else
                    programString = args[i];
            }

            if (programString.Length < 1)
                usage();

            diagMsg(String.Format("Uninstalling {0}.", programString));
            unStr = getUninstallString(programString);

            // We must split the exe from its arguments, because Process.Start() requires it.
            if (unStr.Contains(".exe "))
                splitn = unStr.IndexOf(".exe ") + 4;
            else if (unStr.Contains(".exe\" "))
                splitn = unStr.IndexOf(".exe\" ") + 5;
            else if(unStr.EndsWith(".exe"))
                splitn = unStr.IndexOf(".exe") + 4;
            else if(unStr.EndsWith(".exe\""))
                splitn= unStr.IndexOf(".exe\"") + 5;
            if(splitn > 0)
            {
                unFile = "";
                unArgs = "";

                if (splitn < unStr.Length)
                {
                    unFile = unStr.Substring(0, splitn);
                    unArgs = unStr.Substring(splitn + 1);
                    p = System.Diagnostics.Process.Start(unFile, unArgs);
                }
                else
                {
                    unFile = unStr;
                    p = System.Diagnostics.Process.Start(unFile);
                }

                diagMsg(String.Format("<{0}>  <{1}>", unFile, unArgs));
                diagMsg(String.Format("Spawned process id {0} for \"{1}\".", p.Id, programString));
                if (timeoutMilliseconds > 0)
                {
                    diagMsg(String.Format("Timeout: {0} seconds.", timeoutMilliseconds / 1000));
                    p.WaitForExit(timeoutMilliseconds);
                }
                else
                    p.WaitForExit();
                diagMsg(String.Format("Finished uninstall process for \"{0}\".", programString));
                return exitCodes.success;
            }

            else // Failed to parse uninstall entry from the registry.
            {
                diagMsg(String.Format("Could not parse uninstall string \"{0}\".", unStr));
                return exitCodes.getUninstallKeyFail;
            }
        }
    }
}
