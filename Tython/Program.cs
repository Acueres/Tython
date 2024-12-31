﻿using System.Reflection;
using Tython.Component;

namespace Tython
{
    internal class Program
    {
        static void Main(string[] args)
        {
            const string filename = "Test";
            const string src = @"
            let i = 0
            if i == 1 {
                print 'success'
            }
            else {
                print 'failure'
            }

            if i == 0 {
                print 'success'
            }
";
            Lexer lexer = new(src, filename);
            var (tokens, lexerErrors) = lexer.Execute();

            foreach (var error  in lexerErrors)
            {
                error.Report();
            }

            Parser parser = new(tokens, filename);
            var (stmts, symbolTable, parserErrors) = parser.Execute();

            foreach (var error in parserErrors)
            {
                error.Report();
            }

            Optimizer optimizer = new(stmts);
            var optimizedStmts = optimizer.Execute();

            var generator = new CodeGenerator(optimizedStmts, symbolTable, filename);

            Assembly assembly = generator.GetAssembly();

            Type program = assembly.GetType("Program");
            var main = program.GetMethod("Main", []);
            main.Invoke(null, []);
        }
    }
}
