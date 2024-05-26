using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using System.Text;

namespace CSharpToTypescriptExporter
{
    /// <summary>
    /// Exports c# files to typescript models. See: <a href="https://github.com/TournyMasterBot/CSharpToTypescriptExporter">https://github.com/TournyMasterBot/CSharpToTypescriptExporter</a> for more details.
    /// </summary>
    public class TypeScriptExporter
    {
        private string outputDirectory;
        private readonly string[]? filterAssemblies;

        /// <summary>
        /// Creates the Typescript Exporter with the specified parameters
        /// </summary>
        /// <param name="outputDirectory">Export location for generated typescript models</param>
        /// <param name="filterAssemblies">optional. When null, fetches all loaded assemblies. Otherwise, only scans assemblies specified</param>
        public TypeScriptExporter(string outputDirectory, string[]? filterAssemblies = null)
        {
            this.outputDirectory = outputDirectory;
            this.filterAssemblies = filterAssemblies;
        }

        /// <summary>
        /// Exports the generated typescript files to the location specified when initialized. This export preserves the file structure of the tagged items.
        /// This exporter is fairly naive, so you must ensure you have permissions to read and write to the specified locations
        /// </summary>
        public void ExportTypescriptFiles()
        {
            var classesToExport = GetExportableClasses();
            var interfacesToExport = GetExportableInterfaces();

            // Process classes
            foreach (var type in classesToExport)
            {
                var tsCode = GenerateTypescriptClassCode(type);
                if (tsCode == null)
                {
                    continue;
                }
                var filePath = GetOutputFilePath(type);
                WriteToFile(filePath, tsCode);
            }

            // Process interfaces
            foreach (var i in interfacesToExport)
            {
                var tsCode = GenerateInterfaceTypescriptCode(i);
                if (tsCode == null)
                {
                    continue;
                }
                var filePath = GetOutputFilePath(i);
                WriteToFile(filePath, tsCode);
            }
        }

        /// <summary>
        /// Fetches all classes within the specified assemblies
        /// </summary>
        /// <returns>A list of assemblies that contain tagged concrete classes</returns>
        private IEnumerable<Type> GetExportableClasses()
        {
            var assemblies = GetFilteredAssemblies();
            return assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(ITypescriptClassExportable).IsAssignableFrom(type) && !type.IsInterface && type.Name != "ITypescriptClassExportable");
        }

        /// <summary>
        /// Fetches all interfaces within the specified assemblies
        /// </summary>
        /// <returns>A list of assemblies that contain tagged interfaces</returns>
        private IEnumerable<Type> GetExportableInterfaces()
        {
            var assemblies = GetFilteredAssemblies();
            var exportableInterfaces = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(ITypescriptInterfaceExportable).IsAssignableFrom(type) && type.IsInterface && type.Name != "ITypescriptInterfaceExportable");
            return exportableInterfaces;
        }

        /// <summary>
        /// Filters AppDomain.CurrentDomain assemblies to only allow the specified assemblies if defined
        /// </summary>
        /// <returns>A list of assemblies</returns>
        private IEnumerable<Assembly> GetFilteredAssemblies()
        {
            var assemblies = filterAssemblies == null || filterAssemblies.Length == 0
                ? AppDomain.CurrentDomain.GetAssemblies()
                : AppDomain.CurrentDomain.GetAssemblies().Where(assembly => filterAssemblies.Any(dir => assembly.Location.StartsWith(dir, StringComparison.OrdinalIgnoreCase))).ToArray();
            return assemblies;
        }

        /// <summary>
        /// Transforms csharp classes into usable typescript models. This exported model will also contain the associated import required to use the model.
        /// </summary>
        /// <param name="type">A class type that needs to be transformed</param>
        /// <returns>Typescript code</returns>
        private string? GenerateTypescriptClassCode(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var importStatements = new HashSet<string>();
            var tsCode = new StringBuilder();

            // Generate import statements
            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;
                if (!propertyType.IsPrimitive && propertyType.Namespace != null)
                {
                    var importData = GetImportPath(propertyType, type);
                    if (importData != null)
                    {
                        foreach (var item in importData)
                        {
                            importStatements.Add($"import {{ {item.ImportClassName} }} from \"{item.ImportPath}\";");
                        }
                    }
                }

                // Handle generic types (e.g., Dictionary<string, IMyCustomType>)
                if (propertyType.IsGenericType)
                {
                    var genericArguments = propertyType.GetGenericArguments();
                    foreach (var arg in genericArguments)
                    {
                        var importData = GetImportPath(arg, type);
                        if (importData != null)
                        {
                            foreach (var item in importData)
                            {
                                importStatements.Add($"import {{ {item.ImportClassName} }} from \"{item.ImportPath}\";");
                            }
                        }
                    }
                }
            }

            // Concatenate import statements into a single string
            var importStatementsString = string.Join("\n", importStatements.Distinct());

            // Append import statements to TypeScript code
            tsCode.AppendLine(importStatementsString);

            // Append TypeScript class definition
            tsCode.AppendLine($"\nexport class {type.Name} {{");
            foreach (var property in properties)
            {
                var propertyName = property.Name;
                var propertyType = MapCSharpTypeToTSType(property.PropertyType);

                // Check if the property has a JsonProperty attribute
                var jsonPropertyAttribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                if (jsonPropertyAttribute != null)
                {
                    propertyName = jsonPropertyAttribute.PropertyName;
                }

                // Append property to TypeScript code
                tsCode.Append($"  {propertyName}{(propertyType.EndsWith('?') ? '?' : '!')}: {propertyType};\n");
            }
            tsCode.Append("}\n");

            return tsCode.ToString();
        }

        /// <summary>
        /// Transforms csharp interfaces into usable typescript models. This exported model will also contain the associated import required to use the model.
        /// </summary>
        /// <param name="type">An interface type that needs to be transformed</param>
        /// <returns>Typescript code</returns>
        private string? GenerateInterfaceTypescriptCode(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var importStatements = new HashSet<string>(); // Using HashSet to ensure unique imports
            var tsCode = new StringBuilder();

            // Generate import statements
            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;
                if (!propertyType.IsPrimitive && propertyType.Namespace != null)
                {
                    var importData = GetImportPath(propertyType, type);
                    if (importData != null)
                    {
                        foreach (var item in importData)
                        {
                            importStatements.Add($"import {{ {item.ImportClassName} }} from \"{item.ImportPath}\";");
                        }
                    }

                    // Check if the property type is a generic type
                    if (propertyType.IsGenericType)
                    {
                        // Get the generic arguments and add their import statements
                        var genericArguments = propertyType.GetGenericArguments();
                        foreach (var arg in genericArguments)
                        {
                            var genericImportData = GetImportPath(arg, type);
                            if (genericImportData != null)
                            {
                                foreach (var item in genericImportData)
                                {
                                    importStatements.Add($"import {{ {item.ImportClassName} }} from \"{item.ImportPath}\";");
                                }
                            }
                        }
                    }
                }
            }

            // Concatenate import statements into a single string
            var importStatementsString = string.Join("\n", importStatements.Distinct());

            // Append import statements to TypeScript code
            tsCode.AppendLine(importStatementsString);

            // Append TypeScript interface definition
            tsCode.AppendLine($"\nexport interface {type.Name} {{");
            foreach (var property in properties)
            {
                var propertyName = property.Name;
                var propertyType = MapCSharpTypeToTSType(property.PropertyType);

                // Check if the property has a JsonProperty attribute
                var jsonPropertyAttribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                if (jsonPropertyAttribute != null)
                {
                    propertyName = jsonPropertyAttribute.PropertyName;
                }

                // Append property to TypeScript code
                tsCode.Append($"  {propertyName}: {propertyType};\n");
            }
            tsCode.Append("}\n");

            return tsCode.ToString();
        }

        /// <summary>
        /// Maps a csharp type to a specific typescript type
        /// </summary>
        /// <param name="type">Type to convert</param>
        /// <returns>Typescript type</returns>
        /// <exception cref="NotImplementedException">This exception means you attempted to use a type that is not currently mapped</exception>
        private string MapCSharpTypeToTSType(Type type)
        {
            Type checkType = type;

            if (type.Name.StartsWith("Nullable"))
            {
                checkType = Nullable.GetUnderlyingType(type) ?? throw new NotImplementedException();
            }

            string returnType;

            if (checkType.IsEnum)
            {
                returnType = "string";
            }
            else if (checkType.FullName?.StartsWith("System.Collections") == true)
            {
                // Handle dictionary types
                if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) || type.GetGenericTypeDefinition() == typeof(ConcurrentDictionary<,>)))
                {
                    var keyType = MapCSharpTypeToTSType(type.GetGenericArguments()[0]);
                    var valueType = MapCSharpTypeToTSType(type.GetGenericArguments()[1]);
                    returnType = $"{{ [key: {keyType}]: {valueType} }}";
                }
                // Handle other collection types as arrays
                else
                {
                    var elementType = type.GetGenericArguments().FirstOrDefault();
                    returnType = elementType != null ? $"[{MapCSharpTypeToTSType(elementType)}]" : "[]";
                }
            }
            else if (checkType.FullName?.StartsWith("System") == true && !checkType.IsPrimitive)
            {
                if (checkType == typeof(DateTime))
                {
                    returnType = "Date";
                }
                else if (checkType == typeof(StringBuilder))
                {
                    returnType = "string";
                }
                else if (checkType == typeof(Color))
                {
                    returnType = "string";
                }
                else if (checkType == typeof(string))
                {
                    returnType = "string";
                }
                else if (checkType == typeof(int) || checkType == typeof(float) || checkType == typeof(double) || checkType == typeof(decimal))
                {
                    returnType = "number";
                }
                else
                {
                    returnType = "string"; // Default to string for unknown types
                }
            }
            else
            {
                if (checkType == typeof(Int16) || checkType == typeof(Int32) || checkType == typeof(Int64) || checkType == typeof(UInt16) || checkType == typeof(UInt32) || checkType == typeof(UInt64))
                {
                    returnType = "number";
                }
                else if (checkType == typeof(String))
                {
                    returnType = "string";
                }
                else
                {
                    // For custom types, return the type name
                    returnType = checkType.Name;
                }
            }

            return returnType;
        }

        /// <summary>
        /// Fetches the import path to determine if we need to import a custom location for the typescript model
        /// </summary>
        /// <returns>A list of import paths required to support the typescript model</returns>
        private IEnumerable<TypescriptImportData> GetImportPath(Type type, Type sourceType)
        {
            var importData = new List<TypescriptImportData>();

            if (type.Namespace == null || type.Namespace.StartsWith("System") || type.IsEnum)
            {
                return importData; // Skip System and Enum types
            }

            // Determine the relative path to the target namespace
            var sourceNamespaceParts = sourceType.Namespace?.Split('.') ?? Array.Empty<string>();
            var typeNamespaceParts = type.Namespace?.Split('.') ?? Array.Empty<string>();

            var commonParts = new List<string>();
            int minLength = Math.Min(sourceNamespaceParts.Length, typeNamespaceParts.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (sourceNamespaceParts[i] == typeNamespaceParts[i])
                {
                    commonParts.Add(sourceNamespaceParts[i]);
                }
                else
                {
                    break;
                }
            }

            // Determine the relative path to the target namespace
            int backSteps = sourceNamespaceParts.Length - commonParts.Count;
            int forwardSteps = typeNamespaceParts.Length - commonParts.Count;
            var relativePath = "";
            if (backSteps > 0)
            {
                relativePath = string.Join("/", Enumerable.Repeat("..", backSteps));
            }
            if (forwardSteps > 0)
            {
                relativePath = Path.Combine(relativePath, string.Join("/", typeNamespaceParts.Skip(commonParts.Count)));
            }
            relativePath = relativePath.Length == 0 ? "." : relativePath;

            // Replace backslashes with forward slashes
            relativePath = relativePath.Replace("\\", "/");

            // Add the import data for the current type
            importData.Add(new TypescriptImportData()
            {
                ImportPath = $"{relativePath}/{type.Name}",
                ImportClassName = type.Name
            });

            return importData;
        }

        /// <summary>
        /// Create an output file path
        /// </summary>
        /// <returns>An output file path string</returns>
        private string GetOutputFilePath(Type type)
        {
            var relativePath = type.Namespace.Replace('.', Path.DirectorySeparatorChar);
            var fileName = type.Name + ".ts";
            return Path.Combine(outputDirectory, relativePath, fileName);
        }

        /// <summary>
        /// Write the file to disk
        /// </summary>
        /// <param name="filePath">Target typescript file path</param>
        /// <param name="content">Content that needs to be written to the file</param>
        private void WriteToFile(string filePath, string content)
        {
            var directory = Path.GetDirectoryName(filePath) ?? "";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(filePath, content.Trim());
        }
    }

}
