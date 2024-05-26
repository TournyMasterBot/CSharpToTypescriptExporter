# CSharpToTypescriptExporter
A simple project that takes in an input folder path with C# classes and interfaces and a desired output folder path to export Typescript (.ts) files

# As Is
This project is provided AS-IS, with no warranty, guarantee, or assurance that this will do what you want it to do.

I use this project for personal use, and it does what I need. You are welcome to modify it in any way that suits your needs.

# Quickstart

## Compiling
As of this writing, this code builds under dotnet 8, using Visual Studio 2022.

## Using the code
* Import the `.dll` file as a project reference into your C# project 
* Then tag classes you wish to export as inheriting from `ITypescriptClassExportable`, or interfaces you wish to export with `ITypescriptInterfaceExportable`
* Finally, execute the `Export` method defined in the library.

**EXAMPLES**

> Basic usage
```
using CSharpToTypescriptExporter;

namespace Your.Namespace
{
    public class Program
    {
        static void Main(string[] args)
        {
            string[]? filterAssemblies = new string[] { @"C:\Path\To\Your\Desired\Project\Assembly" };
            string outputDirectory = @"C:\Your\Desired\Output\Folder\Generated\";

            var exporter = new TypeScriptExporter(outputDirectory: outputDirectory, filterAssemblies: filterAssemblies);
            exporter.ExportTypescriptFiles();

            Console.WriteLine("TypeScript files exported successfully.");
        }
    }
}
```

> Tagging an interface
```
using CSharpToTypescriptExporter;
using System.Text;

namespace Game.Models
{
    public interface IMapRepresentation: ITypescriptInterfaceExportable
    {
        public StringBuilder? TextRepresentation { get; set; }
    }
}

```

> Tagging a class
```
using CSharpToTypescriptExporter;
using Game.Models.LocationModels;
using Newtonsoft.Json;

namespace Game.Models.NavModels
{
    public class NavPath: ITypescriptClassExportable
    {
        /// <summary>
        /// Path required to get from location A to B
        /// </summary>
        [JsonProperty(PropertyName = "path")]
        public required HashSet<Coordinate> Path { get; set; }
    }
}
```

# Notes
* As of this writing, exported C# models will automatically resolve paths to custom objects and add the relevant import statements in TypeScript.
* Exported classes and interfaces will maintain their folder structure.
* By default, this will load from all loaded assemblies, but can be filtered to specified directories by using the `filterPaths` input string array.
* As of this writing, no attempt has been made to support the following:
	* Partial classes
* This project has not been tested on MacOS or Linux, but (should) work.

# Dependencies
As of this writing, the only package required from NuGet is 'Newtonsoft.Json' - this was a shortcut to support mapping C# class names to desired TS models.