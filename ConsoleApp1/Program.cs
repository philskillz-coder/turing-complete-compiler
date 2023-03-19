namespace compiler;

class Program
{
    readonly static String CODE = @"
DEF test
VAR number 5
ENDDEF

DEF main
CALL test
ENDDEF

CALL main

";



    static void Main(string[] args)
    {
        Compiler compiler = new Compiler(CODE);
        Console.WriteLine(compiler.Process());
    }
}