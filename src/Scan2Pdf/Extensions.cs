using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.Diagnostics;

namespace Scan2Pdf
{
    public static class Extensions
    {
        public static string GetCommandLine(this Process process)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
            using (ManagementObjectCollection objects = searcher.Get())
            {
                return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
            }
        }
    }
}
