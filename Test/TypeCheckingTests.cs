using Glykon.Compiler.Syntax;
using Glykon.Compiler.Semantics;
using Glykon.Compiler.Syntax.Statements;
using Glykon.Compiler.Diagnostics.Errors;

namespace Tests;

public class TypeCheckingTests
{
    // Helpers
    private static (IStatement[] stmts, SymbolTable st, List<IGlykonError> parseErr) Parse(string src, string file)
    {
        var (tokens, _) = new Lexer(src, file).Execute();
        return new Parser(tokens, new(), file).Execute();
    }

    private static List<IGlykonError> Check(string src, string file)
    {
        var (stmts, st, parseErr) = Parse(src, file);
        Assert.Empty(parseErr); 
        var checker = new TypeChecker(st, file);
        foreach (var s in stmts) checker.Analyze(s);
        return checker.GetErrors();
    }

    // Literals, unary & binary
    [Fact]
    public void UnaryAndLiteralSuccess() 
    {
        const string code = """
            let i = 5
            let r = -i
            let f = true
            let g = not f
        """;
        Assert.Empty(Check(code, nameof(UnaryAndLiteralSuccess)));
    }

    [Fact]
    public void UnaryTypeMismatch()
    {
        const string code = """
            let s = 'text'
            let oops = -s      # string cannot be negated
        """;
        Assert.Single(Check(code, nameof(UnaryTypeMismatch)));
    }

    [Fact]
    public void BinaryArithmeticSuccess()
    {
        const string code = """
            let a = 2 + 3 * 4
            let b = a / 2 - 1
        """;
        Assert.Empty(Check(code, nameof(BinaryArithmeticSuccess)));
    }

    [Fact]
    public void BinaryArithmeticTypeMismatch()
    {
        const string code = """
            let x = 10 + 'str'   # int + string is illegal
        """;
        Assert.Single(Check(code, nameof(BinaryArithmeticTypeMismatch)));
    }

    [Fact]
    public void LogicalAndComparisonChecks()
    {
        const string ok = """
            let a = (2 < 3) and true
        """;
        Assert.Empty(Check(ok, nameof(LogicalAndComparisonChecks)));

        const string bad = """
            let b = (2 < 3) and 4 # rhs not bool
        """;
        Assert.Single(Check(bad, nameof(LogicalAndComparisonChecks) + "_bad"));
    }

    // Variable and constant declarations
    [Fact]
    public void ExplicitVariableTypeMatch()
    {
        const string code = """
            let i: int = 42
        """;
        Assert.Empty(Check(code, nameof(ExplicitVariableTypeMatch)));
    }

    [Fact]
    public void ExplicitVariableTypeMismatch()
    {
        const string code = """
            let s: str = 123
        """;
        Assert.Single(Check(code, nameof(ExplicitVariableTypeMismatch)));
    }

    [Fact]
    public void ConstantTypeMismatch()
    {
        const string code = """
            const Pi: real = 'oops'  # const must match declared type
        """;
        Assert.Single(Check(code, nameof(ConstantTypeMismatch)));
    }

    //Conditions

    [Fact]
    public void IfWhileConditionMustBeBool()
    {
        const string code = """
            if 0: let a = 1
            while 'text': break
        """;
        // both conditions wrong -> 2 errors
        Assert.Equal(2, Check(code, nameof(IfWhileConditionMustBeBool)).Count);
    }

    // Function returns

    [Fact]
    public void FunctionReturnTypeMatch()
    {
        const string code = """
            def add(a: int, b: int) -> int {
                return a + b
            }
        """;
        Assert.Empty(Check(code, nameof(FunctionReturnTypeMatch)));
    }

    [Fact]
    public void FunctionReturnTypeMismatch()
    {
        const string code = """
            def bad() -> int {
                return 'str'
            }
        """;
        Assert.Single(Check(code, nameof(FunctionReturnTypeMismatch)));
    }

    [Fact]
    public void VoidFunctionReturningValue()
    {
        const string code = """
            def nope() {
                return 1
            }
        """;
        Assert.Single(Check(code, nameof(VoidFunctionReturningValue)));
    }

    // Calls & overloads

    [Fact]
    public void CallWithCorrectArguments()
    {
        const string code = """
            def sum(a: int, b: int) -> int { return a + b }
            let r = sum(2, 3)
        """;
        Assert.Empty(Check(code, nameof(CallWithCorrectArguments)));
    }

    [Fact]
    public void CallWithWrongArgumentType()
    {
        const string code = """
            def sum(a: int, b: int) -> int { return a + b }
            let r = sum(2, 'str')
        """;
        Assert.Single(Check(code, nameof(CallWithWrongArgumentType)));
    }

    [Fact]
    public void OverloadResolutionSuccess()
    {
        const string code = """
            def log(msg: str) { return }
            def log(level: int, msg: str) { return }
            log('hi')          # picks 1‑arg
            log(1, 'bye')      # picks 2‑arg
        """;
        Assert.Empty(Check(code, nameof(OverloadResolutionSuccess)));
    }

    [Fact]
    public void OverloadResolutionFailure()
    {
        const string code = """
            def log(msg: str) { return }
            def log(level: int, msg: str) { return }
            log(true, 'oops')   # no matching overload
        """;
        Assert.Single(Check(code, nameof(OverloadResolutionFailure)));
    }
}
