using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Utility
{
    public interface IFileUtility
    {
        void CopyRecursive(string sourceDirectory, string targetDirectory, string[] exclusions);

        void CopyRecursive(DirectoryInfo source, DirectoryInfo target, string[] exclusions);
    }

    public class FileUtility : IFileUtility
    {
        public void CopyRecursive(string sourceDirectory, string targetDirectory, string[] exclusions)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyRecursive(diSource, diTarget, exclusions);
        }

        public void CopyRecursive(DirectoryInfo source, DirectoryInfo target, string[] exclusions)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (exclusions.Contains(fi.FullName)) continue;

                // Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                if (exclusions.Contains(diSourceSubDir.FullName)) continue;

                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                CopyRecursive(diSourceSubDir, nextTargetSubDir, exclusions);
            }
        }
    }
}
