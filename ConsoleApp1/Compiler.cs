using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;


namespace compiler;

internal class Compiler
{

    private readonly ScopeManager scopeManager = new ScopeManager(new Scope("global"));
    private readonly DefinitionManager definitionManager = new DefinitionManager();
    private readonly MemoryManager memoryManager = new MemoryManager();

    private readonly string code;
    private readonly List<String> result = new List<string>();

    private bool inDefinition = false;
    private int line = 0;
    private int callDepth = 0;

    private readonly bool do_comments;
    private readonly string comment_prefix;
    private readonly bool add_instruction_numbers;



    public Compiler(string code, bool do_comments, string comment_prefix, bool add_instruction_numbers)
    {
        this.code = code;
        this.do_comments = do_comments;
        this.comment_prefix = comment_prefix;
        this.add_instruction_numbers = add_instruction_numbers;
        Process();
    }

    private string Comment(string text)
    {
        return do_comments ? comment_prefix + Instruction_Number() + text : "";
    }
    private string Instruction_Number()
    {
        int c = result.Count()*4;
        return add_instruction_numbers ? $"[{c}, {c+1}, {c+2}, {c+3}]" : "";
    }

    private void ProcessLine(string line)
    {
        line = line.Trim();
        string[] keywords = line.Split(" ");
        switch (keywords[0].Trim())
        {
            case "SET":
                {
                    // $VAR variable
                    // $ADDR address
                    // 0123 normal

                    string destination = keywords[1].Trim();
                    string value = keywords[2].Trim();

                    // get value for variable
                    if (!scopeManager.currentScope.ResolveAddress(value, out int valueAddress, out bool valueImmediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    int destinationAddress = -1;

                    // get variable destination
                    if (destination.StartsWith(STYLES.POINTER)) // pointer
                    {
                        if (!int.TryParse(destination.Substring(STYLES.POINTER.Length), out destinationAddress)) {
                            throw new Exception("Source address not valid");
                        }

                        // if its a pointer, dont create a variable
                        //scopeManager.currentScope.SetVariable(destination, valueAddress);
                    }
                    else if (destination.StartsWith(STYLES.VARIABLE)) // variable
                    {
                        
                        if (!scopeManager.currentScope.VariableExists(destination.Substring(STYLES.VARIABLE.Length))) // variable doesnt exist
                        {
                            if (!memoryManager.GetNonOccupiedRamAddress(out int? _sourceAddress) || _sourceAddress == null) // no memory available
                            {
                                throw new Exception("No memory available");
                            }
                            destinationAddress = _sourceAddress.Value;
                        }
                        
                        scopeManager.currentScope.SetVariable(destination.Substring(1), destinationAddress);
                    }
                    else // no correct variable name
                    {
                        throw new Exception("Variable destination must be a pointer or variable address");
                    }

                    result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {destinationAddress} 0 { ADDRESSES.RAM_ADDRESS}{Comment("Move source address to ram-address-register")}"); // move the source (ram) address to register 0
                    result.Add($"{ALU.MOVE + (valueImmediate ? 1 : 0) * OP.IMMEDIATE0} {valueAddress} 0 { ADDRESSES.RAM}{Comment("Move value to ram")}"); // move the value to ram
                    break;
                }

            case "ADD":
                {
                    var _address0 = keywords[1].Trim();
                    var _address1 = keywords[2].Trim();
                    var _resultAddress = keywords[3].Trim();

                    // get value for variable
                    if (!scopeManager.currentScope.ResolveAddress(_address0, out int address0, out bool address0Immediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    // get value for variable
                    if (!scopeManager.currentScope.ResolveAddress(_address1, out int address1, out bool address1Immediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    // get value for variable
                    if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    if (!address0Immediate && !address1Immediate) // both in ram
                    {
                        // todo: use register addresses
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 { ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                        result.Add($"{ALU.MOVE} 0 0 { ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 { ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                        result.Add($"{ALU.MOVE} 0 0 { ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.ADD} { ADDRESSES.RAM_TEMP_0} { ADDRESSES.RAM_TEMP_1} { ADDRESSES.RAM}{Comment("Add values from ram-temp-0 and -1 and move result to ram")}");
                    }

                    if (address1Immediate)
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.ADD + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}{Comment("Add value from ram-temp-0 and immediate-value-1 and move result to ram")}");
                    }

                    if (address0Immediate)
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.ADD + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}{Comment("Add immediate-value-0 and value from ram-temp-0 and move result to ram")}");
                    }
                    break;
                }

            case "SUB":
                {
                    var _address0 = keywords[1].Trim();
                    var _address1 = keywords[2].Trim();
                    var _resultAddress = keywords[3].Trim();

                    // get value for variable
                    if (!scopeManager.currentScope.ResolveAddress(_address0, out int address0, out bool address0Immediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    // get value for variable
                    if (!scopeManager.currentScope.ResolveAddress(_address1, out int address1, out bool address1Immediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    // get value for variable
                    if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    if (!address0Immediate && !address1Immediate) // both in ram
                    {
                        // todo: use register addresses
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.SUBTRACT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}{Comment("Add values from ram-temp-0 and -1 and move result to ram")}");
                    }

                    if (address1Immediate)
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.SUBTRACT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}{Comment("Add value from ram-temp-0 and immediate-value-1 and move result to ram")}");
                    }

                    if (address0Immediate)
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.SUBTRACT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}{Comment("Add immediate-value-0 and value from ram-temp-0 and move result to ram")}");
                    }
                    break;
                }

            case "DEF":
                {
                    string name = keywords[1].Trim();
                    int currentPosition = (result.Count+1) * 4;
                    inDefinition = true;

                    if (!definitionManager.CreateDefinition(name, currentPosition, result.Count, out Definition? definition) || definition == null)
                    {
                        throw new Exception("A definition with this name already exists");
                    }

                    if (!scopeManager.CreateScope("definition." + name, out Scope? scope) || scope == null)
                    {
                        throw new Exception("Scope error");
                    }
                    scopeManager.SetCurrentScope(scope);
                    definitionManager.AddOpenCodeDefinition(definition);
                    // add jump-back address to jump-back register

                    break;
                }

            case "ENDDEF":
                {
                    if (!inDefinition)
                    {
                        throw new Exception("Not in a definition");
                    }

                    inDefinition = false;

                    // +6 for 6 extra actions
                    int currentPosition = (result.Count+4) * 4;
                    Definition definition = definitionManager.LatestOpenCodeDefinition();
                    definitionManager.CloseLatestOpenCodeDefinition();

                    // inject a jump-to reference before the definition start to jump to the instruction where the definition ends
                    result.Insert(definition.ResultStartIndex, $"{ OP.ALU +  ALU.MOVE +  OP.IMMEDIATE0} {currentPosition} 0 { ADDRESSES.CLOCK}{Comment("Jump to the end of the definition")}");

                    // jump-back to origin
                    result.Add($"{ OP.ALU + ALU.SUBTRACT + OP.IMMEDIATE1} { ADDRESSES.JB_LATEST} 1 { ADDRESSES.JB_LATEST}{Comment("Subtract 1 from the latest jump-back-address")}");
                    result.Add($"{ OP.ALU + ALU.MOVE} { ADDRESSES.JB_LATEST} 0 { ADDRESSES.JB_RAM_ADDRESS}{Comment("Move the value from the jump-back-register to the jump-back-ram-address-register")}");
                    
                    result.Add($"{ OP.ALU +  ALU.MOVE} { ADDRESSES.JB_RAM} 0 { ADDRESSES.CLOCK}{Comment("Jump to the instruction from the jump-back-ram")}");
                    
                    break;
                }

            case "CALL":
                {
                    string name = keywords[1].Trim();

                    if (!definitionManager.ResolveDefinition(name, out Definition? definition) || definition == null)
                    {
                        throw new Exception("Definition not found");
                    }

                    result.Add($"{ OP.ALU +  ALU.MOVE} {ADDRESSES.JB_LATEST} 0 { ADDRESSES.JB_RAM_ADDRESS}{Comment("Move the value from the latest jump-back-register to the jump-back-ram-address-register")}");
                    result.Add($"{OP.ALU + ALU.ADD + OP.IMMEDIATE1} {ADDRESSES.JB_LATEST} 1 {ADDRESSES.JB_LATEST}{Comment("Add 1 to the latest jump-back-address")}");


                    // +3 for 3 extra actions before actually jumping to definition
                    result.Add($"{ OP.ALU +  ALU.MOVE +  OP.IMMEDIATE0} {(result.Count + 3) * 4} 0 { ADDRESSES.JB_RAM}{Comment("Add the jump back instruction number to the jump-back-ram")}"); //jump-back address
                    result.Add($"{OP.ALU + ALU.MOVE + OP.IMMEDIATE0} {definition.StartInstruction} 0 { ADDRESSES.CLOCK}{Comment("Jump to the start of the definition")}");


                    // put current instruction in register 3 for jump back
                    // define a jump-back register which holds the instruction which to jump back after finishing the call
                    break;
                }
        }
    }

    public string Process()
    {
        string[] lines = code.Split("\n");

        foreach (string lineCode in lines)
        {
            // iterates two times over array wtf
            if (this.line == lines.Length)
            {
                break;
            }
            this.line++;

            ProcessLine(lineCode);
        }

        return string.Join("\n", result);
    }


}
