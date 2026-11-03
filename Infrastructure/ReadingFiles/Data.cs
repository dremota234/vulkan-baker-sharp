namespace ShaderPipelineBaker.Infrastructure.ReadingFiles
{
    namespace ShaderMetadata
    {
        internal record ConfigData(
            string ShaderName,
            string HlslPath,
            string SpvPath,
            string SpvTextPath,
            string BinPath,
            string CsPath
        );

        internal record KernelInfo
        (
            string ExecModel,
            string Name,
            string Interface,
            string LocalSizeX,
            string LocalSizeY,
            string LocalSizeZ
        );

        internal record BufferInfo
        (
            string Name,
            string TypeName,
            string DescriptorSet,
            string Binding,
            bool IsWritable,

            string ElementType,
            int ElementBitWidth,
            int ArrayStride
        );

        internal record ShaderData
        (
            KernelInfo ShaderKernelSettings,
            BufferInfo[] ShaderBuffersSettings,
            ConfigData ShaderSaveSettings
        );
    }
}