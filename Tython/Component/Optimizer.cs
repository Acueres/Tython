﻿using Tython.Model;

namespace Tython.Component
{
    public class Optimizer(IStatement[] statements)
    {
        readonly IStatement[] statements = statements;

        public IStatement[] Execute()
        {
            List<IStatement> result = [];

            foreach (var statement in statements)
            {
                IExpression evaluatedExpr = EvaluateExpression(statement.Expression);
                IStatement stmt;

                if (statement.Type == StatementType.Print)
                {
                    stmt = new PrintStmt(evaluatedExpr);
                }
                else if (statement.Type == StatementType.Variable)
                {
                    VariableStmt variableStmt = (VariableStmt)statement;
                    stmt = new VariableStmt(evaluatedExpr, variableStmt.Name, variableStmt.VariableType);
                }
                else
                {
                    stmt = new ExpressionStmt(evaluatedExpr);
                }

                result.Add(stmt);
            }

            return [.. result];
        }

        IExpression EvaluateExpression(IExpression expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.Literal:
                    return expression;
                case ExpressionType.Variable:
                    return expression;
                case ExpressionType.Grouping:
                    {
                        var expr = (GroupingExpr)expression;
                        return EvaluateExpression(expr.Expr);
                    }
                case ExpressionType.Unary:
                    {
                        var expr = (UnaryExpr)expression;
                        var evaluatedExpr = EvaluateExpression(expr.Expr);

                        if (evaluatedExpr is not LiteralExpr literal) return new UnaryExpr(expr.Operator, evaluatedExpr);

                        object value = literal.Token.Value;
                        bool isDouble = value is double;
                        bool isInt = value is int;
                        bool isBool = value is bool;

                        switch (expr.Operator.Type)
                        {
                            case TokenType.Minus:
                                {
                                    if (isInt) value = -(int)value;
                                    else if (isDouble) value = -(double)value;
                                    else throw new Exception($"Operator - is not defined for {value}");
                                    break;
                                }
                            case TokenType.Not:
                                {
                                    if (isBool) value = !(bool)value;
                                    else throw new Exception($"Operator not is not defined for {value}");
                                    break;
                                }
                            default:
                                throw new Exception($"Operator {expr.Operator.Value} is not unary");
                        }

                        Token token;
                        if (isDouble)
                            token = new(value, literal.Token.Line, TokenType.Real);
                        else if (isInt)
                            token = new(value, literal.Token.Line, TokenType.Int);
                        else
                            token = new((bool)value ? TokenType.True : TokenType.False, literal.Token.Line);

                        return new LiteralExpr(token);
                    }
                case ExpressionType.Binary:
                    {
                        var expr = (BinaryExpr)expression;
                        var left = EvaluateExpression(expr.Left);
                        var right = EvaluateExpression(expr.Right);

                        if (left is not LiteralExpr leftLiteral || right is not LiteralExpr rightLiteral)
                        {
                            return new BinaryExpr(expr.Operator, left, right);
                        }

                        object leftValue = leftLiteral.Token.Value;
                        object rightValue = rightLiteral.Token.Value;
                        object value;

                        bool isPrimaryDouble = leftValue is double;
                        bool isPrimaryLong = leftValue is int;
                        bool isPrimaryString = leftValue is string;

                        bool isSecondaryDouble = rightValue is double;
                        bool isSecondaryLong = rightValue is int;
                        bool isSecondaryString = rightValue is string;

                        switch (expr.Operator.Type)
                        {
                            case TokenType.Minus:
                                {
                                    if (isPrimaryLong && isSecondaryLong) value = (int)leftValue - (int)rightValue;
                                    else if (isPrimaryDouble && isSecondaryDouble) value = (double)leftValue - (double)rightValue;
                                    else if (isPrimaryLong && isSecondaryDouble) value = (int)leftValue - (double)rightValue;
                                    else if (isPrimaryDouble && isSecondaryLong) value = (double)leftValue - (int)rightValue;
                                    else throw new Exception($"Operator - is not defined for {leftValue}, {rightValue}");
                                    break;
                                }
                            case TokenType.Plus:
                                {
                                    if (isPrimaryString && isSecondaryString) value = (string)leftValue + (string)rightValue;
                                    else if (isPrimaryLong && isSecondaryLong) value = (int)leftValue + (int)rightValue;
                                    else if (isPrimaryDouble && isSecondaryDouble) value = (double)leftValue + (double)rightValue;
                                    else if (isPrimaryLong && isSecondaryDouble) value = (int)leftValue + (double)rightValue;
                                    else if (isPrimaryDouble && isSecondaryLong) value = (double)leftValue + (int)rightValue;
                                    else throw new Exception($"Operator + is not defined for {left}, {right}");
                                    break;
                                }
                            case TokenType.Slash:
                                {
                                    if (isSecondaryDouble && (double)rightValue == 0
                                        || isSecondaryLong && (int)rightValue == 0) throw new Exception("Division by zero");

                                    if (isPrimaryLong && isSecondaryLong) value = (int)leftValue / (int)rightValue;
                                    else if (isPrimaryDouble && isSecondaryDouble) value = (double)leftValue / (double)rightValue;
                                    else if (isPrimaryLong && isSecondaryDouble) value = (int)leftValue / (double)rightValue;
                                    else if (isPrimaryDouble && isSecondaryLong) value = (double)leftValue / (int)rightValue;
                                    else throw new Exception($"Operator / is not defined for {left}, {right}");
                                    break;
                                }
                            case TokenType.Star:
                                if (isPrimaryLong && isSecondaryLong) value = (int)leftValue * (int)rightValue;
                                else if (isPrimaryDouble && isSecondaryDouble) value = (double)leftValue * (double)rightValue;
                                else if (isPrimaryLong && isSecondaryDouble) value = (int)leftValue * (double)rightValue;
                                else if (isPrimaryDouble && isSecondaryLong) value = (double)leftValue * (int)rightValue;
                                else throw new Exception($"Operator * is not defined for {left}, {right}");
                                break;
                            case TokenType.Greater:
                                {
                                    if (isPrimaryLong && isSecondaryLong) value = (int)leftValue > (int)rightValue;
                                    else if (isPrimaryDouble && isSecondaryDouble) value = (double)leftValue > (double)rightValue;
                                    else if (isPrimaryLong && isSecondaryDouble) value = (int)leftValue > (double)rightValue;
                                    else if (isPrimaryDouble && isSecondaryLong) value = (double)leftValue > (int)rightValue;
                                    else throw new Exception($"Operator > is not defined for {left}, {right}");
                                    break;
                                }
                            case TokenType.GreaterEqual:
                                {
                                    if (isPrimaryLong && isSecondaryLong) value = (int)leftValue >= (int)rightValue;
                                    else if (isPrimaryDouble && isSecondaryDouble) value = (double)leftValue >= (double)rightValue;
                                    else if (isPrimaryLong && isSecondaryDouble) value = (int)leftValue >= (double)rightValue;
                                    else if (isPrimaryDouble && isSecondaryLong) value = (double)leftValue >= (int)rightValue;
                                    else throw new Exception($"Operator >= is not defined for {left}, {right}");
                                    break;
                                }
                            case TokenType.Less:
                                {
                                    if (isPrimaryLong && isSecondaryLong) value = (int)leftValue < (int)rightValue;
                                    else if (isPrimaryDouble && isSecondaryDouble) value = (double)leftValue < (double)rightValue;
                                    else if (isPrimaryLong && isSecondaryDouble) value = (int)leftValue < (double)rightValue;
                                    else if (isPrimaryDouble && isSecondaryLong) value = (double)leftValue < (int)rightValue;
                                    else throw new Exception($"Operator < is not defined for {left}, {right}");
                                    break;
                                }
                            case TokenType.LessEqual:
                                {
                                    if (isPrimaryLong && isSecondaryLong) value = (int)leftValue <= (int)rightValue;
                                    else if (isPrimaryDouble && isSecondaryDouble) value = (double)leftValue <= (double)rightValue;
                                    else if (isPrimaryLong && isSecondaryDouble) value = (int)leftValue <= (double)rightValue;
                                    else if (isPrimaryDouble && isSecondaryLong) value = (double)leftValue <= (int)rightValue;
                                    else throw new Exception($"Operator <= is not defined for {left}, {right}");
                                    break;
                                }
                            case TokenType.Equal:
                                {
                                    value = leftValue.Equals(rightValue);
                                    break;
                                }
                            case TokenType.NotEqual:
                                {
                                    value = !leftValue.Equals(rightValue);
                                    break;
                                }
                            default:
                                throw new Exception($"Operator {expr.Operator.Value} is not binary");
                        }

                        Token token;
                        if (value is double)
                            token = new(value, leftLiteral.Token.Line, TokenType.Real);
                        else if (value is int)
                            token = new(value, leftLiteral.Token.Line, TokenType.Int);
                        else if (value is string)
                            token = new(value, leftLiteral.Token.Line, TokenType.String);
                        else
                            token = new((bool)value ? TokenType.True : TokenType.False, leftLiteral.Token.Line);

                        return new LiteralExpr(token);
                    }
            }

            return expression;
        }
    }
}
