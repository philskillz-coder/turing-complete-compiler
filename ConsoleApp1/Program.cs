namespace compiler;

class Program
{
    readonly static String CODE = @"
DEF two
ENDDEF

DEF main
VAR five 5
VAR kek 27
VAR res 0
ADD $five $kek $res
CALL two
ENDDEF

CALL main

";



    static void Main(string[] args)
    {
        Compiler compiler = new Compiler(CODE);
        Console.WriteLine(compiler.Process());
    }
}