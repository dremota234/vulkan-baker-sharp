using System.Text.Json;

namespace ShaderPipelineBaker.Infrastructure.ReadingFiles
{
    using ShadersData.Metadata;

    /// <summary>
    /// Читает JSON-конфиг и заполняет <see cref="ConfigDatas"/>.
    /// <para>Пример ожидаемой структуры JSON-файла:</para>
    /// <code>
    /// {
    ///   "SpriteShader": [
    ///     {
    ///       "hlslPath": "shaders/sprite.hlsl",
    ///       "spvPath": "shaders/sprite.spv",
    ///       "spvTextPath": "shaders/sprite.spv.txt",
    ///       "binPath": "shaders/sprite.bin",
    ///       "csPath": "shaders/sprite.cs"
    ///     }
    ///   ],
    ///   "OpaqueShader": [
    ///     {
    ///       "hlslPath": "shaders/opaque.hlsl",
    ///       "spvPath": "shaders/opaque.spv",
    ///       "spvTextPath": "shaders/opaque.spv.txt",
    ///       "binPath": "shaders/opaque.bin",
    ///       "csPath": "shaders/opaque.cs"
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    internal class ConfigReader
    {
        private readonly string configPath;

        /// <summary>
        /// Список считанных данных конфигурации.
        /// </summary>
        internal List<ConfigData> ConfigDatas { get; private set; } = new();

        /// <summary>
        /// Инициализирует новый экземпляр класса.
        /// </summary>
        /// <param name="configPath">Путь к JSON-файлу конфигурации на диске.</param>
        internal ConfigReader(string configPath) => this.configPath = configPath;

        /// <summary>
        /// Открывает файл конфигурации, считывает JSON и заполняет список.
        /// </summary>
        internal List<ConfigData> Load()
        {
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Конфиг не найден: {configPath}");
            

            string jsonString = File.ReadAllText(configPath);

            // Объект <see cref="JsonDocument"/> для чтения и парсинга JSON-строки
            using JsonDocument doc = JsonDocument.Parse(jsonString);
            JsonElement root = doc.RootElement;

            foreach (JsonProperty shaderProperty in root.EnumerateObject())
            {
                string currentShaderName = shaderProperty.Name;
                JsonElement shaderData = shaderProperty.Value;

                foreach (JsonElement element in shaderData.EnumerateArray())
                {
                    ConfigDatas.Add(new ConfigData(
                        ShaderName: currentShaderName,
                        HlslPath: element.GetProperty("hlslPath").GetString() ?? "",
                        SpvPath: element.GetProperty("spvPath").GetString() ?? "",
                        SpvTextPath: element.GetProperty("spvTextPath").GetString() ?? "",
                        BinPath: element.GetProperty("binPath").GetString() ?? "",
                        CsPath: element.GetProperty("csPath").GetString() ?? ""
                    ));
                }
            }

            return ConfigDatas;
        }
    }
}