namespace ShaderPipelineBaker.Infrastructure.ReadingFiles.ShadersData.Metadata;

/// <summary>
/// Хранит пути к файлам и базовые настройки для сохранения и компиляции конкретного шейдера.
/// </summary>
internal record ConfigData(
    /// <summary>Уникальное имя шейдера в системе.</summary>
    string ShaderName,

    /// <summary>Путь к исходному файлу кода на HLSL (High-Level Shader Language).</summary>
    string HlslPath,

    /// <summary>Путь, по которому будет сохранен скомпилированный бинарный SPIR-V файл (.spv).</summary>
    string SpvPath,

    /// <summary>Путь к текстовому представлению SPIR-V (дизассемблированный код для отладки).</summary>
    string SpvTextPath,

    /// <summary>Путь к финальному бинарному файлу конфигурации конвейера.</summary>
    string BinPath,

    /// <summary>Путь к сгенерированному C# файлу-обертке для удобного вызова шейдера из кода.</summary>
    string CsPath
);

/// <summary>
/// Содержит метаданные о вычислительном ядре (Kernel) шейдера и геометрии его рабочих групп.
/// </summary>
internal record KernelInfo(
    /// <summary>Модель выполнения шейдера (Execution Model), например: GLCompute.</summary>
    string ExecModel,

    /// <summary>Имя функции-точки входа в шейдер (обычно "main").</summary>
    string Name,

    /// <summary>Тип интерфейса взаимодействия (например, Vulkan, Direct3D).</summary>
    string Interface,

    /// <summary>Размер локальной группы потоков по оси X (LocalSizeX).</summary>
    string LocalSizeX,

    /// <summary>Размер локальной группы потоков по оси Y (LocalSizeY).</summary>
    string LocalSizeY,

    /// <summary>Размер локальной группы потоков по оси Z (LocalSizeZ).</summary>
    string LocalSizeZ
);

/// <summary>
/// Описывает параметры графического или вычислительного буфера (Buffer), привязанного к шейдеру.
/// </summary>
internal record BufferInfo(
    /// <summary>Имя буфера, заданное в коде шейдера.</summary>
    string Name,

    /// <summary>Имя типа данных буфера (например, структуры или массива).</summary>
    string TypeName,

    /// <summary>Индекс Descriptor Set (набора дескрипторов), к которому привязан буфер.</summary>
    string DescriptorSet,

    /// <summary>Индекс Binding (точки привязки) внутри своего Descriptor Set.</summary>
    string Binding,

    /// <summary>Флаг, разрешающий шейдеру записывать данные в этот буфер (true) или только читать (false).</summary>
    bool IsWritable,

    /// <summary>Тип элементов, хранящихся в буфере (например, float, int, struct).</summary>
    string ElementType,

    /// <summary>Разрядность одного элемента в битах (например, 32 для float, 64 для double).</summary>
    int ElementBitWidth,

    /// <summary>Шаг массива (Array Stride) — расстояние в байтах между началом соседних элементов буфера.</summary>
    int ArrayStride
);

/// <summary>
/// Финальный агрегированный объект, объединяющий в себе полную информацию о шейдере: 
/// его настройки сохранения, параметры вычислительного ядра и все связанные с ним буферы.
/// </summary>
internal record ShaderData(
    /// <summary>Конфигурация вычислительного ядра шейдера.</summary>
    KernelInfo ShaderKernelSettings,

    /// <summary>Список всех входящих и выходящих буферов, используемых шейдером.</summary>
    List<BufferInfo> ShaderBuffersSettings,

    /// <summary>Настройки путей сохранения файлов для данного шейдера.</summary>
    ConfigData ShaderSaveSettings
);