namespace compiler;


static class Program
{
    readonly static string CODE = @"
SET *0 10
WHILE GR *0 0
    SUB *0 1 *0
ENDWHILE
";




    static void Main(string[] args)
    {
        Compiler compiler = new Compiler(do_comments: true, comment_prefix: " # ", false);
        Console.WriteLine(string.Join("\n", compiler.Process(CODE)));
        //LiveCode();
    }

    public static string Truncate(this string value, int maxChars)
    {
        return value.Length <= maxChars ? value : value.Substring(0, maxChars - 3) + "...";
    }


    public static void LiveCode()
    {
        Compiler compiler = new Compiler(do_comments: true, comment_prefix: " # ", false);

        var inputs = new List<string>();
        bool done = false;
        while (!done)
        {
            Console.Write("Input >>> ");
            string input = Console.ReadLine() ?? "";
            Console.Clear();
            inputs.Add(input);

            var result = compiler.Process(String.Join("\n", inputs));
            Console.WriteLine(new String('-', Console.WindowWidth));

            for (int i = 0; i < Console.WindowHeight-2; i++)
            {

                string code_section = inputs.ElementAtOrDefault(i) ?? "";
                string result_section = result.ElementAtOrDefault(i) ?? "";

                int max_length = (int)((Console.WindowWidth - 1) / 2) - 2;
                code_section = Truncate(code_section, max_length);
                code_section += new String(' ', max_length - code_section.Length);

                result_section = Truncate(result_section, max_length);
                result_section += new string(' ', max_length - result_section.Length);

                Console.WriteLine($" {code_section} | {result_section} ");
            }
        }
    }
}