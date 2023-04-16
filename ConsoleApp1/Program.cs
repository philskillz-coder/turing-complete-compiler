namespace compiler;


class Program
{
    readonly static string CODE = @"
SET *0 10
WHILE GR *0 0
    SUB *0 1 *0
ENDWHILE
";




    static void Main(string[] args)
    {
        Compiler compiler = new Compiler(CODE, do_comments: false, comment_prefix: " # ", false);
        Console.WriteLine(compiler.Process());
    }
}