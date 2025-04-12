//# This comment starting from the line in cstomd.config is removed from the output
/*md code:cs
# Dotnet CLI Tool to convert from .cs to .md


- [Dotnet CLI Tool to convert from .cs to .md](#dotnet-cli-tool-to-convert-from-cs-to-md)
  - [Why?](#why)
  - [The end](#the-end)


## Why?
*/
/*md{<strong>See for yourself ...</strong>
- Still a valid C# `code` file,
    * A nice markdown documentation,
        + And [all around awesome!](#end-of-the-document)
}*/

//md{ usings ...
using System;
using System.IO;
using CsToMd;
using static System.Console;
//md}

class Program
{
    const string _expectedArgs = @"The tool expects the first argument to be the path to the `.cs` file,

and the optional second argument to be the path to the config file (the default is './cstomd.config')
The config file should contain the starting lines to exclude from the generated output.

";

    const string _outputMarkdownFileExt = ".md";

    const string _defaultConfigFileName = "cstomd.config";

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            WriteLine("~~~");
            WriteLine($"help: {_expectedArgs}.");
            WriteLine("~~~");
            return;
        }

        if (args.Length > 2)
            throw new ArgumentException($@"error: {_expectedArgs} But instead got the {args.Length} arguments");

        var inputFilePath = args[0];
        if (!File.Exists(inputFilePath))
            throw new ArgumentException($"error: {_expectedArgs} But instead got nonexisting input file '{inputFilePath}'");

        string[] removeLineStartingWith = null;
        if (args.Length == 2)
        {
            var configFilePath = args[1];
            if (!File.Exists(configFilePath))
                throw new ArgumentException($"error: {_expectedArgs} But got the nonexistent config file '{configFilePath}'");
            removeLineStartingWith = File.ReadAllLines(configFilePath);
        }
        else if (File.Exists(_defaultConfigFileName))
            removeLineStartingWith = File.ReadAllLines(_defaultConfigFileName);

        var lines = File.ReadAllLines(inputFilePath);

        var mdStringBuilder = CommentStripper.StripMdComments(lines, removeLineStartingWith);

        var mdFilePath = Path.ChangeExtension(inputFilePath, _outputMarkdownFileExt);

        File.WriteAllText(mdFilePath, mdStringBuilder.ToString());
    }
}
/*md
## The end

~~hand waving~~ :heart:

md*/