﻿using System.Reflection.Metadata.Ecma335;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection;
using Tython.Model;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Globalization;

namespace Tython
{
    public class CodeGenerator
    {
        readonly Statement[] statements;
        readonly string appname;

        readonly PersistedAssemblyBuilder ab;
        readonly MethodBuilder main;
        readonly ILGenerator il;

        public CodeGenerator(Statement[] statements, string appname)
        {
            this.statements = statements;
            this.appname = appname;

            EmitMain(out ab, out main, out il);
        }

        void EmitMain(out PersistedAssemblyBuilder ab, out MethodBuilder main, out ILGenerator il)
        {
            ab = new(new AssemblyName(appname), typeof(object).Assembly);
            ModuleBuilder mob = ab.DefineDynamicModule(appname);
            TypeBuilder tb = mob.DefineType("Program", TypeAttributes.Public | TypeAttributes.Class);
            main = tb.DefineMethod("Main", MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Static);
            il = main.GetILGenerator();

            foreach (Statement statement in statements)
            {
                if (statement.Type == StatementType.Print)
                {
                    EmitPrintStatement(statement);
                }
                else if (statement.Type == StatementType.Variable)
                {
                    EmitVariableDeclarationStatement(statement);
                }
            }

            il.Emit(OpCodes.Ret);

            tb.CreateType();
        }

        void EmitPrintStatement(Statement statement)
        {
            object value = EvaluateExpression(statement.Expression);

            if (value == null)
            {
                il.EmitCall(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [typeof(string)]), []);
            }
            else
            {
                il.EmitWriteLine(value.ToString());
            }
        }

        void EmitVariableDeclarationStatement(Statement statement)
        {
            object value = EvaluateExpression(statement.Expression);
            if (value != null)
            {
                il.DeclareLocal(value.GetType());
                il.Emit(OpCodes.Ldstr, value.ToString());
                il.Emit(OpCodes.Stloc, 0);
            }
            else
            {
                il.DeclareLocal(typeof(object));
            }
        }

        object? EvaluateExpression(Expression expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.Literal:
                    return expression.Token.Type switch
                    {
                        TokenType.String => expression.Token.Value,
                        TokenType.Int => long.Parse(expression.Token.Value),
                        TokenType.Real => double.Parse(expression.Token.Value, CultureInfo.InvariantCulture),
                        TokenType.True => true,
                        TokenType.False => false,
                        TokenType.None => null,
                        _ => throw new Exception("Token not a literal"),
                    };
                case ExpressionType.Variable:
                    il.Emit(OpCodes.Ldloc_S, 0);
                    break;
            }

            return null;
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

            using var fileStream = new FileStream($"{appname}.exe", FileMode.Create, FileAccess.Write);
            peBlob.WriteContentTo(fileStream);

            const string runtimeconfig = @"{
    ""runtimeOptions"": {
      ""tfm"": ""net9.0"",
      ""framework"": {
        ""name"": ""Microsoft.NETCore.App"",
        ""version"": ""9.0.0-preview.3.24172.9""
      }
    }
  }
";

            using StreamWriter outputFile = new($"{appname}.runtimeconfig.json");
            {
                outputFile.WriteLine(runtimeconfig);
            }
        }
    }
}
