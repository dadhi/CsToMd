# CsToMd

- [CsToMd](#cstomd)
  - [Overview](#overview)
  - [Visual Studio extension](#visual-studio-extension)
    - [How to use](#how-to-use)
  - [Dotnet CLI tool](#dotnet-cli-tool)
    - [Ad-hoc document generation](#ad-hoc-document-generation)
    - [Build integration](#build-integration)

## Overview

[![NuGet Badge](https://buildstats.info/nuget/dotnet-cstomd)](https://www.nuget.org/packages/dotnet-cstomd)

The [dotnet CLI tool](https://www.nuget.org/packages/dotnet-cstomd) and [Visual Studio extension](https://marketplace.visualstudio.com/items?itemName=dadhi.cstomd123) to generate the [Markdown](https://guides.github.com/features/mastering-markdown) documentation file from the C# file.

**The idea** is to have a normal C# .cs file with the special comments `/*md`, `md*/`, and `//md` which will be stripped when converting the file into the respective Markdown .md file. There are couple of additional features but this is basically it. 

Now you have **the documentation always up-to-date with the runnable samples** in the normal .NET Test library project with NUnit, XUnit, etc.

You may check the DryIoc [documentation project](https://github.com/dadhi/DryIoc/tree/master/docs/DryIoc.Docs) for the real-world case example.

The additional features in v1.2.0:

- Converting the section outlined with `//md{` and `//md}` comments into the [collapsed markdown details](https://gist.github.com/pierrejoubert73/902cc94d79424356a8d20be2b382e1ab).
- The optional `cstomd.config` file in the folder with the lines starters to be removed completely from the generated documentation file.


## Visual Studio extension

This extension for Visual Studio 2019+ contains the CustomTool File Generator.

When applied to the C# source file it looks like this:

![VS file properties](screen1.png)


The generated result:

![VS result](screen2.png)


### How to use

- Install [the extension](https://marketplace.visualstudio.com/items?itemName=dadhi.cstomd123) directly from the markerplace in Visual Studio or download the extension vsix file from the [release page](https://github.com/dadhi/CsToMd/releases).
- In properties of your .cs file set the `CustomTool` property to `CsToMd`.
- Save the .cs file
- Check the generated .md file under the .cs file in Solution Explorer


## Dotnet CLI tool

The [dotnet-cstomd](https://www.nuget.org/packages/dotnet-cstomd) is a [dotnet CLI tool](https://docs.microsoft.com/en-us/dotnet/core/tools/) providing the same functionality as a Visual Studio extension plus it may be called from the command line and from the MSBuild scripts (**enabling the document generation in the build pipeline**).

I addition the dotnet tool enables the documentation development in the **Visual Studio Code**.


![VSCode usage](screen3.png)


### Ad-hoc document generation

- Install the dotnet-cstomd globally from the nuget, e.g. in the shell of your choice `dotnet tool install --global dotnet-cstomd --version 1.2.1`. Now you can invoke `cstomd MyClass.cs` directly and get the `MyClass.md` output.

### Build integration

  * Switch to your project: `cd path\to\MyProject`
  * Add the tool manifest file: `dotnet new tool-manifest`
  * Install the tool: `dotnet tool install dotnet-cstomd --version 1.2.1` (the manifest file will be updated and later used for restore)
  * Add the section to your project:

    ```xml
    <ItemGroup>
        <DocFile Include="**\*.cs" />
    </ItemGroup>

    <Target Name="MdGenerate" BeforeTargets="BeforeBuild">
        <Exec WorkingDirectory="$(ProjectDir)" Command="dotnet cstomd %(DocFile.Identity)" />
    </Target>
    ```
    You may check the DryIoc [documentation project file](https://github.com/dadhi/DryIoc/blob/6f466ee1b4fde548c7211ecb0a54655011f69e57/docs/DryIoc.Docs/DryIoc.Docs.csproj#L26) for the real-world case example.

  * You may run the document generation target without the rest of the build:
   ```
    dotnet msbuild -target:MdGenerate path\to\MyProject\MyProject.csproj
   ```
   You may create a helper shell script `build_the_docs` with the command above.

  
  Here is the [MS tutorial](https://docs.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use#:~:text=Create%20a%20manifest%20file,-To%20install%20a&text=The%20output%20indicates%20successful%20creation%20of%20the%20file.&text=The%20template%20%22Dotnet%20local%20tool%20manifest%20file%22%20was%20created%20successfully.&text=The%20tools%20listed%20in%20a,the%20one%20that%20contains%20the%20.) for installing and using the local tools.
 

