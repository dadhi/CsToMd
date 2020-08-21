/*md
# Dotnet CLI Tool to convert from .cs to .md

> Hmm why? :smirk:

<details>
<summary><strong>Click to expand the reason</strong></summary>

Because it is

- still a valid C# `code` file,
    * a nice markdown documentation,
        + and [all around awesome!](#end-of-the-document)

</details>

<details>
<summary><code>using (...)</code></summary>

```cs md*/
using System;
using System.IO;
using CsToMd;
using static System.Console;
/*md
```

</details>

```cs md*/
namespace CsToMdDotnetTool
{
    class Program
    {
        const string _expectedArgs = "The tool expects a single argument which is the path to the `.cs` file";
        const string _outputMarkdownFileExt = ".md";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                WriteLine("~~~");
                WriteLine($"help: {_expectedArgs}.");
                WriteLine("~~~");
                return;
            }

            if (args.Length != 1)
                throw new ArgumentException($"error: {_expectedArgs}, but instead got the {args.Length} arguments");

            var csFilePath = args[0];
            if (!File.Exists(csFilePath))
                throw new ArgumentException($"error: {_expectedArgs}, but instead got non existing file '{csFilePath}'");

            var lines = File.ReadAllLines(csFilePath);

            var mdStringBuilder = CommentStripper.StripMdComments(lines);

            var mdFilePath = Path.ChangeExtension(csFilePath, _outputMarkdownFileExt);

            File.WriteAllText(mdFilePath, mdStringBuilder.ToString());
        }
    }
}
/*md
```

## End of the document

 ~~hand waving~~ :heart:

md*/