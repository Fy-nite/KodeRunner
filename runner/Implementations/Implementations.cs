using System.IO.Compression;

namespace KodeRunner
{
    class Implementations {
        public static async Task Import(string ProjectName)
        {
            string project_dir = Path.Combine(
                Core.RootDir,
                Core.CodeDir,
                ProjectName
            );
        }
        public static async Task Export(string ProjectName)
        {
            string project_dir = Path.Combine(
                Core.RootDir,
                Core.CodeDir,
                ProjectName
            );

            string export_file = Path.Combine(
                Core.RootDir,
                Core.ExportDir,
                ProjectName + ".KRproject"
            );
            if(File.Exists(export_file))
            {
                File.Delete(export_file);
            }
            ZipFile.CreateFromDirectory(project_dir, export_file);
            Logger.Log($"Exported {ProjectName} to {export_file}");
        }
    }
}
