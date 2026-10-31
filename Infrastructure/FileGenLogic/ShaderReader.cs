using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ShaderPipelineBaker.Infrastructure.FileGenLogic
{
    internal partial class ShaderReader
    {
        private Appsettings_Data data;

        internal ShaderReader(Appsettings_Data data) => this.data = data;

        internal void CompileAndDisassemble()
        {
            var compileInfo = new ProcessStartInfo
            {
                FileName = "dxc.exe",
                Arguments = $"-T cs_6_0 -E {_entryPoint} -spirv -fspv-target-env=vulkan1.2 -Fo \"{data.spvPath}\" \"{data.hlslPath}\"",   ///////////////////////////////////////////////
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var compileProcess = Process.Start(compileInfo))
            {
                string errors = compileProcess!.StandardError.ReadToEnd();
                compileProcess.WaitForExit();

                if (compileProcess.ExitCode != 0)
                    throw new Exception($"DXC Compilation Error:\n{errors}");
                
            }

            var disInfo = new ProcessStartInfo
            {
                FileName = "spirv-dis.exe",
                Arguments = $"\"{_spvPath}\" -o \"{_txtPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var disProcess = Process.Start(disInfo))
            {
                string errors = disProcess!.StandardError.ReadToEnd();
                disProcess.WaitForExit();

                if (disProcess.ExitCode != 0)
                    throw new Exception($"SPIRV-DIS Disassembly Error:\n{errors}");
                
            }

            foreach (string line in File.ReadLines(_txtPath))
                ParseLine(line);
        }

        #region Reading line logic

        #region Data Models
        internal class KernelInfo
        {
            internal string ExecModel { get; set; } = null!;
            internal string Name { get; set; } = null!;
            internal string Interface { get; set; } = null!;
            internal string LocalSizeX { get; set; } = null!;
            internal string LocalSizeY { get; set; } = null!;
            internal string LocalSizeZ { get; set; } = null!;
        }

        public class BufferInfo
        {
            public string Name { get; set; } = null!;
            public string TypeName { get; set; } = null!;
            public string DescriptorSet { get; set; } = null!;
            public string Binding { get; set; } = null!;
            public bool IsWritable { get; set; } = true;

            public string ElementType { get; set; } = null!;
            public int ElementBitWidth { get; set; }
            public int ArrayStride { get; set; }
        }
        #endregion

        #region Parsing Results
        internal static readonly Dictionary<string, KernelInfo> Kernels = new();
        internal static readonly Dictionary<string, BufferInfo> Buffers = new();
        private static readonly HashSet<string> NonWritableTypes = new(); // для связи между отдельными строками файла.
        internal static readonly Dictionary<string, (string BaseType, int Width)> RawTypes = new();
        internal static readonly Dictionary<string, int> RawStrides = new();
        #endregion

        private delegate void MatchProcessor(Match match);

        
        /// <summary>
        /// Разбирает одну строку SPIR-V ассемблера и дозаполняет словари Kernels и Buffers
        /// </summary>
        /// <param name="line">Текущая строка из SPIR-V файла</param>
        internal static void ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            foreach (var (regex, processor) in Parsers)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    processor(match);
                    break;
                }
            }
        }

        #region Individual Regex Parsers

        #region EntryPoint
        private static readonly Regex EntryPointRegex = new(@"OpEntryPoint\s+(?<ExecModel>\w+)\s+%(?<FuncID>\w+)\s+""(?<Name>\w+)""\s+(?<Interface>.+)", RegexOptions.Compiled);

        /// <summary>
        /// Работа с данными EntryPoint
        /// </summary>
        /// <param name="match"></param>
        private static void ProcessEntryPoint(Match match)
        {
            if (!match.Success) return;

            string funcId = match.Groups["FuncID"].Value;

            if (!Kernels.ContainsKey(funcId))
                Kernels[funcId] = new KernelInfo();

            Kernels[funcId].ExecModel = match.Groups["ExecModel"].Value;
            Kernels[funcId].Name = match.Groups["Name"].Value;
            Kernels[funcId].Interface = match.Groups["Interface"].Value.Trim();
        }
        #endregion

        #region LocalSize
        private static readonly Regex LocalSizeRegex = new(@"OpExecutionMode\s+%(?<FuncID>\w+)\s+LocalSize\s+(?<X>\d+)\s+(?<Y>\d+)\s+(?<Z>\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Работа с данными LocalSize
        /// </summary>
        /// <param name="match"></param>
        private static void ProcessLocalSize(Match match)
        {
            if (!match.Success) return;

            string funcId = match.Groups["FuncID"].Value;

            if (!Kernels.ContainsKey(funcId))
                Kernels[funcId] = new KernelInfo();

            Kernels[funcId].LocalSizeX = match.Groups["X"].Value;
            Kernels[funcId].LocalSizeY = match.Groups["Y"].Value;
            Kernels[funcId].LocalSizeZ = match.Groups["Z"].Value;
        }
        #endregion

        #region Decorate
        private static readonly Regex DecorateRegex = new(@"OpDecorate\s+%(?<Buffer>\w+)\s+(?<Prop>DescriptorSet|Binding)\s+(?<Value>\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Работа с данными Decorate
        /// </summary>
        /// <param name="match"></param>
        private static void ProcessDecorate(Match match)
        {
            if (!match.Success) return;

            string bufferName = match.Groups["Buffer"].Value;
            string property = match.Groups["Prop"].Value;
            string value = match.Groups["Value"].Value;

            if (!Buffers.ContainsKey(bufferName))
            {
                Buffers[bufferName] = new BufferInfo { Name = bufferName };
            }

            if (property == "DescriptorSet")
            {
                Buffers[bufferName].DescriptorSet = value;
            }
            else if (property == "Binding")
            {
                Buffers[bufferName].Binding = value;
            }
        }
        #endregion

        #region MemberDecorate
        private static readonly Regex MemberDecorateRegex = new(@"OpMemberDecorate\s+%(?<Type>\w+)\s+\d+\s+NonWritable", RegexOptions.Compiled);

        private static void ProcessMemberDecorate(Match match)
        {
            if (!match.Success) return;

            string typeName = match.Groups["Type"].Value;
            NonWritableTypes.Add(typeName);

            foreach (var buffer in Buffers.Values)
            {
                if (buffer.TypeName == typeName)
                {
                    buffer.IsWritable = false;
                }
            }
        }
        #endregion

        #region Variable
        private static readonly Regex VariableRegex = new(@"%(?<Buffer>\w+)\s*=\s*OpVariable\s+%(?<Type>\w+)", RegexOptions.Compiled);

        private static void ProcessVariable(Match match)
        {
            if (!match.Success) return;

            string bufferName = match.Groups["Buffer"].Value;
            string typeName = match.Groups["Type"].Value;

            if (!Buffers.ContainsKey(bufferName))
                Buffers[bufferName] = new BufferInfo { Name = bufferName };

            BufferInfo buffer = Buffers[bufferName];
            buffer.TypeName = typeName;

            foreach (var rawType in RawTypes)
            {
                if (typeName.Contains(rawType.Key))
                {
                    buffer.ElementType = rawType.Value.BaseType;
                    buffer.ElementBitWidth = rawType.Value.Width;
                }
            }

            foreach (var rawStride in RawStrides)
            {
                if (typeName.Contains(rawStride.Key))
                {
                    buffer.ArrayStride = rawStride.Value;
                }
            }
        }
        #endregion

        #region TypeInt
        private static readonly Regex TypeIntRegex = new(@"%(?<Type>\w+)\s*=\s*OpTypeInt\s+(?<Width>\d+)", RegexOptions.Compiled);

        private static void ProcessTypeInt(Match match)
        {
            if (!match.Success) return;
            RawTypes[match.Groups["Type"].Value] = ("int", int.Parse(match.Groups["Width"].Value));
        }
        #endregion

        #region ArrayStride
        private static readonly Regex ArrayStrideRegex = new(@"OpDecorate\s+%(?<Target>\w+)\s+ArrayStride\s+(?<Stride>\d+)", RegexOptions.Compiled);

        private static void ProcessArrayStride(Match match)
        {
            if (!match.Success) return;
            RawStrides[match.Groups["Target"].Value] = int.Parse(match.Groups["Stride"].Value);
        }
        #endregion

        #endregion
        private static readonly (Regex RegexInstance, MatchProcessor Processor)[] Parsers = new (Regex, MatchProcessor)[]
        {
            (EntryPointRegex!,     ProcessEntryPoint),
            (LocalSizeRegex!,      ProcessLocalSize),
            (DecorateRegex!,       ProcessDecorate),
            (MemberDecorateRegex!, ProcessMemberDecorate),
            (VariableRegex!,       ProcessVariable),
            (TypeIntRegex!,        ProcessTypeInt),
            (ArrayStrideRegex!,    ProcessArrayStride)
        };
        #endregion
    }
}
