﻿namespace TythonCompiler.Syntax.Expressions
{
    public class GroupingExpr(IExpression expr) : IExpression
    {
        public ExpressionType Type => ExpressionType.Grouping;
        public IExpression Expr { get; } = expr;
    }
}
