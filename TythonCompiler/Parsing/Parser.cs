﻿using TythonCompiler.Syntax;
using TythonCompiler.Tokenization;
using TythonCompiler.SemanticAnalysis;
using TythonCompiler.Diagnostics.Exceptions;
using TythonCompiler.Diagnostics.Errors;
using TythonCompiler.Syntax.Expressions;
using TythonCompiler.Syntax.Statements;

namespace TythonCompiler.Parsing;

public class Parser(Token[] tokens, string filename)
{
    readonly string fileName = filename;

    bool AtEnd => tokenIndex >= tokens.Length;

    readonly Token[] tokens = tokens;
    readonly List<IStatement> statements = [];
    readonly SymbolTable symbolTable = new();
    readonly List<ITythonError> errors = [];
    int tokenIndex;

    public (IStatement[], SymbolTable symbolTable, List<ITythonError>) Execute()
    {
        while (!AtEnd)
        {
            if (Current.Type == TokenType.EOF) break;

            try
            {
                if (Current.Type == TokenType.EOF) break;
                IStatement stmt = ParseStatement();
                statements.Add(stmt);
            }
            catch (ParseException)
            {
                Synchronize();
            }
        }

        return (statements.ToArray(), symbolTable, errors);
    }

    IStatement ParseStatement()
    {
        if (Match(TokenType.Const))
        {
            return ParseConstantDeclaration();
        }

        if (Match(TokenType.Let))
        {
            return ParseVariableDeclarationStatement();
        }

        if (Match(TokenType.Return))
        {
            return ParseReturnStatement();
        }

        if (Match(TokenType.BraceLeft))
        {
            return ParseBlockStatement();
        }

        if (Match(TokenType.If))
        {
            return ParseIfStatement();
        }

        if (Match(TokenType.While))
        {
            return ParseWhileStatement();
        }

        if (Match(TokenType.Break, TokenType.Continue))
        {
            JumpStmt jumpStmt = new(Previous);
            Consume(TokenType.Semicolon, "Expect ';' after break or continue");
            return jumpStmt;

        }

        if (Match(TokenType.Def))
        {
            return ParseFunctionDeclaration();
        }

        IExpression expr = ParseExpression();
        Consume(TokenType.Semicolon, "Expect ';' after expression");

        return new ExpressionStmt(expr);
    }

    List<IStatement> ParseStatements()
    {
        List<IStatement> statements = [];

        while (Current.Type != TokenType.BraceRight && !AtEnd)
        {
            IStatement stmt = ParseStatement();
            statements.Add(stmt);
        }

        return statements;
    }

    BlockStmt ParseBlockStatement()
    {
        int scopeIndex = symbolTable.BeginScope();

        List<IStatement> statements = ParseStatements();

        Consume(TokenType.BraceRight, "Expect '}' after block");

        symbolTable.ExitScope();

        return new BlockStmt(statements, scopeIndex);
    }

    IfStmt ParseIfStatement()
    {
        IExpression condition = ParseExpression();

        // Handle ASI artefacts
        Match(TokenType.Semicolon);
        Consume(TokenType.BraceLeft, "Expect '{' after if condition");

        IStatement stmt = ParseBlockStatement();

        IStatement? elseStmt = null;
        if (Match(TokenType.Else))
        {
            // Handle ASI artefacts
            Match(TokenType.Semicolon);
            elseStmt = ParseStatement();
        }
        else if (Match(TokenType.Elif))
        {
            // Handle ASI artefacts
            Match(TokenType.Semicolon);
            elseStmt = ParseIfStatement();
        }

        return new IfStmt(condition, stmt, elseStmt);
    }

    WhileStmt ParseWhileStatement()
    {
        IExpression condition = ParseExpression();

        // Handle ASI artefacts
        Match(TokenType.Semicolon);
        Consume(TokenType.BraceLeft, "Expect '{' after if condition");

        IStatement body = ParseBlockStatement();

        return new WhileStmt(condition, body);
    }

    FunctionStmt ParseFunctionDeclaration()
    {
        Token functionName = Consume(TokenType.Identifier, "Expect function name");
        Consume(TokenType.ParenthesisLeft, "Expect '(' after function name");
        List<Parameter> parameters = [];

        int scopeIndex = symbolTable.BeginScope();
        if (Current.Type != TokenType.ParenthesisRight)
        {
            parameters = ParseParameters();
        }

        Consume(TokenType.ParenthesisRight, "Expect ')' after parameters");

        TokenType returnType = TokenType.None;
        if (Match(TokenType.Arrow))
        {
            returnType = ParseTypeDeclaration();
        }

        Consume(TokenType.BraceLeft, "Expect '{' before function body");
        List<IStatement> body = ParseStatements();

        Consume(TokenType.BraceRight, "Expect '}' after function body");

        symbolTable.ExitScope();

        var signature = symbolTable.RegisterFunction((string)functionName.Value, returnType, [.. parameters.Select(p => p.Type)]);

        return new FunctionStmt((string)functionName.Value, signature, scopeIndex, parameters, returnType, body);
    }

    ReturnStmt ParseReturnStatement()
    {
        if (Match(TokenType.Semicolon))
        {
            return new ReturnStmt(null);
        }

        IExpression value = ParseExpression();

        Consume(TokenType.Semicolon, "Expect ';' after return value");
        return new ReturnStmt(value);
    }

    VariableStmt ParseVariableDeclarationStatement()
    {
        Token token = Consume(TokenType.Identifier, "Expect variable name");

        TokenType declaredType = TokenType.None;
        if (Match(TokenType.Colon))
        {
            declaredType = ParseTypeDeclaration();
        }

        IExpression? initializer = null;
        if (Match(TokenType.Assignment))
        {
            initializer = ParseExpression();
        }

        if (initializer == null)
        {
            ParseError error = new(token, fileName, "Variable must be initialized");
            errors.Add(error);
            throw error.Exception();
        }
        else
        {
            Consume(TokenType.Semicolon, "Expect ';' after variable declaration");

            string name = (string)token.Value;
            symbolTable.RegisterVariable(name, declaredType);
            return new(initializer, name, declaredType);
        }
    }

    ConstantStmt ParseConstantDeclaration()
    {
        Token token = Consume(TokenType.Identifier, "Expect constant name");

        Consume(TokenType.Colon, "Expect type declaration");
        TokenType declaredType = ParseTypeDeclaration();

        Consume(TokenType.Assignment, "Expect constant value");
        IExpression initializer = ParseExpression();

        if (initializer is not LiteralExpr literal)
        {
            ParseError error = new(token, fileName, "Const value must be literal");
            errors.Add(error);
            throw error.Exception();
        }

        Consume(TokenType.Semicolon, "Expect ';' after constant declaration");

        string name = (string)token.Value;
        symbolTable.RegisterConstant(name, literal.Token.Value, declaredType);
        return new(initializer, name, declaredType);
    }

    TokenType ParseTypeDeclaration()
    {
        Token next = Advance();
        return next.Type;
    }

    List<Parameter> ParseParameters()
    {
        List<Parameter> parameters = [];
        do
        {
            if (parameters.Count > ushort.MaxValue)
            {
                errors.Add(new ParseError(Current, fileName, "Argument count exceeded"));
            }

            Token name = Consume(TokenType.Identifier, "Expect parameter name");
            Consume(TokenType.Colon, "Expect colon before type declaration");
            Token type = Advance();

            Parameter parameter = new((string)name.Value, type.Type);
            parameters.Add(parameter);

            symbolTable.RegisterParameter(parameter.Name, parameter.Type);
        }
        while (Match(TokenType.Comma));

        return parameters;
    }

    public IExpression ParseExpression()
    {
        return ParseAssignment();
    }

    IExpression ParseAssignment()
    {
        IExpression expr = ParseLogicalOr();

        if (Match(TokenType.Assignment))
        {
            Token token = Previous2;
            IExpression value = ParseAssignment();

            if (expr.Type == ExpressionType.Variable)
            {
                string name = ((VariableExpr)expr).Name;
                return new AssignmentExpr(name, value);
            }

            ParseError error = new(token, fileName, "Invalid assignment target");
            errors.Add(error);
        }

        return expr;
    }

    IExpression ParseLogicalOr()
    {
        IExpression expr = ParseLogicalAnd();

        while (Match(TokenType.Or))
        {
            Token oper = Previous;
            IExpression right = ParseLogicalAnd();
            expr = new LogicalExpr(oper, expr, right);
        }

        return expr;
    }

    IExpression ParseLogicalAnd()
    {
        IExpression expr = ParseEquality();

        while (Match(TokenType.And))
        {
            Token oper = Previous;
            IExpression right = ParseEquality();
            expr = new LogicalExpr(oper, expr, right);
        }

        return expr;
    }

    IExpression ParseEquality()
    {
        IExpression expr = ParseComparison();

        while (Match(TokenType.Equal, TokenType.NotEqual))
        {
            Token oper = Previous;
            IExpression right = ParseComparison();
            expr = new BinaryExpr(oper, expr, right);
        }

        return expr;
    }

    IExpression ParseComparison()
    {
        IExpression expr = ParseTerm();

        while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
        {
            Token oper = Previous;
            IExpression right = ParseTerm();
            expr = new BinaryExpr(oper, expr, right);
        }

        return expr;
    }

    IExpression ParseTerm()
    {
        IExpression expr = ParseFactor();

        while (Match(TokenType.Plus, TokenType.Minus))
        {
            Token oper = Previous;
            IExpression right = ParseFactor();
            expr = new BinaryExpr(oper, expr, right);
        }

        return expr;
    }

    IExpression ParseFactor()
    {
        IExpression expr = ParseUnary();

        while (Match(TokenType.Slash, TokenType.Star))
        {
            Token oper = Previous;
            IExpression right = ParseUnary();
            expr = new BinaryExpr(oper, expr, right);
        }

        return expr;
    }

    IExpression ParseUnary()
    {
        if (Match(TokenType.Not, TokenType.Minus))
        {
            Token oper = Previous;
            IExpression right = ParseUnary();
            return new UnaryExpr(oper, right);
        }

        return ParseCall();
    }

    IExpression ParseCall()
    {
        IExpression expr = ParsePrimary();

        while (Match(TokenType.ParenthesisLeft))
        {
            expr = CompleteCall(expr);
        }

        return expr;
    }

    CallExpr CompleteCall(IExpression callee)
    {
        List<IExpression> args = [];
        if (Current.Type != TokenType.ParenthesisRight)
        {
            do
            {
                if (args.Count > ushort.MaxValue)
                {
                    errors.Add(new ParseError(Current, fileName, "Argument count exceeded"));
                }

                args.Add(ParseExpression());
            }
            while (Match(TokenType.Comma));
        }
        Token closingParenthesis = Consume(TokenType.ParenthesisRight, "Expect ')' after arguments.");
        return new CallExpr(callee, closingParenthesis, args);
    }

    IExpression ParsePrimary()
    {
        if (Match(TokenType.None, TokenType.LiteralTrue, TokenType.LiteralFalse,
                  TokenType.LiteralInt, TokenType.LiteralReal, TokenType.LiteralString))
            return new LiteralExpr(Previous);

        if (Match(TokenType.Identifier))
        {
            string name = (string)Previous.Value;
            return new VariableExpr(name);
        }

        if (Match(TokenType.ParenthesisLeft))
        {
            IExpression expr = ParseExpression();
            Consume(TokenType.ParenthesisRight, "Expect ')' after expression");
            return new GroupingExpr(expr);
        }

        ParseError error = new(Current, fileName, "Expect expression");
        errors.Add(error);
        throw error.Exception();
    }

    void Synchronize()
    {
        Advance();

        while (!AtEnd)
        {
            if (Current.Type == TokenType.Semicolon) return;

            switch (Current.Type)
            {
                case TokenType.Class:
                case TokenType.Struct:
                case TokenType.Interface:
                case TokenType.Enum:
                case TokenType.Def:
                case TokenType.For:
                case TokenType.If:
                case TokenType.While:
                case TokenType.Return:
                    return;
            }

            Advance();
        }
    }

    Token Consume(TokenType symbol, string message)
    {
        if (Current.Type == symbol) return Advance();
        ParseError error = new(Current, fileName, message);
        errors.Add(error);
        throw error.Exception();
    }

    Token Advance()
    {
        return tokens[tokenIndex++];
    }

    Token GetToken(int offset)
    {
        int nextTokenPos = tokenIndex + offset;
        Token token = tokens[nextTokenPos];
        return token;
    }

    Token Current => GetToken(0);

    Token Previous => GetToken(-1);

    Token Previous2 => GetToken(-2);

    bool Match(params TokenType[] values)
    {
        foreach (TokenType value in values)
        {
            if (Current.Type == value)
            {
                Advance();
                return true;
            }
        }

        return false;
    }
}
