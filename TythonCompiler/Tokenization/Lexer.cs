﻿using System.Globalization;
using TythonCompiler.Diagnostics.Errors;

namespace TythonCompiler.Tokenization;

public class Lexer(string source, string fileName)
{
    readonly string source = source;
    readonly string fileName = fileName;

    bool AtEnd => currentCharIndex >= source.Length;

    readonly List<Token> tokens = [];
    readonly List<ITythonError> errors = [];
    int line = 0;
    int currentCharIndex = 0;

    public (Token[] tokens, List<ITythonError> errors) Execute()
    {
        while (!AtEnd)
        {
            Token token = GetNextToken();
            if (!token.IsNull)
                tokens.Add(token);
        }

        tokens.Add(new(TokenType.EOF, line));
        return (tokens.ToArray(), errors);
    }

    Token GetNextToken()
    {
        char character = Advance();

        switch (character)
        {
            //symbols
            case '{':
                return new(TokenType.BraceLeft, line);
            case '}':
                return new(TokenType.BraceRight, line);
            case '(':
                return new(TokenType.ParenthesisLeft, line);
            case ')':
                return new(TokenType.ParenthesisRight, line);
            case '[':
                return new(TokenType.BracketLeft, line);
            case ']':
                return new(TokenType.BracketRight, line);
            case ',':
                return new(TokenType.Comma, line);
            case ':':
                return new(TokenType.Colon, line);

            //long symbols
            case '.':
                {
                    if (char.IsAsciiDigit(Peek()))
                    {
                        return ScanNumber(true);
                    }

                    return new(TokenType.Dot, line);
                }
            case '+':
                return new(TokenType.Plus, line);
            case '-':
                if (Match('>'))
                {
                    return new(TokenType.Arrow, line);
                }

                return new(TokenType.Minus, line);
            case '<':
                return new(Match('=') ? TokenType.LessEqual : TokenType.Less, line);
            case '>':
                return new(Match('=') ? TokenType.GreaterEqual : TokenType.Greater, line);
            case '*':
                return new(Match('*') ? TokenType.StarDouble : TokenType.Star, line);
            case '/':
                return new(Match('/') ? TokenType.SlashDouble : TokenType.Slash, line);
            case '=':
                return new(Match('=') ? TokenType.Equal : TokenType.Assignment, line);
            case '!': //! is not valid by itself
                if (Match('='))
                {
                    return new(TokenType.NotEqual, line);
                }
                else
                {
                    errors.Add(new SyntaxError(line, fileName, "Syntax Error: invalid syntax"));
                }
                break;

            //strings
            case '\'':
            case '"':
                return ScanString(character);

            //whitespace
            case ' ':
            case '\r':
            case '\t':
                break;

            //comments
            case '#':
                while (Peek() != '\n') Advance();
                break;

            //statement terminator
            case '\n':
                line++;
                return ScanEndOfLine();
            case ';':
                return new(TokenType.Semicolon, line);

            default:
                if (char.IsAsciiDigit(character))
                {
                    return ScanNumber();
                }

                if (char.IsLetter(character))
                {
                    return ScanIdentifier();
                }

                errors.Add(new SyntaxError(line, fileName, $"Invalid character '{character}' in token"));
                return Token.Null;
        }

        return Token.Null;
    }

    Token ScanIdentifier()
    {
        int identifierStart = currentCharIndex - 1;

        while (IsAllowedIdentifierCharacter(Peek())) Advance();

        string identifier = source[identifierStart..currentCharIndex];

        if (keywords.Contains(identifier))
        {
            return new(keywordToType[identifier], line);
        }

        return new(identifier, line, TokenType.Identifier);
    }

    Token ScanNumber(bool isFloat = false)
    {
        int numberStart = currentCharIndex - 1;

        while (char.IsAsciiDigit(Peek())) Advance();

        if (Peek() == '.' && char.IsAsciiDigit(Peek(1)))
        {
            isFloat = true;

            Advance();

            while (char.IsAsciiDigit(Peek())) Advance();
        }

        string number = source[numberStart..currentCharIndex];
        object value;
        if (isFloat)
        {
            value = double.Parse(number, CultureInfo.InvariantCulture);
        }
        else
        {
            value = int.Parse(number);
        }

        return new(value, line, isFloat ? TokenType.LiteralReal : TokenType.LiteralInt);
    }

    Token ScanString(char openingQuote)
    {
        bool multiline = Match(openingQuote, 2);
        int currentLine = line;
        int stringStart = currentCharIndex;

        while (!AtEnd && !(multiline ? Match(openingQuote, 3) : Match(openingQuote)))
        {
            if (Peek() == '\n')
            {
                if (!multiline)
                {
                    errors.Add(new SyntaxError(line, fileName, "SyntaxError: unterminated string literal"));
                    return Token.Null;
                }

                line++;
            }

            Advance();
        }

        if (AtEnd)
        {
            errors.Add(new SyntaxError(line, fileName, "SyntaxError: unterminated string literal"));
            return Token.Null;
        }

        int stringEndOffset = multiline ? 3 : 1;
        Token result = new(source[stringStart..(currentCharIndex - stringEndOffset)], currentLine, TokenType.LiteralString);

        return result;
    }

    Token ScanEndOfLine()
    {
        Token last = tokens.LastOrDefault();

        if (!terminatorExceptions.Contains(last.Type))
        {
            return new(TokenType.Semicolon, line);
        }

        return Token.Null;
    }

    bool Match(char token)
    {
        if (AtEnd || Peek() != token) return false;
        currentCharIndex++;
        return true;
    }

    bool Match(char token, int offset)
    {
        for (int i = 0; i < offset; i++)
        {
            if (AtEnd || Peek(i) != token) return false;
        }

        currentCharIndex += offset;

        return true;
    }

    char Peek(int offset = 0)
    {
        int nextCharPos = currentCharIndex + offset;
        char c = AtEnd || nextCharPos >= source.Length ? '\0' : source[nextCharPos];
        return c;
    }

    char Advance()
    {
        return source[currentCharIndex++];
    }

    static bool IsAllowedIdentifierCharacter(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    readonly static HashSet<string> keywords;
    readonly static HashSet<TokenType> terminatorExceptions;
    readonly static Dictionary<string, TokenType> keywordToType;

    static Lexer()
    {
        keywords =
        [
            "class", "struct", "interface", "enum", "def", "let", "const",
            "int", "real", "str", "true", "false", "none",
            "and", "not", "or",
            "if", "else", "elif", "for", "while", "return", "break", "continue",
            ];

        terminatorExceptions =
[
    // Internal/special
    TokenType.Null, TokenType.EOF, TokenType.Semicolon,

    // Openers
    TokenType.BracketLeft,
    TokenType.ParenthesisLeft,
    TokenType.BraceLeft,

    // Punctuation and operators that precede an operand
    TokenType.Comma,
    TokenType.Colon,
    TokenType.Arrow,
    TokenType.Dot,
    TokenType.Assignment,
    
    // Arithmetic operators
    TokenType.Plus,
    TokenType.Minus,
    TokenType.Star,
    TokenType.StarDouble,
    TokenType.Slash,
    TokenType.SlashDouble,

    // Comparison operators
    TokenType.Equal,
    TokenType.NotEqual,
    TokenType.Greater,
    TokenType.GreaterEqual,
    TokenType.Less,
    TokenType.LessEqual,
    
    // Logical operators
    TokenType.And,
    TokenType.Or,
    TokenType.Not,

    // Keywords that begin a declaration or control-flow expression
    TokenType.Def,
    TokenType.Class,
    TokenType.Struct,
    TokenType.Interface,
    TokenType.Enum,
    TokenType.Let,
    TokenType.Const,
    TokenType.If,
    TokenType.Else,
    TokenType.Elif,
    TokenType.For,
    TokenType.While
];

        keywordToType = new()
            {
                { "class", TokenType.Class },
                { "struct", TokenType.Struct },
                { "interface", TokenType.Interface },
                { "enum", TokenType.Enum },
                { "def", TokenType.Def },
                { "let", TokenType.Let },
                { "const", TokenType.Const },
                { "int", TokenType.Int },
                { "real", TokenType.Real },
                { "str", TokenType.String },
                { "bool",  TokenType.Bool },
                { "true",  TokenType.LiteralTrue },
                { "false",  TokenType.LiteralFalse },
                { "none", TokenType.None },
                { "and", TokenType.And },
                { "not", TokenType.Not },
                { "or", TokenType.Or },
                { "if", TokenType.If },
                { "else", TokenType.Else },
                { "elif", TokenType.Elif },
                { "for", TokenType.For },
                { "while", TokenType.While },
                { "return", TokenType.Return },
                { "break", TokenType.Break },
                { "continue", TokenType.Continue }
            };
    }
}
