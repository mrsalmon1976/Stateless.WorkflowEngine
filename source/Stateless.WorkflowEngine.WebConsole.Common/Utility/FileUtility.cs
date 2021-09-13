using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Common.Utility
{
    public interface IFileUtility
    {
        void CopyRecursive(string sourceDirectory, string targetDirectory, IEnumerable<string> exclusions = null);

        void CopyRecursive(DirectoryInfo source, DirectoryInfo target, IEnumerable<string> exclusions = null);

        void DeleteContents(string sourceDirectory, IEnumerable<string> exclusions = null);

        void DeleteContents(DirectoryInfo source, IEnumerable<string> exclusions = null);

        void DeleteDirectoryRecursive(string directory);

        void DeleteDirectoryRecursive(DirectoryInfo directory);

        void ExtractZipFile(string sourceArchiveFileName, string destinationDirectoryName);
    }

    public class FileUtility : IFileUtility
    {
        public void CopyRecursive(string sourceDirectory, string targetDirectory, IEnumerable<string> exclusions = null)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyRecursive(diSource, diTarget, exclusions);
        }

        public void CopyRecursive(DirectoryInfo source, DirectoryInfo target, IEnumerable<string> exclusions = null)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (exclusions != null && exclusions.Contains(fi.FullName)) continue;

                // Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                if (exclusions != null && exclusions.Contains(diSourceSubDir.FullName)) continue;

                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                CopyRecursive(diSourceSubDir, nextTargetSubDir, exclusions);
            }
        }

        public void DeleteContents(string sourceDirectory, IEnumerable<string> exclusions = null)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);

            DeleteContents(diSource, exclusions);
        }

        public void DeleteContents(DirectoryInfo source, IEnumerable<string> exclusions = null)
        {
            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (exclusions != null)
                {
                    // we need to check the folder AND it's directories
                    if (exclusions.Contains(fi.FullName)) continue;
                }


                // Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
                if (fi.Exists)
                {
                    fi.Delete();
                }
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                if (exclusions != null && exclusions.Contains(diSourceSubDir.FullName)) continue;

                if (diSourceSubDir.Exists)
                {
                    diSourceSubDir.Delete(true);
                }
            }
        }

        public void DeleteDirectoryRecursive(string directory)
        {
            DeleteDirectoryRecursive(new DirectoryInfo(directory));
        }

        public void DeleteDirectoryRecursive(DirectoryInfo directory)
        {
            if (directory.Exists)
            {
                directory.Delete(true);
            }
        }

        public void ExtractZipFile(string sourceArchiveFileName, string destinationDirectoryName)
        {
            ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName);
        }


    }
}
