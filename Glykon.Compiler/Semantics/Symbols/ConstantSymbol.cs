﻿using Glykon.Compiler.Syntax;

namespace Glykon.Compiler.Semantics.Symbols;

public class ConstantSymbol(int id, TokenType type, object value) : Symbol(id, type)
{
    public object Value { get; } = value;
}

