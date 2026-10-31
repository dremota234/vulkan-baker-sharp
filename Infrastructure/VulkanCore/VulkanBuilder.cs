using Silk.NET.Vulkan;

namespace Test.Logic.Infrastructure.Stable.VulkanCore
{
    public unsafe class VulkanBuilder
    {
        private readonly VulkanContext _context = new();

        public VulkanBuilder() { }

        /// <summary>
        /// Запускает строгий пошаговый конвейер инициализации Vulkan и возвращает полностью готовый контекст.
        /// </summary>
        /// <returns>Настроенный и готовый к работе экземпляр <see cref="VulkanContext"/>.</returns>
        public VulkanContext Build()
        {
            Api_Loading();
            Instance_Creation();
            PhysicalDevice_Selection();
            LogicalDevice_Creation();
            CommandQueue_Retrieval(); 
            QueueFamilyIndex_Get();
            CommandPool_Creation(); 

            return _context;
        }

        #region Методы для Build
        /// <summary>
        /// Поиск и связывание системной библиотеки Vulkan (vulkan-1.dll/.so) с C#-окружением.
        /// </summary>
        private void Api_Loading() => _context.VulkanApi = Vk.GetApi();

        /// <summary>
        /// Настраивает конфигурацию и создает Экземпляр (Instance) Vulkan.
        /// </summary>
        private void Instance_Creation()
        {
            ReadOnlySpan<byte> appName = "PhysX\0"u8;
            ReadOnlySpan<byte> engineName = "PhysXCompute\0"u8;

            fixed (byte* pAppName = appName)
            fixed (byte* pEngineName = engineName)
            {
                ApplicationInfo appInfo = new ApplicationInfo
                {
                    SType = StructureType.ApplicationInfo,              // Идентификатор типа данной структуры
                    PApplicationName = pAppName,                        // Указатель на UTF-8 имя приложения
                    ApplicationVersion = Vk.MakeVersion(1, 0, 0),       // Мажорная/минорная версия приложения
                    PEngineName = pEngineName,                          // Указатель на UTF-8 имя движка
                    EngineVersion = Vk.MakeVersion(1, 0, 0),            // Версия используемого движка
                    ApiVersion = Vk.Version12                           // Требуемая версия спецификации Vulkan (1.2)
                };

                InstanceCreateInfo createInfo = new InstanceCreateInfo
                {
                    SType = StructureType.InstanceCreateInfo,           // Идентификатор типа структуры создания инстанса
                    PApplicationInfo = &appInfo,                        // Адрес структуры с информацией о приложении
                    EnabledExtensionCount = 0,                          // Количество активируемых глобальных расширений
                    PpEnabledExtensionNames = null,                     // Массив имен расширений (отсутствует)
                    EnabledLayerCount = 0,                              // Количество активируемых слоев валидации/отладки
                    PpEnabledLayerNames = null                          // Массив имен слоев отладки (отсутствует)
                };

                Instance instance;
                Result result = _context.VulkanApi.CreateInstance(&createInfo, null, &instance);

                if (result != Result.Success) throw new Exception($"Не удалось создать Instance Vulkan: {result}");

                _context.Instance = instance;
            }
        }

        /// <summary>
        /// Опрос системы и выбор подходящей физической видеокарты (GPU).
        /// </summary>
        private void PhysicalDevice_Selection()
        {
            var vkApi = _context.VulkanApi;
            var instance = _context.Instance;

            uint deviceCount = 0;
            vkApi.EnumeratePhysicalDevices(instance, &deviceCount, null); // Считаем количество видеокарт в ПК 
            if (deviceCount == 0) throw new Exception("Критическая ошибка: В системе не найдено видеокарт с поддержкой Vulkan!");

            PhysicalDevice* physicalDevices = stackalloc PhysicalDevice[(int)deviceCount];
            vkApi.EnumeratePhysicalDevices(instance, &deviceCount, physicalDevices);

            PhysicalDevice bestGpu = default;
            int bestScore = -1; // Рейтинг для выбора лучшей карты

            for (int i = 0; i < deviceCount; i++)
            {
                PhysicalDevice currentGpu = physicalDevices[i];

                PhysicalDeviceProperties properties;
                vkApi.GetPhysicalDeviceProperties(currentGpu, &properties);

                uint majorVersion = (properties.ApiVersion >> 22) & 0x7F;
                if (majorVersion < 1 || (majorVersion == 1 && ((properties.ApiVersion >> 12) & 0x3FF) < 2))
                {
                    continue;
                }

                int currentScore = 0;

                if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu)
                {
                    currentScore += 10000;
                }

                else if (properties.DeviceType == PhysicalDeviceType.IntegratedGpu)
                {
                    currentScore += 1000;
                }

                currentScore += (int)(properties.Limits.MaxImageDimension2D / 100);

                if (currentScore > bestScore)
                {
                    bestScore = currentScore;
                    bestGpu = currentGpu;
                }
            }

            if (bestGpu.Handle == 0)
            {
                throw new Exception("Не найдено подходящее графическое устройство с поддержкой Vulkan!");
            }

            _context.SelectedGPU = bestGpu;
        }

        /// <summary>
        /// Настройка параметров и создание Логического устройства (интерфейса управления GPU).
        /// </summary>
        private void LogicalDevice_Creation()
        {
            Device LogicalDevice;

            float queuePriority = 1.0f;

            DeviceQueueCreateInfo queueCreateInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = 0,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            PhysicalDeviceFeatures deviceFeatures = new PhysicalDeviceFeatures();

            PhysicalDeviceVulkan12Features features12 = new PhysicalDeviceVulkan12Features
            {
                SType = StructureType.PhysicalDeviceVulkan12Features,
                PNext = null,
                DescriptorIndexing = true,
                RuntimeDescriptorArray = true,
                ScalarBlockLayout = true
            };

            DeviceCreateInfo deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                PNext = &features12,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                PEnabledFeatures = &deviceFeatures,
                EnabledExtensionCount = 0,
                PpEnabledExtensionNames = null
            };

            Result result = _context.VulkanApi.CreateDevice(_context.SelectedGPU, &deviceCreateInfo, null, &LogicalDevice);

            if (result != Result.Success) throw new Exception($"Не удалось создать логическое устройство Device: {result}");

            _context.LogicalDevice = LogicalDevice;
        }

        /// <summary>
        /// Получение доступа к аппаратной очереди команд (Queue) для отправки задач на GPU.
        /// </summary>
        private void CommandQueue_Retrieval()
        {
            Queue localQueue;

            _context.VulkanApi.GetDeviceQueue(_context.LogicalDevice, 0, 0, &localQueue);

            if (localQueue.Handle == 0) throw new Exception("Критическая ошибка: Не удалось получить доступ к очереди GPU!");

            _context.CommandQueue = localQueue;
        }

        /// <summary>
        /// Создает пул памяти для выделения и переиспользования буферов команд вычислений.
        /// </summary>
        private void CommandPool_Creation()
        {
            CommandPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.CommandPoolCreateInfo,
                PNext = null,
                QueueFamilyIndex = _context.QueueFamilyIndex,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };

            CommandPool pool;
            Result result = _context.VulkanApi.CreateCommandPool(
                _context.LogicalDevice,
                &poolInfo,
                null,
                &pool
            );

            if (result != Result.Success)
            {
                throw new Exception($"Не удалось создать Command Pool. Код ошибки Vulkan: {result}");
            }

            _context.CommandPool = pool;
        }

        /// <summary>
        /// Находит индекс семейства очередей видеокарты, поддерживающего математические вычисления (Compute).
        /// </summary>
        private void QueueFamilyIndex_Get()
        {
            uint queueCount = 0;
            _context.VulkanApi.GetPhysicalDeviceQueueFamilyProperties(_context.SelectedGPU, &queueCount, null);

            if (queueCount == 0)
                throw new Exception("Физическое устройство Vulkan не вернуло ни одного семейства очередей!");

            QueueFamilyProperties* queueProps = stackalloc QueueFamilyProperties[(int)queueCount];
            _context.VulkanApi.GetPhysicalDeviceQueueFamilyProperties(_context.SelectedGPU, &queueCount, queueProps);

            uint queueFamilyIndex = uint.MaxValue;

            for (uint i = 0; i < queueCount; i++)
            {
                if ((queueProps[i].QueueFlags & QueueFlags.ComputeBit) != 0)
                {
                    queueFamilyIndex = i;
                    break;
                }
            }

            if (queueFamilyIndex == uint.MaxValue)
                throw new Exception("Эта видеокарта не поддерживает вычисления (Compute Shaders)!");
            
            _context.QueueFamilyIndex = queueFamilyIndex;
        }
        #endregion
    }
}
