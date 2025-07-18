﻿using System.Reflection.Metadata.Ecma335;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

using TythonCompiler.SemanticAnalysis;
using TythonCompiler.Syntax.Statements;
using TythonCompiler.Tokenization;
using TythonCompiler.SemanticAnalysis.Symbols;

namespace TythonCompiler.CodeGeneration;

public class TypeGenerator
{
    readonly string appName;
    readonly IStatement[] statements;
    readonly SymbolTable symbolTable;

    readonly PersistedAssemblyBuilder ab;
    MethodBuilder main;

    public TypeGenerator(IStatement[] statements, SymbolTable symbolTable, string appname)
    {
        appName = appname;
        this.statements = statements;
        this.symbolTable = symbolTable;

        ab = new(new AssemblyName(appName), typeof(object).Assembly);
    }

    public void GenerateAssembly()
    {
        symbolTable.ResetScope();

        ModuleBuilder mob = ab.DefineDynamicModule(appName);
        TypeBuilder tb = mob.DefineType("Program", TypeAttributes.Public | TypeAttributes.Class);

        List<MethodGenerator> methodGenerators = [];
        Dictionary<FunctionSymbol, MethodInfo> methods = LoadStdLibrary();

        foreach (var stmt in statements)
        {
            if (stmt is FunctionStmt f)
            {
                MethodGenerator mg = new(f, symbolTable, tb, appName);
                methodGenerators.Add(mg);
                methods.Add(f.Signature, mg.GetMethodBuilder());

                if (f.Name == "main")
                {
                    main = mg.GetMethodBuilder();
                }
            }
        }

        foreach (var mg in methodGenerators)
        {
            mg.EmitMethod(methods);
        }

        tb.CreateType();
    }

    public Assembly GetAssembly()
    {
        using var stream = new MemoryStream();
        ab.Save(stream);
        stream.Seek(0, SeekOrigin.Begin);
        Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        return assembly;
    }

    public void Save()
    {
        MetadataBuilder metadataBuilder = ab.GenerateMetadata(out BlobBuilder ilStream, out BlobBuilder fieldData);
        PEHeaderBuilder peHeaderBuilder = new(imageCharacteristics: Characteristics.ExecutableImage);

        ManagedPEBuilder peBuilder = new(
                        header: peHeaderBuilder,
                        metadataRootBuilder: new MetadataRootBuilder(metadataBuilder),
                        ilStream: ilStream,
                        mappedFieldData: fieldData,
                        entryPoint: MetadataTokens.MethodDefinitionHandle(main.MetadataToken));

        BlobBuilder peBlob = new();
        peBuilder.Serialize(peBlob);

        using var fileStream = new FileStream($"{appName}.exe", FileMode.Create, FileAccess.Write);
        peBlob.WriteContentTo(fileStream);

        const string runtimeconfig = @"{
    ""runtimeOptions"": {
      ""tfm"": ""net9.0"",
      ""framework"": {
        ""name"": ""Microsoft.NETCore.App"",
        ""version"": ""9.0.0""
      }
    }
  }
";

        using StreamWriter outputFile = new($"{appName}.runtimeconfig.json");
        outputFile.WriteLine(runtimeconfig);
    }

    Dictionary<FunctionSymbol, MethodInfo> LoadStdLibrary()
    {
        Dictionary<FunctionSymbol, MethodInfo> stdFunctions = [];

        var console = typeof(Console);

        stdFunctions.Add(symbolTable.GetFunction("println", [TokenType.String]),
            console.GetMethod("WriteLine", [typeof(string)]));

        stdFunctions.Add(symbolTable.GetFunction("println", [TokenType.Int]),
            console.GetMethod("WriteLine", [typeof(int)]));

        stdFunctions.Add(symbolTable.GetFunction("println", [TokenType.Real]),
            console.GetMethod("WriteLine", [typeof(double)]));

        stdFunctions.Add(symbolTable.GetFunction("println", [TokenType.Bool]),
            console.GetMethod("WriteLine", [typeof(bool)]));

        return stdFunctions;
    }
}

