using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoBackupFolder
{
    class Program
    {
        public static BackupSettings backupSettings { get; set; }
        public Program()
        {

        }
        static void Main(string[] args)
        {
            var zipHelper = new ZipHelper();
            if (File.Exists("settings.json"))
            {
                string readText = File.ReadAllText("settings.json");
                backupSettings = JsonConvert.DeserializeObject<BackupSettings>(readText);
                Console.WriteLine("Ayarlar bulundu. Yedekleme yapılıyor.");
            }
            else
            {
                backupSettings = new BackupSettings();
                Console.WriteLine("Yedeklenecek dosya yolunu secin.");
                backupSettings.SourcePath = Console.ReadLine();
                Console.WriteLine("Yedeklerin kaydedilecegi dosya yolunu secin.");
                backupSettings.TargetPath = Console.ReadLine();
                Console.WriteLine("Sifrelenmesini istiyorsaniz sifre giriniz:");
                backupSettings.Password = Console.ReadLine();
                Console.WriteLine("Dosya ismi ne olsun?");
                backupSettings.BackupName = Console.ReadLine();
            }

            try
            {
                if (File.Exists("settings.json"))
                {
                    File.Delete("settings.json");
                }

                using (var tw = new StreamWriter("settings.json", true))
                {
                    tw.WriteLine(JsonConvert.SerializeObject(backupSettings));
                }

                var size = GetDirectorySize($"{backupSettings.TargetPath}\\{backupSettings.BackupName}");

                if (size > 1000000000)
                {
                    string[] files = Directory.GetFiles($"{backupSettings.TargetPath}\\{backupSettings.BackupName}");

                    foreach (string file in files)
                    {
                        FileInfo fi = new FileInfo(file);
                        if (fi.CreationTime < DateTime.Now.AddDays(-15) && fi.CreationTime.DayOfWeek != DayOfWeek.Monday)
                            fi.Delete();
                    }
                }

                zipHelper.CreateBackup(backupSettings.TargetPath, backupSettings.SourcePath, backupSettings.BackupName, backupSettings.Password);

                using (var tw = new StreamWriter("process.log", true))
                {
                    tw.WriteLine($"Backup completed @ {DateTime.Now}");
                }

            }
            catch (Exception ex)
            {
                using (var tw = new StreamWriter("error.log", true))
                {
                    Console.WriteLine(ex.ToString());
                    tw.WriteLine(ex.ToString());
                    Console.ReadKey();
                }
            }
        }
        static long GetDirectorySize(string p)
        {
            // 1.
            // Get array of all file names.
            string[] a = Directory.GetFiles(p, "*.*");

            // 2.
            // Calculate total bytes of all files in a loop.
            long b = 0;
            foreach (string name in a)
            {
                // 3.
                // Use FileInfo to get length of each file.
                FileInfo info = new FileInfo(name);
                b += info.Length;
            }
            // 4.
            // Return total size
            return b;
        }
    }

    public class ZipHelper
    {
        public void CreateBackup(string outPathname, string folderName, string backupFileName, string password = null)
        {
            var fileFolderPath = $"{outPathname}\\{backupFileName}";
            if (!Directory.Exists(fileFolderPath))
            {
                Directory.CreateDirectory(fileFolderPath);
            }
            FileStream fsOut = File.Create($"{fileFolderPath}\\backup-{DateTime.Now.ToString("ddMMyyyyHHmmss")}");
            ZipOutputStream zipStream = new ZipOutputStream(fsOut);

            zipStream.SetLevel(9); //0-9, 9 being the highest level of compression
            if (!string.IsNullOrEmpty(password))
            {
                // optional. Null is the same as not setting. Required if using AES.
                zipStream.Password = password;
            }

            // This setting will strip the leading part of the folder path in the entries, to
            // make the entries relative to the starting folder.
            // To include the full path for each entry up to the drive root, assign folderOffset = 0.
            int folderOffset = folderName.Length + (folderName.EndsWith("\\") ? 0 : 1);

            CompressFolder(folderName, zipStream, folderOffset);

            zipStream.IsStreamOwner = true; // Makes the Close also Close the underlying stream
            zipStream.Close();
        }

        // Recurses down the folder structure
        //
        private void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset)
        {
            string[] files = Directory.GetFiles(path).Where(name => !name.EndsWith(".log")).ToArray();

            foreach (string filename in files)
            {

                FileInfo fi = new FileInfo(filename);

                string entryName = filename.Substring(folderOffset); // Makes the name in zip based on the folder
                entryName = ZipEntry.CleanName(entryName); // Removes drive from name and fixes slash direction
                ZipEntry newEntry = new ZipEntry(entryName);
                newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity

                // Specifying the AESKeySize triggers AES encryption. Allowable values are 0 (off), 128 or 256.
                // A password on the ZipOutputStream is required if using AES.
                //   newEntry.AESKeySize = 256;

                // To permit the zip to be unpacked by built-in extractor in WinXP and Server2003, WinZip 8, Java, and other older code,
                // you need to do one of the following: Specify UseZip64.Off, or set the Size.
                // If the file may be bigger than 4GB, or you do not need WinXP built-in compatibility, you do not need either,
                // but the zip will be in Zip64 format which not all utilities can understand.
                //   zipStream.UseZip64 = UseZip64.Off;
                newEntry.Size = fi.Length;

                zipStream.PutNextEntry(newEntry);

                // Zip the file in buffered chunks
                // the "using" will close the stream even if an exception occurs
                byte[] buffer = new byte[4096];
                using (FileStream streamReader = File.OpenRead(filename))
                {
                    StreamUtils.Copy(streamReader, zipStream, buffer);
                }
                zipStream.CloseEntry();
            }
            string[] folders = Directory.GetDirectories(path);
            foreach (string folder in folders)
            {
                CompressFolder(folder, zipStream, folderOffset);
            }
        }
    }
    public class BackupSettings
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public string Password { get; set; }
        public string BackupName { get; set; }
    }
}
