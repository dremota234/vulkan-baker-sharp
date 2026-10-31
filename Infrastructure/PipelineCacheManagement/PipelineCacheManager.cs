using Silk.NET.Vulkan;
using System.Text.Json;
using Test.Logic.Infrastructure.Stable.VulkanCore;

namespace Infrastructure.Depending.PipelineCacheManagement
{
    /// <summary>
    /// Менеджер управления кэшем конвейера (Pipeline Cache) для Vulkan.
    /// Автоматически считывает пути для сохранения и загрузки бинарных данных 
    /// из файла конфигурации <c>appsettings.json</c>.
    /// </summary>
    /// <remarks>
    /// Позволяет централизованно хранить скомпилированный машинный код всех шейдеров приложения 
    /// на диске, предотвращая микро-фризы (stuttering) при повторных запусках программы.
    /// </remarks>
    internal class PipelineCacheManager : IDisposable
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="PipelineCacheManager"/>, 
        /// используя контекст Vulkan и путь к конфигурационному файлу JSON.
        /// </summary>
        /// <param name="vulkanContext">Объект графического контекста <see cref="VulkanContext"/>, предоставляющий доступ к API и логическому устройству.</param>
        /// <param name="appSettingsPath">Абсолютный или относительный путь к файлу конфигурации <c>appsettings.json</c>.</param>
        internal PipelineCacheManager(VulkanContext vulkanContext, string appSettingsPath)
        {
            _vulkanApi = vulkanContext.VulkanApi;
            _logicalDevice = vulkanContext.LogicalDevice;
            _filename = GetPathFromConfig(appSettingsPath);

            _pipelineCache = LoadPipelineCache();
        }

        private readonly Vk _vulkanApi;
        private readonly Device _logicalDevice;
        private readonly string _filename;
        private PipelineCache _pipelineCache;
        private bool _disposed = false;

        /// <summary>
        /// Возвращает текущий активный объект кэша конвейера.
        /// </summary>
        internal PipelineCache Cache => _pipelineCache;

        /// <summary>
        /// Извлекает из JSON-структуры свойства "CacheFilePath" и "CacheFileName" 
        /// и объединяет их в один полный путь к файлу.
        /// </summary>
        /// <param name="configPath">Путь к файлу конфигурации.</param>
        /// <returns>Полный путь к бинарному файлу кэша.</returns>
        private string GetPathFromConfig(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                    throw new FileNotFoundException($"Файл конфигурации не найден: {configPath}");

                string jsonString = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(jsonString);
                JsonElement root = doc.RootElement;

                JsonElement cacheSection = root.GetProperty("PipelineCache");
                string folder = cacheSection.GetProperty("CacheFilePath").GetString()!;
                string file = cacheSection.GetProperty("CacheFileName").GetString()!;

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                return Path.Combine(folder, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cache Error] Не удалось прочитать конфиг: {ex.Message}. Откат на путь по умолчанию.");
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vulkan_cache.bin");
            }
        }

        /// <summary>
        /// Извлекает текущие бинарные данные из кэша конвейера (Pipeline Cache) 
        /// и сохраняет их в файл на диске для повторного использования при следующем запуске.
        /// </summary>
        /// <param name="pipelineCache">
        /// Активный объект <see cref="PipelineCache"/>, содержащий скомпилированные в процессе работы приложения шейдеры.
        /// </param>
        /// <remarks>
        /// Метод считывает данные в два этапа: сначала запрашивает размер буфера у Vulkan, 
        /// а затем копирует сам blob данных через фиксированный в памяти массив (fixed pointer). 
        /// Рекомендуется вызывать этот метод перед самым закрытием приложения или при выходе из уровня.
        /// </remarks>
        internal unsafe void SavePipelineCache()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_pipelineCache.Handle == 0) return;

            nuint cacheSize = 0;

            Result result = _vulkanApi.GetPipelineCacheData(_logicalDevice, _pipelineCache, &cacheSize, null);

            if (result != Result.Success || cacheSize == 0)
            {
                Console.WriteLine("Кэш пуст или не удалось получить его размер.");
                return;
            }

            byte[] cacheData = new byte[cacheSize];

            fixed (byte* pCacheData = cacheData)
            {
                result = _vulkanApi.GetPipelineCacheData(_logicalDevice, _pipelineCache, &cacheSize, pCacheData);
            }

            if (result == Result.Success)
            {
                File.WriteAllBytes(_filename, cacheData);
                Console.WriteLine($"Pipeline cache успешно сохранен в {_filename} ({cacheSize} байт).");
            }
        }

        /// <summary>
        /// Загружает сохраненный кэш конвейера (Pipeline Cache) из бинарного файла на диске 
        /// или создает новый пустой кэш, если файл отсутствует или устарел.
        /// </summary>
        /// <remarks>
        /// Драйвер Vulkan автоматически проверяет валидность данных в заголовке файла 
        /// (соответствие Vendor ID, Device ID и версии драйвера). Если данные невалидны, 
        /// они будут безопасно проигнорированы, и метод вернет пустой кэш без сбоя программы.
        /// </remarks>
        /// <returns>
        /// Инициализированный объект <see cref="PipelineCache"/>, готовый к передаче 
        /// в функции создания графических или вычислительных конвейеров.
        /// </returns>
        /// <exception cref="Exception">
        /// Генерируется, если Vulkan не смог создать даже пустой объект кэша конвейера.
        /// </exception>
        internal unsafe PipelineCache LoadPipelineCache()
        {
            byte[] cacheData = null!;

            if (File.Exists(_filename))
            {
                try { cacheData = File.ReadAllBytes(_filename); }
                catch (Exception ex) { Console.WriteLine($"Ошибка чтения файла кэша: {ex.Message}"); }
            }

            PipelineCacheCreateInfo cacheCreateInfo = new PipelineCacheCreateInfo
            {
                SType = StructureType.PipelineCacheCreateInfo,  // Идентификатор типа структуры.
                PNext = null,                                   // Указатель на следующую структуру расширения.
                Flags = 0                                       // Битовые флаги (настройки поведения), управляющие созданием объекта.
            };

            PipelineCache pipelineCache;

            if (cacheData != null && cacheData.Length > 0)
            {
                fixed (byte* pInitialData = cacheData)
                {
                    cacheCreateInfo.InitialDataSize = (nuint)cacheData.Length;
                    cacheCreateInfo.PInitialData = pInitialData;

                    if (_vulkanApi.CreatePipelineCache(_logicalDevice, &cacheCreateInfo, null, &pipelineCache) == Result.Success)
                    {
                        Console.WriteLine("Pipeline cache успешно загружен из файла.");
                        return pipelineCache;
                    }
                }
            }

            Console.WriteLine("Создается новый пустой Pipeline cache.");
            cacheCreateInfo.InitialDataSize = 0;
            cacheCreateInfo.PInitialData = null;

            if (_vulkanApi.CreatePipelineCache(_logicalDevice, &cacheCreateInfo, null, &pipelineCache) != Result.Success)
                throw new Exception("Не удалось создать даже пустой Pipeline Cache.");

            return pipelineCache;
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;

            SavePipelineCache();

            if (_pipelineCache.Handle != 0)
            {
                _vulkanApi.DestroyPipelineCache(_logicalDevice, _pipelineCache, null);
                _pipelineCache = default;
                Console.WriteLine("Ресурсы Pipeline Cache успешно освобождены в Vulkan.");
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~PipelineCacheManager()
        {
            if (!_disposed && _pipelineCache.Handle != 0)
            {
                Console.WriteLine("[Warning] PipelineCacheManager не был уничтожен через Dispose(). Возможна утечка ресурсов.");
            }
        }
    }
}