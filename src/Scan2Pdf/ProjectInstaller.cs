using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Text;
using R = Scan2Pdf.Resources.Resources;

namespace Scan2Pdf
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        private const string Scan2PdfCommandName = "ConvertScansToPDF";

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);

            string installationDirectory = Context.Parameters["assemblypath"];
            string[] installationDirectoryTokens = installationDirectory.Split('\\');
            installationDirectoryTokens[installationDirectoryTokens.Length - 1] = null;
            installationDirectory = string.Join("\\", installationDirectoryTokens);
            string scan2PdfCommand = $"\"{installationDirectory}Scan2Pdf.exe\" \"%1\"";

            RegistryKey shellKey = Registry.ClassesRoot.OpenSubKey("*\\shell", RegistryKeyPermissionCheck.ReadWriteSubTree);
            RegistryKey scan2PdfConvertKey = shellKey.OpenSubKey(Scan2PdfCommandName, RegistryKeyPermissionCheck.ReadWriteSubTree);

            if (scan2PdfConvertKey == null)
                scan2PdfConvertKey = shellKey.CreateSubKey(Scan2PdfCommandName, RegistryKeyPermissionCheck.ReadWriteSubTree);

            scan2PdfConvertKey.SetValue(null, R.ConvertScansToPDF);

            RegistryKey commandKey = scan2PdfConvertKey.OpenSubKey("command", RegistryKeyPermissionCheck.ReadWriteSubTree);

            if (commandKey == null)
                commandKey = scan2PdfConvertKey.CreateSubKey("command", RegistryKeyPermissionCheck.ReadWriteSubTree);

            commandKey.SetValue(null, scan2PdfCommand);
        }

        public override void Commit(IDictionary savedState)
        {
            base.Commit(savedState);
        }

        public override void Rollback(IDictionary savedState)
        {
            base.Rollback(savedState);
        }

        public override void Uninstall(IDictionary savedState)
        {
            base.Uninstall(savedState);

            RegistryKey shellKey = Registry.ClassesRoot.OpenSubKey("*\\shell", RegistryKeyPermissionCheck.ReadWriteSubTree);
            RegistryKey scan2PdfConvertKey = shellKey.OpenSubKey(Scan2PdfCommandName);

            if (scan2PdfConvertKey != null)
                shellKey.DeleteSubKeyTree(Scan2PdfCommandName);
        }
    }
}
