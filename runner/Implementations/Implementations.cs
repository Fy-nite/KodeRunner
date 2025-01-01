using System.IO.Compression;

namespace KodeRunner
{
    class Implementations {
        public static void Import(string FilePath)
        {
            string ProjectName = Path.GetFileNameWithoutExtension(FilePath);
            string ExportPath = Path.Combine(
                Core.RootDir,
                Core.CodeDir,
                ProjectName
            );
            ZipFile.ExtractToDirectory(FilePath, ExportPath);
            Logger.Log($"Imported {FilePath} as project {ProjectName}");
        }
        public static void Export(string ProjectName)
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
