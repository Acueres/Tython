﻿using TythonCompiler.SemanticAnalysis.Symbols;
using TythonCompiler.Tokenization;

namespace TythonCompiler.SemanticAnalysis;

    public class Scope
    {
        public Scope Root => root;
        public int ScopeIndex { get; }

        readonly Scope root;
        readonly Dictionary<int, VariableSymbol> variables = [];
        readonly Dictionary<int, ParameterSymbol> parameters = [];
        readonly Dictionary<int, ConstantSymbol> constants = [];
        readonly Dictionary<FunctionSignature, FunctionSymbol> functions = [];

        readonly HashSet<int> initialized = [];

        int parameterCount = 0;

        public Scope()
        {
            root = null;
            ScopeIndex = 0;
        }

        public Scope(Scope root, int scopeIndex)
        {
            this.root = root;
            ScopeIndex = scopeIndex;
        }

        public FunctionSignature AddFunction(int symbolId, TokenType returnType, TokenType[] parameterTypes)
        {
            FunctionSymbol symbol = new(returnType, parameterTypes);
            FunctionSignature signature = new(symbolId, parameterTypes);
            functions.Add(signature, symbol);
            return signature;
        }

        public FunctionSymbol? GetFunction(FunctionSignature signature)
        {
            if (!functions.TryGetValue(signature, out FunctionSymbol? symbol))
            {
                if (root is null)
                {
                    return null;
                }

                return root.GetFunction(signature);
            }

            return symbol;
        }

        public ConstantSymbol AddConstant(int symbolId, object value, TokenType type)
        {
            ConstantSymbol symbol = new(value, type);
            constants.Add(symbolId, symbol);
            return symbol;
        }

        public ConstantSymbol? GetConstant(int symbolId)
        {
            if (!constants.TryGetValue(symbolId, out ConstantSymbol? symbol))
            {
                if (root is null)
                {
                    return null;
                }

                return root.GetConstant(symbolId);
            }

            return symbol;
        }

        public ParameterSymbol AddParameter(int symbolId, TokenType type)
        {
            ParameterSymbol symbol = new(parameterCount++, type);
            parameters.Add(symbolId, symbol);
            return symbol;
        }

        public ParameterSymbol GetParameter(int symbolId)
        {
            if (!parameters.TryGetValue(symbolId, out ParameterSymbol? symbol))
            {
                if (root is null)
                {
                    return null;
                }

                return root.GetParameter(symbolId);
            }

            return symbol;
        }

    public void UpdateParameter(int symbolId, TokenType type)
    {
        if (parameters.TryGetValue(symbolId, out ParameterSymbol? symbol))
        {
            ParameterSymbol newSymbol = new(symbol.Index, type);
            parameters[symbolId] = newSymbol;
        }
    }

    public VariableSymbol AddVariable(int symbolId, TokenType type)
        {
            VariableSymbol symbol = new(type);
            variables.Add(symbolId, symbol);
            return symbol;
        }

        public void InitializeVariable(int symbolId)
        {
            initialized.Add(symbolId);
        }

        /**Search for symbol in scopes disregarding its initialization status.*/
        public VariableSymbol? GetVariable(int symbolId)
        {
            if (!variables.TryGetValue(symbolId, out VariableSymbol? symbol))
            {
                if (root is null)
                {
                    return null;
                }

                return root.GetVariable(symbolId);
            }

            return symbol;
        }

        /**Search for an initialized symbol in scopes.*/
        public VariableSymbol? GetInitializedVariable(int symbolId)
        {
            if (!initialized.Contains(symbolId))
            {
                if (root is null)
                {
                    return null;
                }

                return root.GetInitializedVariable(symbolId);
            }

            return variables[symbolId];
        }

    public void UpdateVariable(int symbolId, TokenType type)
    {
        if (variables.ContainsKey(symbolId))
        {
            VariableSymbol newSymbol = new(type);
            variables[symbolId] = newSymbol;
        }
    }
}
