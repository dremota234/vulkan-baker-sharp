using Silk.NET.Vulkan;

namespace ShaderPipelineBaker.Infrastructure
{
    using Vl = Silk.NET.Vulkan;
    public struct MemoryBuffer
    {
        public Vl.Buffer Buffer;
        public DeviceMemory Memory;
        public ulong Size;
    }

    public unsafe class ComputeShaderData : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;

        #region Интерфейсы и Пайплайн (Конфигурация)

        /// <summary>
        /// Описание портов шейдера (Layout розетки).
        /// </summary>
        public DescriptorSetLayout DescriptorSetLayout { get; set; }

        /// <summary>
        /// Полная панель подключения ресурсов (Pipeline Layout).
        /// </summary>
        public PipelineLayout PipelineLayout { get; set; }

        /// <summary>
        /// Монолитный скомпилированный вычислительный конвейер (Электроприбор).
        /// </summary>
        public Pipeline ComputePipeline { get; set; }

        /// <summary>
        /// Фабрика (пул), из которой выделяются дескрипторные наборы для этого шейдера.
        /// </summary>
        public DescriptorPool DescriptorPool { get; set; }

        /// <summary>
        /// Физическая розетка, связывающая буферы с портами шейдера.
        /// </summary>
        public DescriptorSet DescriptorSet { get; set; }

        #endregion

        #region Память и Массивы данных (Динамические списки)

        /// <summary>
        /// Структура, объединяющая виртуальный буфер и его реальную физическую память на GPU.
        /// </summary>


        /// <summary>
        /// Словарь всех буферов данных, привязанных к портам шейдера.
        /// Ключ — номер binding в шейдере (например: 0 - вход, 1 - выход).
        /// </summary>
        public Dictionary<uint, MemoryBuffer> StorageBuffers { get; } = new();

        #endregion

        #region Выполнение команд

        /// <summary>
        /// Буфер для записи инструкций выполнения (у каждого шейдера свой экземпляр бумаги).
        /// </summary>
        public CommandBuffer CommandBuffer { get; set; }

        #endregion

        /// <summary>
        /// Создает новый контейнер ресурсов для шейдера.
        /// </summary>
        public ComputeShaderData(Vk vk, Device device)
        {
            _vk = vk ?? throw new ArgumentNullException(nameof(vk));
            _device = device;
        }

        /// <summary>
        /// Безопасная очистка всех динамически созданных ресурсов этого шейдера в правильном порядке Vulkan.
        /// </summary>
        public void Dispose()
        {
            CommandBuffer = default;

            foreach (var pair in StorageBuffers.Values)
            {
                if (pair.Buffer.Handle != 0)
                {
                    _vk.DestroyBuffer(_device, pair.Buffer, null);
                }
                if (pair.Memory.Handle != 0)
                {
                    _vk.FreeMemory(_device, pair.Memory, null);
                }
            }
            StorageBuffers.Clear();

            if (DescriptorPool.Handle != 0)
            {
                _vk.DestroyDescriptorPool(_device, DescriptorPool, null);
                DescriptorPool = default;
                DescriptorSet = default;
            }

            if (ComputePipeline.Handle != 0)
            {
                _vk.DestroyPipeline(_device, ComputePipeline, null);
                ComputePipeline = default;
            }

            if (PipelineLayout.Handle != 0)
            {
                _vk.DestroyPipelineLayout(_device, PipelineLayout, null);
                PipelineLayout = default;
            }

            if (DescriptorSetLayout.Handle != 0)
            {
                _vk.DestroyDescriptorSetLayout(_device, DescriptorSetLayout, null);
                DescriptorSetLayout = default;
            }

            GC.SuppressFinalize(this);
        }
    }
}
