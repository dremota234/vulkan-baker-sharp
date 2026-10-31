using Silk.NET.Vulkan;

namespace Test.Logic.Infrastructure.Stable.VulkanCore
{
    /// <summary>
    /// Хранит активный контекст API Vulkan и дескрипторы созданных GPU-ресурсов.
    /// Управляет жизненным циклом и гарантирует освобождение неуправляемой памяти.
    /// </summary>
    public class VulkanContext : IDisposable
    {
        /// <summary>
        /// Экземпляр загруженного API Vulkan. Используется для доступа к глобальным функциям и методам расширений.
        /// </summary>
        internal Vk VulkanApi = null!;

        /// <summary>
        /// Дескриптор экземпляра Vulkan (Instance). Объединяет состояние приложения и связывает его с системным драйвером.
        /// </summary>
        internal Instance Instance;

        /// <summary>
        /// Выбранный физический GPU (видеокарта) в системе, используемый для выполнения вычислительных или графических задач.
        /// </summary>
        internal PhysicalDevice SelectedGPU;

        /// <summary>
        /// Логическое устройство Vulkan. Представляет собой изолированный программный интерфейс для взаимодействия с выбранным GPU.
        /// </summary>
        internal Device LogicalDevice;

        /// <summary>
        /// Очередь выполнения команд GPU. Используется для отправки буферов команд (вычислительных или графических) на исполнение видеокарте.
        /// </summary>
        internal Queue CommandQueue;

        /// <summary>
        /// Индекс семейства очередей видеокарты, поддерживающего математические вычисления (Compute).
        /// </summary>
        internal uint QueueFamilyIndex;

        /// <summary>
        /// Пул памяти для быстрого выделения и переиспользования буферов команд.
        /// </summary>
        internal CommandPool CommandPool;

        /// <summary>
        /// Автоматическая очистка после последнего использования обекта класса
        /// </summary>
        public unsafe void Dispose()
        {
            if (CommandPool.Handle != 0)
            {
                VulkanApi.DestroyCommandPool(LogicalDevice, CommandPool, null);
                CommandPool = default;
            }

            if (LogicalDevice.Handle != 0)
            {
                VulkanApi.DestroyDevice(LogicalDevice, null);
                LogicalDevice = default;
            }

            if (Instance.Handle != 0)
            {
                VulkanApi.DestroyInstance(Instance, null);
                Instance = default;
            }

            SelectedGPU = default;
            CommandQueue = default;
            VulkanApi = null!;

            GC.SuppressFinalize(this);
        }
    }
}
