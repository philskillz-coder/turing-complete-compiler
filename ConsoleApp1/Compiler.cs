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

    private readonly ScopeManager scopeManager = new ScopeManager(new Scope("global", null));
    private readonly DefinitionManager definitionManager = new DefinitionManager();
    private readonly MemoryManager memoryManager = new MemoryManager();
    private readonly ConditionManager conditionManager = new ConditionManager();
    private readonly LoopManager loopManager = new LoopManager();

    private readonly List<string> final = new List<string>();

    private bool inDefinition = false;
    private int line = 0;
    private bool openIf = false;
    private bool openWhile = false;

    private readonly bool do_comments;
    private readonly string comment_prefix;
    private readonly bool add_instruction_numbers;



    public Compiler(bool do_comments, string comment_prefix, bool add_instruction_numbers)
    {
        this.do_comments = do_comments;
        this.comment_prefix = comment_prefix;
        this.add_instruction_numbers = add_instruction_numbers;
    }

    private string Comment(string text)
    {
        return do_comments ? comment_prefix + Instruction_Number() + text : "";
    }
    private string Instruction_Number()
    {
        int c = final.Count()*4;
        return add_instruction_numbers ? $"[{c}, {c+1}, {c+2}, {c+3}]" : "";
    }

    private List<string> ProcessLine(string line, bool in_if, bool in_while)
    {
        List<string> result = new List<string>();
        line = line.Trim();
        string[] keywords = line.Split(" ");
        switch (keywords[0].Trim())
        {
            // todo: DEL keyword
            case "SET":
                {
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }
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
                            scopeManager.currentScope.SetVariable(destination.Substring(1), destinationAddress);
                        } else
                        {
                            scopeManager.currentScope.ResolveVarName(destination.Substring(1), out destinationAddress);
                        }
                        
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
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }

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
                        result.Add($"{ALU.ADD} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}{Comment("Add values from ram-temp-0 and -1 and move result to ram")}");
                    }
                    else if (!address1Immediate)
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.ADD + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}{Comment("Add immediate-value-0 and value from ram-temp-0 and move result to ram")}");
                    }
                    else if (!address0Immediate)
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.ADD + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}{Comment("Add value from ram-temp-0 and immediate-value-1 and move result to ram")}");

                    }
                    else
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.ADD + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} {ADDRESSES.RAM}{Comment("Add immediate-value-0 and immediate-value-1 and move result to ram")}");
                    }
                    break;
                }

            case "SUB":
                {
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }

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
                        result.Add($"{ALU.SUBTRACT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}{Comment("Subtract value from ram-temp-0 from ram-temp-1 and move result to ram")}");
                    } 
                    else if (!address1Immediate)
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.SUBTRACT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}{Comment("Subract immediate-value-0 from value from ram-temp-0 and move result to ram")}");
                    }
                    else if (!address0Immediate)
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                        result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.SUBTRACT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}{Comment("Subtract value from ram-temp-0 from immediate-value-1 and move result to ram")}");


                    } else
                    {
                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}"); // move result address to ram write
                        result.Add($"{ALU.SUBTRACT + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} {ADDRESSES.RAM}{Comment("Subtract immediate-value-0 from immediate-value-1 and move result to ram")}");
                    }
                    break;
                }

            case "DEF":
                {
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }

                    string name = keywords[1].Trim();
                    int currentPosition = (final.Count+1) * 4;
                    inDefinition = true;

                    if (!definitionManager.CreateDefinition(name, currentPosition, final.Count, out Definition? definition) || definition == null)
                    {
                        throw new Exception("A definition with this name already exists");
                    }

                    if (!scopeManager.CreateScope("definition." + name, scopeManager.currentScope, out Scope? scope) || scope == null)
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
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }

                    if (!inDefinition)
                    {
                        throw new Exception("Not in a definition");
                    }

                    inDefinition = false;

                    // +6 for 6 extra actions
                    int currentPosition = (final.Count+4) * 4;
                    Definition definition = definitionManager.LatestOpenCodeDefinition();
                    definitionManager.CloseLatestOpenCodeDefinition();

                    // inject a jump-to reference before the definition start to jump to the instruction where the definition ends
                    final.Insert(definition.ResultStartIndex, $"{ OP.ALU +  ALU.MOVE +  OP.IMMEDIATE0} {currentPosition} 0 { ADDRESSES.CLOCK}{Comment("Jump to the end of the definition")}");

                    // jump-back to origin
                    result.Add($"{ OP.ALU + ALU.SUBTRACT + OP.IMMEDIATE1} { ADDRESSES.JB_LATEST} 1 { ADDRESSES.JB_LATEST}{Comment("Subtract 1 from the latest jump-back-address")}");
                    result.Add($"{ OP.ALU + ALU.MOVE} { ADDRESSES.JB_LATEST} 0 { ADDRESSES.JB_RAM_ADDRESS}{Comment("Move the value from the jump-back-register to the jump-back-ram-address-register")}");
                    
                    result.Add($"{ OP.ALU +  ALU.MOVE} { ADDRESSES.JB_RAM} 0 { ADDRESSES.CLOCK}{Comment("Jump to the instruction from the jump-back-ram")}");
                    
                    break;
                }

            case "CALL":
                {
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }

                    string name = keywords[1].Trim();

                    if (!definitionManager.ResolveDefinition(name, out Definition? definition) || definition == null)
                    {
                        throw new Exception("Definition not found");
                    }

                    result.Add($"{ OP.ALU +  ALU.MOVE} {ADDRESSES.JB_LATEST} 0 { ADDRESSES.JB_RAM_ADDRESS}{Comment("Move the value from the latest jump-back-register to the jump-back-ram-address-register")}");
                    result.Add($"{OP.ALU + ALU.ADD + OP.IMMEDIATE1} {ADDRESSES.JB_LATEST} 1 {ADDRESSES.JB_LATEST}{Comment("Add 1 to the latest jump-back-address")}");


                    // +3 for 3 extra actions before actually jumping to definition
                    // +2 for 2 actions before in the code ( because result has not been added to final -> final.Count is 2 less than it should be)
                    result.Add($"{ OP.ALU +  ALU.MOVE +  OP.IMMEDIATE0} {(final.Count + 3 + 2) * 4} 0 { ADDRESSES.JB_RAM}{Comment("Add the jump back instruction number to the jump-back-ram")}"); //jump-back address
                    result.Add($"{OP.ALU + ALU.MOVE + OP.IMMEDIATE0} {definition.StartInstruction} 0 { ADDRESSES.CLOCK}{Comment("Jump to the start of the definition")}");


                    // put current instruction in register 3 for jump back
                    // define a jump-back register which holds the instruction which to jump back after finishing the call
                    break;
                }

            case "IF":
                {
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }
                    openIf = true;

                    string conditionCode = string.Join(" ", keywords.Skip(1).ToList());
                    List<string> ifInstructions = ProcessLine(conditionCode, in_if: true, in_while: false);
                    int currentPosition = (final.Count + ifInstructions.Count() + 2) * 4;
                    
                    foreach (var item in ifInstructions)
                    {
                        result.Add(item + $"{currentPosition}");
                    }

                    conditionManager.CreateCondition(currentPosition, final.Count+ifInstructions.Count(), out Condition? condition);

                    conditionManager.AddOpenCodeCondition(condition);

                    break;
                }
            case "ENDIF":
                {
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }

                    if (!openIf)
                    {
                        throw new Exception("Not in a condition");
                    }

                    openIf = false;

                    // +1 for 1 extra action
                    int currentPosition = (final.Count + 2) * 4;
                    Condition condition = conditionManager.LatestOpenCodeCondition();
                    conditionManager.CloseLatestOpenCodeCondition();

                    // inject a jump-to reference before the definition start to jump to the instruction where the definition ends
                    final.Insert(condition.ResultStartIndex, $"{OP.ALU + ALU.MOVE + OP.IMMEDIATE0} {currentPosition} 0 {ADDRESSES.CLOCK}{Comment("Jump to the end of the condition")}");

                    break;
                }

            case "WHILE":
                {
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (in_while)
                    {
                        throw new Exception("Can't do this in while loop!");
                    }

                    openWhile = true;

                    string conditionCode = string.Join(" ", keywords.Skip(1).ToList());
                    List<string> whileInstructions = ProcessLine(conditionCode, in_if: false, in_while: true);
                    int currentPosition = (final.Count + 1) * 4;

                    foreach (var item in whileInstructions)
                    {
                        // +1 to skip the "jump-to-end" instruction
                        result.Add(item + $"{(final.Count() + whileInstructions.Count() + 1) * 4}");
                    }

                    loopManager.CreateLoop(currentPosition, final.Count + whileInstructions.Count(), out Loop? loop);

                    loopManager.AddOpenCodeLoop(loop);

                    break;
                }
            case "ENDWHILE":
                {
                    if (in_if)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }

                    if (in_while)
                    {
                        throw new Exception("Can't do this in if condition!");
                    }
                    if (!openWhile)
                    {
                        throw new Exception("Not in a while loop");
                    }

                    openWhile = false;

                    // +1 for 1 extra action
                    int currentPosition = (final.Count + 2) * 4;
                    Loop loop = loopManager.LatestOpenCodeLoop();
                    loopManager.CloseLatestOpenCodeLoop();

                    // inject a jump-to reference to the start of the loop
                    final.Add($"{OP.ALU + ALU.MOVE + OP.IMMEDIATE0} {loop.StartInstruction} 0 {ADDRESSES.CLOCK}{Comment("Jump to the start of the loop")}");

                    // inject a jump-to reference before the definition start to jump to the instruction where the definition ends if the condition is false
                    final.Insert(loop.ResultIndex, $"{OP.ALU + ALU.MOVE + OP.IMMEDIATE0} {currentPosition} 0 {ADDRESSES.CLOCK}{Comment("Jump to the end of the loop")}");

                    break;
                }


            // conditions 
            case "EQ":
                {
                    // Code for EQUAL case
                    if (in_if || in_while)
                    {
                        var _address0 = keywords[1].Trim();
                        var _address1 = keywords[2].Trim();

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

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.EQUAL + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} ");
                        }
                        else if (!address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.EQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} ");
                        }
                        else if (!address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.EQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} ");
                        }
                        else
                        {
                            result.Add($"{OP.CONDITIONS + CONDITIONS.EQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} ");
                        }

                    } else
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

                        if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                        {
                            throw new Exception("Memory address could not be resolved");
                        }

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.EQUAL + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}");
                        }
                        else if (!address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.EQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}");
                        }
                        else if (!address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.EQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}");

                        } else
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.EQUAL + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} {ADDRESSES.RAM}");
                        }
                    }
                    break;
                }
            case "NEQ":
                {
                    // Code for NOTEQUAL case
                    if (in_if || in_while)
                    {
                        var _address0 = keywords[1].Trim();
                        var _address1 = keywords[2].Trim();

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

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.NOTEQUAL + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} ");
                        }
                        else if (!address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.NOTEQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} ");
                        }
                        else if (!address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.NOTEQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} ");
                        }
                        else
                        {
                            result.Add($"{OP.CONDITIONS + CONDITIONS.NOTEQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} ");
                        }
                    }
                    else
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

                        if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                        {
                            throw new Exception("Memory address could not be resolved");
                        }

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.NOTEQUAL + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}");
                        }
                        else if (!address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.NOTEQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}");
                        }
                        else if (!address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.NOTEQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}");
                        } else
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.NOTEQUAL + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} {ADDRESSES.RAM}");
                        }
                    }
                    break;
                }
            case "SM":
                {
                    // Code for SMALLER case
                    if (in_if || in_while)
                    {
                        var _address0 = keywords[1].Trim();
                        var _address1 = keywords[2].Trim();

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

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLER + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} ");
                        }
                        else if (address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} ");
                        }
                        else if (address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} ");
                        }
                        else
                        {
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} ");
                        }
                    }
                    else
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

                        if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                        {
                            throw new Exception("Memory address could not be resolved");
                        }

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLER + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}");
                        }
                        else if (address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}");
                        }
                        else if (address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}");
                        }
                        else
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLER + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} {ADDRESSES.RAM}");
                        }
                    }
                    break;
                }
            case "SMEQ":
                {
                    // Code for SMALLEREQUAL case
                    if (in_if || in_while)
                    {
                        var _address0 = keywords[1].Trim();
                        var _address1 = keywords[2].Trim();

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

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLEREQUAL + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} ");
                        }
                        else if (address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} ");
                        }
                        else if (address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} ");
                        }
                        else
                        {
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} ");
                        }
                    }
                    else
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

                        if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                        {
                            throw new Exception("Memory address could not be resolved");
                        }

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLEREQUAL + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}");
                        }
                        else if (address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}");
                        }
                        else if (address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}");
                        }
                        else
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.SMALLEREQUAL + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} {ADDRESSES.RAM}");
                        }
                    }
                    break;
                }
            case "GR":
                {
                    // Code for GREATER case
                    if (in_if || in_while)
                    {
                        var _address0 = keywords[1].Trim();
                        var _address1 = keywords[2].Trim();

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

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATER + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} ");
                        }
                        else if (address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} ");
                        }
                        else if (address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} ");
                        }
                        else
                        {
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} ");
                        }
                    }
                    else
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

                        if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                        {
                            throw new Exception("Memory address could not be resolved");
                        }

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATER + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}");
                        }
                        else if (address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}");
                        }
                        else if (address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATER + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}");
                        }
                        else
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATER + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} {ADDRESSES.RAM}");
                        }
                    }
                    break;
                }
            case "GREQ":
                {
                    // Code for GREATEREQUAL case
                    if (in_if || in_while)
                    {
                        var _address0 = keywords[1].Trim();
                        var _address1 = keywords[2].Trim();

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

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATEREQUAL + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} ");
                        }
                        else if (address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} ");
                        }
                        else if (address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} ");
                        }
                        else
                        {
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} ");
                        }
                    }
                    else
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

                        if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                        {
                            throw new Exception("Memory address could not be resolved");
                        }

                        if (!address0Immediate && !address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move second address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_1}{Comment("Move second value to ram-temp-1")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATEREQUAL + CONDITIONS.JUMP_IF_RESULT} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM_TEMP_1} {ADDRESSES.RAM}");
                        }
                        else if (address1Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address0} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move first address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move first value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE1} {ADDRESSES.RAM_TEMP_0} {address1} {ADDRESSES.RAM}");
                        }
                        else if (address0Immediate)
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {address1} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move second address to ram-address-register")}"); // move first address to ram read
                            result.Add($"{ALU.MOVE} 0 0 {ADDRESSES.RAM_TEMP_0}{Comment("Move second value to ram-temp-0")}");

                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATEREQUAL + CONDITIONS.JUMP_IF_RESULT + OP.IMMEDIATE0} {address0} {ADDRESSES.RAM_TEMP_0} {ADDRESSES.RAM}");
                        }
                        else
                        {
                            result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                            result.Add($"{OP.CONDITIONS + CONDITIONS.GREATEREQUAL + OP.IMMEDIATE0 + OP.IMMEDIATE1} {address0} {address1} {ADDRESSES.RAM}");
                        }
                    }
                    break;
                }
            case "AW":
                {
                    // Code for ALLWAYS case
                    if (in_if || in_while)
                    {
                        result.Add($"{OP.CONDITIONS + CONDITIONS.ALLWAYS + OP.IMMEDIATE0 + OP.IMMEDIATE1 + CONDITIONS.JUMP_IF_RESULT} 0 0 ");
                    }
                    else
                    {
                        var _resultAddress = keywords[3].Trim();

                        if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                        {
                            throw new Exception("Memory address could not be resolved");
                        }

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                        result.Add($"{OP.CONDITIONS + CONDITIONS.ALLWAYS + OP.IMMEDIATE0 + OP.IMMEDIATE1} 0 0 {ADDRESSES.RAM}");
                    }
                    break;
                }
            case "NV":
                {
                    // Code for NEVER case
                    if (in_if || in_while)
                    {
                        result.Add($"{OP.CONDITIONS + CONDITIONS.NEVER + OP.IMMEDIATE0 + OP.IMMEDIATE1 + CONDITIONS.JUMP_IF_RESULT} 0 0 ");
                    }
                    else
                    {
                        var _resultAddress = keywords[3].Trim();

                        if (!scopeManager.currentScope.ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                        {
                            throw new Exception("Memory address could not be resolved");
                        }

                        result.Add($"{ALU.MOVE + OP.IMMEDIATE0} {resultAddress} 0 {ADDRESSES.RAM_ADDRESS}{Comment("Move result address to ram-address-register")}");
                        result.Add($"{OP.CONDITIONS + CONDITIONS.NEVER + OP.IMMEDIATE0 + OP.IMMEDIATE1} 0 0 {ADDRESSES.RAM}");
                    }
                    break;
                }
            
        }

        return result;
    }

    public string[] Process(string code)
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

            List<string> compiled = ProcessLine(lineCode, false, false);
            foreach (string item in compiled)
            {
                final.Add(item);
            }
        }

        return final.ToArray();
    }


}
