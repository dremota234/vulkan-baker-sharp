using ShaderPipelineBaker.Infrastructure.FileGenLogic;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Text;
using Test.Logic.Infrastructure.Stable.VulkanCore;

namespace ShaderPipelineBaker.Infrastructure.FileGenLogic
{
    internal class GeneratedShader
    {
        internal GeneratedShader(VulkanContext vulkanContext, StringBuilder stringBuilder = null!)
        {
            _vulkanContext = vulkanContext;
            _code = stringBuilder ?? new StringBuilder();
        }

        private VulkanContext _vulkanContext;
        private StringBuilder _code;

        internal string GenerateFill(
            string className,
            byte[] shaderBytes
            )
        {
            // Превращаем байты шейдера в C#-массив для авто-встраивания
            string shaderBytesString = string.Join(", ", shaderBytes);

            _code.AppendLine("using System;");
            _code.AppendLine("using System.Collections.Generic;");
            _code.AppendLine("using System.Runtime.CompilerServices;");
            _code.AppendLine("using System.Runtime.InteropServices;");
            _code.AppendLine("using Silk.NET.Vulkan;");
            _code.AppendLine("using Buffer = Silk.NET.Vulkan.Buffer;");
            _code.AppendLine();
            _code.AppendLine($"public unsafe class {className} : IDisposable");
            _code.AppendLine("{");

            // 1. Поля Vulkan-контекста и ресурсов
            _code.AppendLine("    private readonly VulkanContext _context;");
            foreach (var kernel in ShaderReader.Kernels.Values)
                _code.AppendLine($"    private Pipeline _pipeline_{kernel.Name};");
            _code.AppendLine();
            _code.AppendLine("    private PipelineLayout _layout;");
            _code.AppendLine("    private DescriptorPool _pool;");
            _code.AppendLine("    private DescriptorSet _set;");
            _code.AppendLine("    private ShaderModule _shaderModule;");
            _code.AppendLine();

            // 2. Строго типизированные поля для хранения созданных буферов и памяти
            foreach (var buffer in ShaderReader.Buffers.Values)
            {
                _code.AppendLine($"    public Buffer {buffer.Name} {{ get; private set; }}");
                _code.AppendLine($"    public DeviceMemory {buffer.Name}Memory {{ get; private set; }}");
                _code.AppendLine($"    public ulong {buffer.Name}Size {{ get; private set; }}");
                _code.AppendLine();
            }

            // 3. Конструктор, принимающий ТОЛЬКО VulkanContext и размеры буферов
            _code.Append($"    public {className}(VulkanContext context, ");
            List<string> sizeParams = new();
            foreach (var buffer in ShaderReader.Buffers.Values)
                sizeParams.Add($"uint {buffer.Name}ElementCount");
            _code.Append(string.Join(", ", sizeParams));
            _code.AppendLine(")");
            _code.AppendLine("    {");
            _code.AppendLine("        _context = context;");
            _code.AppendLine("        var vk = context.VulkanApi;");
            _code.AppendLine("        var device = context.LogicalDevice;");
            _code.AppendLine();

            // Встраиваем бинарный SPIR-V шейдер прямо в класс
            _code.AppendLine($"        byte[] shaderBytes = new byte[] {{ {shaderBytesString} }};");
            _code.AppendLine();

            // Создание буферов на основе ArrayStride из парсера
            foreach (var buffer in ShaderReader.Buffers.Values)
            {
                _code.AppendLine($"        {buffer.Name}Size = {buffer.Name}ElementCount * {buffer.ArrayStride}UL;");
                _code.AppendLine($"        (Buffer {buffer.Name}, DeviceMemory {buffer.Name}Memory) = CreateBufferInternal({buffer.Name}Size, BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);");
                _code.AppendLine($"        this.{buffer.Name} = {buffer.Name};");
                _code.AppendLine($"        this.{buffer.Name}Memory = {buffer.Name}Memory;");
                _code.AppendLine();
            }

            // Разметка Descriptor Set Layout
            _code.AppendLine($"        int bindingCount = {ShaderReader.Buffers.Count};");
            _code.AppendLine("        DescriptorSetLayoutBinding* bindings = stackalloc DescriptorSetLayoutBinding[bindingCount];");
            int idx = 0;
            foreach (var buffer in ShaderReader.Buffers.Values)
            {
                _code.AppendLine($"        bindings[{idx}] = new DescriptorSetLayoutBinding {{ Binding = {buffer.Binding}, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit }};");
                idx++;
            }
            _code.AppendLine();

            _code.AppendLine("        var layoutCreateInfo = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = (uint)bindingCount, PBindings = bindings };");
            _code.AppendLine("        DescriptorSetLayout setLayout;");
            _code.AppendLine("        vk.CreateDescriptorSetLayout(device, &layoutCreateInfo, null, &setLayout).Assert();");
            _code.AppendLine();

            // Pipeline Layout
            _code.AppendLine("        var pipelineLayoutCreateInfo = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = &setLayout };");
            _code.AppendLine("        vk.CreatePipelineLayout(device, &pipelineLayoutCreateInfo, null, out _layout).Assert();");
            _code.AppendLine();

            // Пул и Сет дескрипторов
            _code.AppendLine($"        var poolSize = new DescriptorPoolSize {{ Type = DescriptorType.StorageBuffer, DescriptorCount = (uint)bindingCount }};");
            _code.AppendLine("        var poolCreateInfo = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 1, PoolSizeCount = 1, PPoolSizes = &poolSize };");
            _code.AppendLine("        vk.CreateDescriptorPool(device, &poolCreateInfo, null, out _pool).Assert();");
            _code.AppendLine();
            _code.AppendLine("        var allocInfo = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _pool, DescriptorSetCount = 1, PSetLayouts = &setLayout };");
            _code.AppendLine("        vk.AllocateDescriptorSets(device, &allocInfo, out _set).Assert();");
            _code.AppendLine();

            // Привязка созданных буферов к дескрипторам
            _code.AppendLine("        WriteDescriptorSet* writeSets = stackalloc WriteDescriptorSet[bindingCount];");
            _code.AppendLine("        DescriptorBufferInfo* bufferInfos = stackalloc DescriptorBufferInfo[bindingCount];");
            idx = 0;
            foreach (var buffer in ShaderReader.Buffers.Values)
            {
                _code.AppendLine($"        bufferInfos[{idx}] = new DescriptorBufferInfo {{ Buffer = this.{buffer.Name}, Offset = 0, Range = this.{buffer.Name}Size }};");
                _code.AppendLine($"        writeSets[{idx}] = new WriteDescriptorSet {{ SType = StructureType.WriteDescriptorSet, DstSet = _set, DstBinding = {buffer.Binding}, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &bufferInfos[{idx}] }};");
                idx++;
            }
            _code.AppendLine("        vk.UpdateDescriptorSets(device, (uint)bindingCount, writeSets, 0, null);");
            _code.AppendLine("        vk.DestroyDescriptorSetLayout(device, setLayout, null);");
            _code.AppendLine();

            // Shader Module
            _code.AppendLine("        fixed (byte* pCode = shaderBytes)");
            _code.AppendLine("        {");
            _code.AppendLine("            var moduleCreateInfo = new ShaderModuleCreateInfo { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)shaderBytes.Length, PCode = (uint*)pCode };");
            _code.AppendLine("            vk.CreateShaderModule(device, &moduleCreateInfo, null, out _shaderModule).Assert();");
            _code.AppendLine("        }");
            _code.AppendLine();

            // Генерация пайплайнов для КАЖДОГО кернела
            foreach (var kernel in ShaderReader.Kernels.Values)
            {
                _code.AppendLine($"        var entryPointStr_{kernel.Name} = Marshal.StringToHGlobalAnsi(\"{kernel.Name}\");");
                _code.AppendLine($"        var stageCreateInfo_{kernel.Name} = new PipelineShaderStageCreateInfo {{ SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = _shaderModule, PName = (byte*)entryPointStr_{kernel.Name} }};");
                _code.AppendLine($"        var pipelineCreateInfo_{kernel.Name} = new ComputePipelineCreateInfo {{ SType = StructureType.ComputePipelineCreateInfo, Stage = stageCreateInfo_{kernel.Name}, Layout = _layout }};");
                _code.AppendLine($"        vk.CreateComputePipelines(device, default, 1, &pipelineCreateInfo_{kernel.Name}, null, out _pipeline_{kernel.Name}).Assert();");
                _code.AppendLine($"        Marshal.FreeHGlobal(entryPointStr_{kernel.Name});");
                _code.AppendLine();
            }
            _code.AppendLine("    }");
            _code.AppendLine();

            // 4. Методы записи / чтения данных (Обобщенные)
            _code.AppendLine("    public void WriteData<T>(DeviceMemory memory, ReadOnlySpan<T> data, ulong size) where T : unmanaged");
            _code.AppendLine("    {");
            _code.AppendLine("        void* mappedData;");
            _code.AppendLine("        _context.VulkanApi.MapMemory(_context.LogicalDevice, memory, 0, size, 0, &mappedData).Assert();");
            _code.AppendLine("        fixed (T* pData = data) Unsafe.CopyBlock(mappedData, pData, (uint)size);");
            _code.AppendLine("        _context.VulkanApi.UnmapMemory(_context.LogicalDevice, memory);");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    public void ReadData<T>(DeviceMemory memory, Span<T> outData, ulong size) where T : unmanaged");
            _code.AppendLine("    {");
            _code.AppendLine("        void* mappedData;");
            _code.AppendLine("        _context.VulkanApi.MapMemory(_context.LogicalDevice, memory, 0, size, 0, &mappedData).Assert();");

            _code.AppendLine("        fixed (T* pOut = outData) Unsafe.CopyBlock(pOut, mappedData, (uint)size);");
            _code.AppendLine("        _context.VulkanApi.UnmapMemory(_context.LogicalDevice, memory);");
            _code.AppendLine("    }");
            _code.AppendLine();

            // 5. Отдельный строго типизированный Dispatch под каждый кернел
            foreach (var kernel in ShaderReader.Kernels.Values)
            {
                _code.AppendLine($"    ///  Запуск ядра {kernel.Name} ");
                _code.AppendLine($"    public void Dispatch{kernel.Name}(uint totalElementsX, uint totalElementsY = 1, uint totalElementsZ = 1)");
                _code.AppendLine("    {");
                _code.AppendLine("        var vk = _context.VulkanApi;");
                _code.AppendLine($"        uint groupCountX = (totalElementsX + {kernel.LocalSizeX} - 1) / {kernel.LocalSizeX};");
                _code.AppendLine($"        uint groupCountY = (totalElementsY + {kernel.LocalSizeY} - 1) / {kernel.LocalSizeY};");
                _code.AppendLine($"        uint groupCountZ = (totalElementsZ + {kernel.LocalSizeZ} - 1) / {kernel.LocalSizeZ};");
                _code.AppendLine();
                _code.AppendLine("        CommandBufferAllocateInfo allocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _context.CommandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };");
                _code.AppendLine("        CommandBuffer cmd;");
                _code.AppendLine("        vk.AllocateCommandBuffers(_context.LogicalDevice, &allocInfo, &cmd).Assert();");
                _code.AppendLine();
                _code.AppendLine("        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };");
                _code.AppendLine("        vk.BeginCommandBuffer(cmd, &beginInfo).Assert();");
                _code.AppendLine($"        vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, pipeline{kernel.Name});");
                _code.AppendLine("        fixed (DescriptorSet* pSet = &_set) vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _layout, 0, 1, pSet, 0, null);");
                _code.AppendLine("        vk.CmdDispatch(cmd, groupCountX, groupCountY, groupCountZ);");
                _code.AppendLine("        vk.EndCommandBuffer(cmd).Assert();");
                _code.AppendLine();
                _code.AppendLine("        SubmitInfo submitInfo = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cmd };");
                _code.AppendLine("        vk.QueueSubmit(_context.CommandQueue, 1, &submitInfo, default).Assert();");
                _code.AppendLine("        vk.QueueWaitIdle(_context.CommandQueue).Assert();");
                _code.AppendLine("        vk.FreeCommandBuffers(_context.LogicalDevice, _context.CommandPool, 1, &cmd);");
                _code.AppendLine("    }");
                _code.AppendLine();
            }

            // 6. Служебные методы создания буферов внутри класса
            _code.AppendLine("    private (Buffer, DeviceMemory) CreateBufferInternal(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties)");
            _code.AppendLine("    {");
            _code.AppendLine("        var vk = _context.VulkanApi;");
            _code.AppendLine("        var bufferCreateInfo = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = size, Usage = usage, SharingMode = SharingMode.Exclusive };");
            _code.AppendLine("        Buffer buffer; vk.CreateBuffer(_context.LogicalDevice, &bufferCreateInfo, null, &buffer).Assert();");
            _code.AppendLine("        MemoryRequirements memReq; vk.GetBufferMemoryRequirements(_context.LogicalDevice, buffer, out memReq);");
            _code.AppendLine("        var allocInfo = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = memReq.Size, MemoryTypeIndex = FindMemoryType(memReq.MemoryTypeBits, properties) };");
            _code.AppendLine("        DeviceMemory memory; vk.AllocateMemory(_context.LogicalDevice, &allocInfo, null, &memory).Assert();");
            _code.AppendLine("        vk.BindBufferMemory(_context.LogicalDevice, buffer, memory, 0).Assert();");
            _code.AppendLine("        return (buffer, memory);");
            _code.AppendLine("    }");
            _code.AppendLine();
            _code.AppendLine("    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)");
            _code.AppendLine("    {");
            _code.AppendLine("        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(_context.SelectedGPU, out var memProperties);");
            _code.AppendLine("        for (int i = 0; i < memProperties.MemoryTypeCount; i++)");
            _code.AppendLine("            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties) return (uint)i;");
            _code.AppendLine("""               throw new Exception("Memory type not found");"""); // Правильное экранирование кавычек
            _code.AppendLine("    }");
            _code.AppendLine();

            // 7. Метод Dispose для очистки всех пайплайнов и буферов
            _code.AppendLine("    public void Dispose()");
            _code.AppendLine("    {");
            _code.AppendLine("        var vk = _context.VulkanApi;");
            _code.AppendLine("        var device = _context.LogicalDevice;");
            foreach (var buffer in ShaderReader.Buffers.Values)
            {
                _code.AppendLine($"        vk.DestroyBuffer(device, {buffer.Name}, null);");
                _code.AppendLine($"        vk.FreeMemory(device, {buffer.Name}Memory, null);");
            }
            foreach (var kernel in ShaderReader.Kernels.Values)
            {
                _code.AppendLine($"        vk.DestroyPipeline(device, pipeline{kernel.Name}, null);");
            }
            _code.AppendLine("        vk.DestroyShaderModule(device, _shaderModule, null);");
            _code.AppendLine("        vk.DestroyDescriptorPool(device, _pool, null);");
            _code.AppendLine("        vk.DestroyPipelineLayout(device, _layout, null);");
            _code.AppendLine("    }");
            _code.AppendLine("}");
            return _code.ToString();
        }
    } 
}