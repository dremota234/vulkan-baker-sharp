namespace ShaderPipelineBaker.Infrastructure.ReadingFiles.ShadersData.Registry;

using Metadata;

/// <summary>
/// Глобальное статическое хранилище конфигураций шейдеров.
/// </summary>
internal static class RegistryConfigData
{
    /// <summary>
    /// Список всех доступных конфигурационных данных (путей к файлам) для шейдеров.
    /// </summary>
    internal static List<ConfigData> ConfigDataList = new();

    /// <summary>
    /// Перебирает все <see cref="ConfigData"/> из <see cref="ConfigDataList"/> и передает в выбранный метод.
    /// </summary>
    internal static void ForEach(Action<ConfigData> action)
    {
        foreach (var item in ConfigDataList) action(item);
    }

    /// <summary>
    /// Представляет элемент реестра для сборки и хранения информации об отдельном шейдере.
    /// Служит временным контейнером (строителем) перед созданием финального объекта <see cref="ShaderData"/>.
    /// </summary>
    internal class ShaderRegistryItem
    {
        /// <summary>
        /// Инициализирует новый экземпляр реестра для шейдера и связывает его с конфигурацией по указанному индексу.
        /// </summary>
        /// <param name="configDataIndex">Индекс конфигурации в глобальном списке <see cref="RegistryConfigData.ConfigDataList"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Вызывается, если переданный индекс находится вне границ списка конфигураций.</exception>
        internal ShaderRegistryItem(ConfigData configData) =>
            ConfigData = configData;

        /// <summary>
        /// Список описаний буферов, которые привязаны и используются данным шейдером.
        /// </summary>
        internal List<BufferInfo> BufferInfoList { get; set; } = new();

        /// <summary>
        /// Конфигурационные данные шейдера (имя, пути к исходникам и бинарным файлам).
        /// </summary>
        internal ConfigData ConfigData { get; set; }

        /// <summary>
        /// Информация о ядре шейдера (модель выполнения, размеры локальных групп). 
        /// Может быть <see langword="null"/> до момента заполнения.
        /// </summary>
        internal KernelInfo? KernelInfo { get; set; }

        /// <summary>
        /// Финальный собранный объект данных шейдера. 
        /// Инициализируется только после вызова метода <see cref="FillShaderData"/>.
        /// </summary>
        internal ShaderData? ShaderData { get; set; }

        /// <summary>
        /// Проверяет полноту метаданных, собирает и возвращает итоговый объект <see cref="ShaderData"/>, 
        /// а также сохраняет его в свойство <see cref="ShaderData"/>.
        /// </summary>
        /// <returns>Полностью сформированный объект <see cref="ShaderData"/> с настройками ядра, буферов и путей.</returns>
        /// <exception cref="InvalidOperationException">
        /// Вызывается в следующих случаях:
        /// <list type="bullet">
        /// <item><description>Если не задан <see cref="KernelInfo"/>.</description></item>
        /// <item><description>Если отсутствует <see cref="ConfigData"/>.</description></item>
        /// <item><description>Если список <see cref="BufferInfoList"/> пуст.</description></item>
        /// </list>
        /// </exception>
        internal ShaderData FillShaderData()
        {
            if (KernelInfo == null) throw new InvalidOperationException
                    ("KernelInfo не задан! Невозможно собрать ShaderData.");

            if (ConfigData == null) throw new InvalidOperationException
                    ("ConfigData не задан! Невозможно собрать ShaderData.");

            if (BufferInfoList.Count == 0) throw new InvalidOperationException
                    ("BufferInfoList содержит 0 элементов! Невозможно собрать ShaderData.");

            ShaderData = new
                (
                    ShaderKernelSettings: KernelInfo,
                    ShaderBuffersSettings: BufferInfoList,
                    ShaderSaveSettings: ConfigData
                );

            return ShaderData;
        }
    }
}