namespace ShaderPipelineBaker.Infrastructure.ReadingFiles
{
    using ShadersData.Metadata;
    using ShadersData.Registry;

    internal class BuildReadResult
    {
        private string configPath;

        internal BuildReadResult(string configPath)
            => this.configPath = configPath;
        
        internal void Build()
        {
            BuildConfigData();
            RegistryConfigData.ForEach(BuildShaderData); 
        }

        #region ConfigData
        internal void BuildConfigData()
        {
            ConfigReader configReader = new(configPath);
            RegistryConfigData.ConfigDataList = configReader.Load();
        }
        #endregion

        #region ShaderData
        internal void BuildShaderData(ConfigData configData)
        {

        }
        #endregion
    }
}