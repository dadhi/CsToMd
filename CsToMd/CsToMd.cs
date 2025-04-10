using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using static System.Environment;

namespace CsToMd
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration(nameof(CsToMd),
        "Generates a Markdown .md file from the input C# .cs file removing the specially prefixed comments, e.g. `/*md`, `md*/`, `//md`",
        "2.1.0")]
    [Guid(PackageGuid)]
    [ComVisible(true)]
    [ProvideObject(typeof(CsToMd))]
    [CodeGeneratorRegistration(typeof(CsToMd), nameof(CsToMd), ContextGuidEmbraced, GeneratesDesignTimeSource = true)]
    public sealed class CsToMd : IVsSingleFileGenerator
    {
        public const string DefaultConfigFileName = "cstomd.config";

        public const string PackageGuid = "5a4dc0a7-5ae0-42a4-8d38-326644b59f10";
        public const string ContextGuidEmbraced = "{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}";

        /// <summary>Retrieves the file extension that is given to the output file name.</summary>
        /// <returns>If the method succeeds, it returns <see cref="F:Microsoft.VisualStudio.VSConstants.S_OK" />. If it fails, it returns an error code.</returns>
        /// <param name="pbstrDefaultExtension">[out, retval] Returns the file extension that is to be given to the output file name.
        /// The returned extension must include a leading period. </param>
        public int DefaultExtension(out string pbstrDefaultExtension)
        {
            pbstrDefaultExtension = ".md";
            return pbstrDefaultExtension.Length;
        }

        /// <summary>Executes the transformation and returns the newly generated output file, whenever a custom tool is loaded,
        /// or the input file is saved.</summary>
        /// <returns>If the method succeeds, it returns <see cref="F:Microsoft.VisualStudio.VSConstants.S_OK" />.
        /// If it fails, it returns an error code.</returns>
        /// <param name="wszInputFilePath">[in] The full path of the input file. May be null in future releases of Visual Studio,
        /// so generators should not rely on this value.</param>
        /// <param name="bstrInputFileContents">[in] The contents of the input file.
        /// This is either a UNICODE BSTR (if the input file is text) or a binary BSTR (if the input file is binary).
        /// If the input file is a text file, the project system automatically converts the BSTR to UNICODE.</param>
        /// <param name="wszDefaultNamespace">[in] This parameter is meaningful only for custom tools that generate code.
        /// It represents the namespace into which the generated code will be placed.
        /// If the parameter is not null and not empty, the custom tool can use the following syntax to enclose the generated code.
        /// ' Visual Basic Namespace [default namespace]... End Namespace// Visual C#namespace [default namespace] { ... }</param>
        /// <param name="rgbOutputFileContents">[out] Returns an array of bytes to be written to the generated file.
        /// You must include UNICODE or UTF-8 signature bytes in the returned byte array, as this is a raw stream.
        /// The memory for <paramref name="rgbOutputFileContents" /> must be allocated using the .NET Framework call, System.Runtime.InteropServices.AllocCoTaskMem,
        /// or the equivalent Win32 system call, CoTaskMemAlloc. The project system is responsible for freeing this memory.</param>
        /// <param name="pcbOutput">[out] Returns the count of bytes in the <paramref name="rgbOutputFileContents" /> array.</param>
        /// <param name="pGenerateProgress">[in] A reference to the <see cref="T:Microsoft.VisualStudio.Shell.Interop.IVsGeneratorProgress" />
        /// interface through which the generator can report its progress to the project system.</param>
        public int Generate(string wszInputFilePath, string bstrInputFileContents,
            string wszDefaultNamespace, IntPtr[] rgbOutputFileContents,
            out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            try
            {
                var inputLines = bstrInputFileContents.Split(new[] { NewLine }, StringSplitOptions.None);

                var inputDir = Path.GetDirectoryName(wszInputFilePath);
                // ReSharper disable once AssignNullToNotNullAttribute
                var defaultConfigFilePath = Path.Combine(inputDir, DefaultConfigFileName);

                string configReadError = null;
                string[] removeLineStartingWith = null;
                if (File.Exists(defaultConfigFilePath))
                {
                    try
                    {
                        removeLineStartingWith = File.ReadAllLines(defaultConfigFilePath);
                    }
                    catch (Exception ex)
                    {
                        configReadError = $"Unable to read '{defaultConfigFilePath}' with error: '{ex.Message}'";
                    }
                }

                var outputBuilder = CommentStripper.StripMdComments(inputLines, removeLineStartingWith);

                if (configReadError != null)
                {
                    outputBuilder.AppendLine("### There are errors while producing the markdown document file");
                    outputBuilder.AppendLine();
                    outputBuilder.AppendLine(configReadError);
                }

                var output = outputBuilder.ToString();
                var outputBytes = Encoding.UTF8.GetBytes(output);
                var outputByteCount = outputBytes.Length;
                rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(outputByteCount);
                Marshal.Copy(outputBytes, 0, rgbOutputFileContents[0], outputByteCount);

                pcbOutput = (uint)outputByteCount;
            }
            catch (Exception)
            {
                pcbOutput = 0;
            }

            return VSConstants.S_OK;
        }
    }
}
