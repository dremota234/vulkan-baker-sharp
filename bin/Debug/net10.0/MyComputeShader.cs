using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

public unsafe class MyComputeShader : IDisposable
{
    private readonly VulkanContext _context;
    private Pipeline _pipeline_main;

    private PipelineLayout _layout;
    private DescriptorPool _pool;
    private DescriptorSet _set;
    private ShaderModule _shaderModule;

    public Buffer InputBuffer { get; private set; }
    public DeviceMemory InputBufferMemory { get; private set; }
    public ulong InputBufferSize { get; private set; }

    public Buffer OutputBuffer { get; private set; }
    public DeviceMemory OutputBufferMemory { get; private set; }
    public ulong OutputBufferSize { get; private set; }

    public Buffer gl_GlobalInvocationID { get; private set; }
    public DeviceMemory gl_GlobalInvocationIDMemory { get; private set; }
    public ulong gl_GlobalInvocationIDSize { get; private set; }

    public MyComputeShader(VulkanContext context, uint InputBufferElementCount, uint OutputBufferElementCount, uint gl_GlobalInvocationIDElementCount)
    {
        _context = context;
        var vk = context.VulkanApi;
        var device = context.LogicalDevice;

        byte[] shaderBytes = new byte[] { 3, 2, 35, 7, 0, 5, 1, 0, 0, 0, 14, 0, 26, 0, 0, 0, 0, 0, 0, 0, 17, 0, 2, 0, 1, 0, 0, 0, 14, 0, 3, 0, 0, 0, 0, 0, 1, 0, 0, 0, 15, 0, 8, 0, 5, 0, 0, 0, 1, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0, 16, 0, 6, 0, 1, 0, 0, 0, 17, 0, 0, 0, 64, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 3, 0, 3, 0, 5, 0, 0, 0, 88, 2, 0, 0, 5, 0, 9, 0, 5, 0, 0, 0, 116, 121, 112, 101, 46, 83, 116, 114, 117, 99, 116, 117, 114, 101, 100, 66, 117, 102, 102, 101, 114, 46, 105, 110, 116, 0, 0, 0, 5, 0, 5, 0, 3, 0, 0, 0, 73, 110, 112, 117, 116, 66, 117, 102, 102, 101, 114, 0, 5, 0, 9, 0, 6, 0, 0, 0, 116, 121, 112, 101, 46, 82, 87, 83, 116, 114, 117, 99, 116, 117, 114, 101, 100, 66, 117, 102, 102, 101, 114, 46, 105, 110, 116, 0, 5, 0, 6, 0, 4, 0, 0, 0, 79, 117, 116, 112, 117, 116, 66, 117, 102, 102, 101, 114, 0, 0, 0, 0, 5, 0, 4, 0, 1, 0, 0, 0, 109, 97, 105, 110, 0, 0, 0, 0, 71, 0, 4, 0, 2, 0, 0, 0, 11, 0, 0, 0, 28, 0, 0, 0, 71, 0, 4, 0, 3, 0, 0, 0, 34, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 3, 0, 0, 0, 33, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 4, 0, 0, 0, 34, 0, 0, 0, 0, 0, 0, 0, 71, 0, 4, 0, 4, 0, 0, 0, 33, 0, 0, 0, 1, 0, 0, 0, 71, 0, 4, 0, 7, 0, 0, 0, 6, 0, 0, 0, 4, 0, 0, 0, 72, 0, 5, 0, 5, 0, 0, 0, 0, 0, 0, 0, 35, 0, 0, 0, 0, 0, 0, 0, 72, 0, 4, 0, 5, 0, 0, 0, 0, 0, 0, 0, 24, 0, 0, 0, 71, 0, 3, 0, 5, 0, 0, 0, 2, 0, 0, 0, 72, 0, 5, 0, 6, 0, 0, 0, 0, 0, 0, 0, 35, 0, 0, 0, 0, 0, 0, 0, 71, 0, 3, 0, 6, 0, 0, 0, 2, 0, 0, 0, 21, 0, 4, 0, 8, 0, 0, 0, 32, 0, 0, 0, 1, 0, 0, 0, 43, 0, 4, 0, 8, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 0, 43, 0, 4, 0, 8, 0, 0, 0, 10, 0, 0, 0, 2, 0, 0, 0, 29, 0, 3, 0, 7, 0, 0, 0, 8, 0, 0, 0, 30, 0, 3, 0, 5, 0, 0, 0, 7, 0, 0, 0, 32, 0, 4, 0, 11, 0, 0, 0, 12, 0, 0, 0, 5, 0, 0, 0, 30, 0, 3, 0, 6, 0, 0, 0, 7, 0, 0, 0, 32, 0, 4, 0, 12, 0, 0, 0, 12, 0, 0, 0, 6, 0, 0, 0, 21, 0, 4, 0, 13, 0, 0, 0, 32, 0, 0, 0, 0, 0, 0, 0, 23, 0, 4, 0, 14, 0, 0, 0, 13, 0, 0, 0, 3, 0, 0, 0, 32, 0, 4, 0, 15, 0, 0, 0, 1, 0, 0, 0, 14, 0, 0, 0, 19, 0, 2, 0, 16, 0, 0, 0, 33, 0, 3, 0, 17, 0, 0, 0, 16, 0, 0, 0, 32, 0, 4, 0, 18, 0, 0, 0, 12, 0, 0, 0, 8, 0, 0, 0, 59, 0, 4, 0, 11, 0, 0, 0, 3, 0, 0, 0, 12, 0, 0, 0, 59, 0, 4, 0, 12, 0, 0, 0, 4, 0, 0, 0, 12, 0, 0, 0, 59, 0, 4, 0, 15, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 54, 0, 5, 0, 16, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 17, 0, 0, 0, 248, 0, 2, 0, 19, 0, 0, 0, 61, 0, 4, 0, 14, 0, 0, 0, 20, 0, 0, 0, 2, 0, 0, 0, 81, 0, 5, 0, 13, 0, 0, 0, 21, 0, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0, 65, 0, 6, 0, 18, 0, 0, 0, 22, 0, 0, 0, 3, 0, 0, 0, 9, 0, 0, 0, 21, 0, 0, 0, 61, 0, 4, 0, 8, 0, 0, 0, 23, 0, 0, 0, 22, 0, 0, 0, 132, 0, 5, 0, 8, 0, 0, 0, 24, 0, 0, 0, 23, 0, 0, 0, 10, 0, 0, 0, 65, 0, 6, 0, 18, 0, 0, 0, 25, 0, 0, 0, 4, 0, 0, 0, 9, 0, 0, 0, 21, 0, 0, 0, 62, 0, 3, 0, 25, 0, 0, 0, 24, 0, 0, 0, 253, 0, 1, 0, 56, 0, 1, 0 };

        InputBufferSize = InputBufferElementCount * 0UL;
        (Buffer InputBuffer, DeviceMemory InputBufferMemory) = CreateBufferInternal(InputBufferSize, BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        this.InputBuffer = InputBuffer;
        this.InputBufferMemory = InputBufferMemory;

        OutputBufferSize = OutputBufferElementCount * 0UL;
        (Buffer OutputBuffer, DeviceMemory OutputBufferMemory) = CreateBufferInternal(OutputBufferSize, BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        this.OutputBuffer = OutputBuffer;
        this.OutputBufferMemory = OutputBufferMemory;

        gl_GlobalInvocationIDSize = gl_GlobalInvocationIDElementCount * 0UL;
        (Buffer gl_GlobalInvocationID, DeviceMemory gl_GlobalInvocationIDMemory) = CreateBufferInternal(gl_GlobalInvocationIDSize, BufferUsageFlags.StorageBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        this.gl_GlobalInvocationID = gl_GlobalInvocationID;
        this.gl_GlobalInvocationIDMemory = gl_GlobalInvocationIDMemory;

        int bindingCount = 3;
        DescriptorSetLayoutBinding* bindings = stackalloc DescriptorSetLayoutBinding[bindingCount];
        bindings[0] = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit };
        bindings[1] = new DescriptorSetLayoutBinding { Binding = 1, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit };
        bindings[2] = new DescriptorSetLayoutBinding { Binding = , DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit };

        var layoutCreateInfo = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = (uint)bindingCount, PBindings = bindings };
        DescriptorSetLayout setLayout;
        vk.CreateDescriptorSetLayout(device, &layoutCreateInfo, null, &setLayout).Assert();

        var pipelineLayoutCreateInfo = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = &setLayout };
        vk.CreatePipelineLayout(device, &pipelineLayoutCreateInfo, null, out _layout).Assert();

        var poolSize = new DescriptorPoolSize { Type = DescriptorType.StorageBuffer, DescriptorCount = (uint)bindingCount };
        var poolCreateInfo = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 1, PoolSizeCount = 1, PPoolSizes = &poolSize };
        vk.CreateDescriptorPool(device, &poolCreateInfo, null, out _pool).Assert();

        var allocInfo = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _pool, DescriptorSetCount = 1, PSetLayouts = &setLayout };
        vk.AllocateDescriptorSets(device, &allocInfo, out _set).Assert();

        WriteDescriptorSet* writeSets = stackalloc WriteDescriptorSet[bindingCount];
        DescriptorBufferInfo* bufferInfos = stackalloc DescriptorBufferInfo[bindingCount];
        bufferInfos[0] = new DescriptorBufferInfo { Buffer = this.InputBuffer, Offset = 0, Range = this.InputBufferSize };
        writeSets[0] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _set, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &bufferInfos[0] };
        bufferInfos[1] = new DescriptorBufferInfo { Buffer = this.OutputBuffer, Offset = 0, Range = this.OutputBufferSize };
        writeSets[1] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _set, DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &bufferInfos[1] };
        bufferInfos[2] = new DescriptorBufferInfo { Buffer = this.gl_GlobalInvocationID, Offset = 0, Range = this.gl_GlobalInvocationIDSize };
        writeSets[2] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _set, DstBinding = , DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &bufferInfos[2] };
        vk.UpdateDescriptorSets(device, (uint)bindingCount, writeSets, 0, null);
        vk.DestroyDescriptorSetLayout(device, setLayout, null);

        fixed (byte* pCode = shaderBytes)
        {
            var moduleCreateInfo = new ShaderModuleCreateInfo { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)shaderBytes.Length, PCode = (uint*)pCode };
            vk.CreateShaderModule(device, &moduleCreateInfo, null, out _shaderModule).Assert();
        }

        var entryPointStr_main = Marshal.StringToHGlobalAnsi("main");
        var stageCreateInfo_main = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = _shaderModule, PName = (byte*)entryPointStr_main };
        var pipelineCreateInfo_main = new ComputePipelineCreateInfo { SType = StructureType.ComputePipelineCreateInfo, Stage = stageCreateInfo_main, Layout = _layout };
        vk.CreateComputePipelines(device, default, 1, &pipelineCreateInfo_main, null, out _pipeline_main).Assert();
        Marshal.FreeHGlobal(entryPointStr_main);

    }

    public void WriteData<T>(DeviceMemory memory, ReadOnlySpan<T> data, ulong size) where T : unmanaged
    {
        void* mappedData;
        _context.VulkanApi.MapMemory(_context.LogicalDevice, memory, 0, size, 0, &mappedData).Assert();
        fixed (T* pData = data) Unsafe.CopyBlock(mappedData, pData, (uint)size);
        _context.VulkanApi.UnmapMemory(_context.LogicalDevice, memory);
    }

    public void ReadData<T>(DeviceMemory memory, Span<T> outData, ulong size) where T : unmanaged
    {
        void* mappedData;
        _context.VulkanApi.MapMemory(_context.LogicalDevice, memory, 0, size, 0, &mappedData).Assert();
        fixed (T* pOut = outData) Unsafe.CopyBlock(pOut, mappedData, (uint)size);
        _context.VulkanApi.UnmapMemory(_context.LogicalDevice, memory);
    }

    ///  Запуск ядра main 
    public void Dispatchmain(uint totalElementsX, uint totalElementsY = 1, uint totalElementsZ = 1)
    {
        var vk = _context.VulkanApi;
        uint groupCountX = (totalElementsX + 64 - 1) / 64;
        uint groupCountY = (totalElementsY + 1 - 1) / 1;
        uint groupCountZ = (totalElementsZ + 1 - 1) / 1;

        CommandBufferAllocateInfo allocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _context.CommandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        CommandBuffer cmd;
        vk.AllocateCommandBuffers(_context.LogicalDevice, &allocInfo, &cmd).Assert();

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        vk.BeginCommandBuffer(cmd, &beginInfo).Assert();
        vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, pipelinemain);
        fixed (DescriptorSet* pSet = &_set) vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, _layout, 0, 1, pSet, 0, null);
        vk.CmdDispatch(cmd, groupCountX, groupCountY, groupCountZ);
        vk.EndCommandBuffer(cmd).Assert();

        SubmitInfo submitInfo = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cmd };
        vk.QueueSubmit(_context.CommandQueue, 1, &submitInfo, default).Assert();
        vk.QueueWaitIdle(_context.CommandQueue).Assert();
        vk.FreeCommandBuffers(_context.LogicalDevice, _context.CommandPool, 1, &cmd);
    }

    private (Buffer, DeviceMemory) CreateBufferInternal(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties)
    {
        var vk = _context.VulkanApi;
        var bufferCreateInfo = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = size, Usage = usage, SharingMode = SharingMode.Exclusive };
        Buffer buffer; vk.CreateBuffer(_context.LogicalDevice, &bufferCreateInfo, null, &buffer).Assert();
        MemoryRequirements memReq; vk.GetBufferMemoryRequirements(_context.LogicalDevice, buffer, out memReq);
        var allocInfo = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = memReq.Size, MemoryTypeIndex = FindMemoryType(memReq.MemoryTypeBits, properties) };
        DeviceMemory memory; vk.AllocateMemory(_context.LogicalDevice, &allocInfo, null, &memory).Assert();
        vk.BindBufferMemory(_context.LogicalDevice, buffer, memory, 0).Assert();
        return (buffer, memory);
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(_context.SelectedGPU, out var memProperties);
        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties) return (uint)i;
               throw new Exception("Memory type not found");
    }

    public void Dispose()
    {
        var vk = _context.VulkanApi;
        var device = _context.LogicalDevice;
        vk.DestroyBuffer(device, InputBuffer, null);
        vk.FreeMemory(device, InputBufferMemory, null);
        vk.DestroyBuffer(device, OutputBuffer, null);
        vk.FreeMemory(device, OutputBufferMemory, null);
        vk.DestroyBuffer(device, gl_GlobalInvocationID, null);
        vk.FreeMemory(device, gl_GlobalInvocationIDMemory, null);
        vk.DestroyPipeline(device, pipelinemain, null);
        vk.DestroyShaderModule(device, _shaderModule, null);
        vk.DestroyDescriptorPool(device, _pool, null);
        vk.DestroyPipelineLayout(device, _layout, null);
    }
}
