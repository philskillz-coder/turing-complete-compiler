namespace compiler;


class Program
{
    readonly static String CODE = @"
DEF second
ENDDEF

DEF main
    CALL second
    SET $first 1
    SET $second 2
    SET $result 0
    ADD $first $second $result
ENDDEF

CALL main
";




    static void Main(string[] args)
    {
        Compiler compiler = new Compiler(CODE, do_comments: true, comment_prefix: " # ", false);
        Console.WriteLine(compiler.Process());
    }
}