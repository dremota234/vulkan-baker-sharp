using Infrastructure.Depending.PipelineCacheManagement;
using ShaderPipelineBaker.Infrastructure.FileGenLogic;
using Silk.NET.Vulkan;
using System.Text.Json;
using Test.Logic.Infrastructure.Stable.VulkanCore;

namespace ShaderPipelineBaker.Infrastructure
{
    internal class Appsettings_Data
    {
        internal Appsettings_Data(
            string hlslPath = null!,
            string spvPath = null!,
            string txtPath = null!,
            string endPath = null!,
            string fileName = null!
            )
        {
            this.hlslPath = hlslPath;
            this.spvPath = spvPath;
            this.txtPath = txtPath;
            this.endPath = endPath;
            this.fileName = fileName;
        }

        internal string hlslPath = null!;
        internal string spvPath = null!;
        internal string txtPath = null!;
        internal string endPath = null!;
        internal string fileName = null!;
    }

    internal class GenFile : IDisposable
    {
        private const string appSettingsPath =
            "C:\\Users\\User\\PhysXCompute\\ShaderPipelineBaker\\Infrastructure\\appsettings.json"; // TODO:

        private static void Main(string[] args)
        {
            var vulkanContext = new VulkanBuilder().Build();

            using var genFile = new GenFile();
            var data = genFile.LoadConfiguration();

            using var cacheFileReader = new PipelineCacheManager(vulkanContext, appSettingsPath);

            genFile.Generate
            (
                data: data,
                vulkanContext: vulkanContext,
                cache: cacheFileReader.Cache
            );
        }

        private Appsettings_Data LoadConfiguration()
        {
            if (!File.Exists(appSettingsPath))
                throw new FileNotFoundException($"Файл не найден: {appSettingsPath}");

            try
            {
                string jsonString = File.ReadAllText(appSettingsPath);
                using JsonDocument doc = JsonDocument.Parse(jsonString);

                string fileName = doc.RootElement.GetProperty("FileName").GetString() ?? "Exception_Of_Search_Name";

                JsonElement root = doc.RootElement.GetProperty("ShaderPaths");

                return (new(
                    root.GetProperty("HlslPath").GetString() ?? "",
                    root.GetProperty("SpvPath").GetString() ?? "",
                    root.GetProperty("TxtPath").GetString() ?? "",
                    root.GetProperty("EndPath").GetString() ?? "",
                    fileName
                    )
                );
            }
            catch (Exception ex) { throw new InvalidOperationException($"Ошибка чтения JSON: {ex.Message}", ex); }
        }

        internal void Generate
            (
                Appsettings_Data data,
                VulkanContext vulkanContext,
                PipelineCache cache
            )
        {
            var shaderReader = new ShaderReader(data);
            shaderReader.CompileAndDisassemble();
            byte[] shaderBytes = File.ReadAllBytes(data.spvPath);//
            var generator = new GeneratedShader(vulkanContext);
            string generatedCode = generator.GenerateFill(data.fileName, shaderBytes);
            File.WriteAllText(data.endPath + data.fileName, generatedCode);
        }

        public void Dispose()
        {

        }
    }
}
