﻿using TythonCompiler.Syntax.Expressions;

namespace TythonCompiler.Syntax.Statements
{
    public class BlockStmt(List<IStatement> statements, int scopeIndex) : IStatement
    {
        public StatementType Type => StatementType.Block;
        public IExpression Expression { get; }
        public int ScopeIndex { get; } = scopeIndex;
        public List<IStatement> Statements { get; } = statements;
    }
}
