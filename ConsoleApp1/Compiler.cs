using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace compiler;

internal class Compiler
{
    private enum KEYWORDS
    {
        ALU_ADD = 0,
        ALU_SUBTRACT = 1,
        ALU_AND = 2,
        ALU_OR = 3,
        ALU_NOT = 4,
        ALU_XOR = 5,
        ALU_MOVE = 6,

        CONDITIONS_EQUAL = 0,
        CONDITIONS_NOTEQUAL = 1,
        CONDITIONS_SMALLER = 2,
        CONDITIONS_SMALLEREQUAL = 3,
        CONDITIONS_GREATER = 4,
        CONDITIONS_GREATEREQUAL = 5,
        CONDITIONS_ALLWAYS = 6,
        CONDITIONS_NEVER = 7,

        OP_IMMEDIATE0 = 128,
        OP_IMMEDIATE1 = 64,
        OP_ALU = 0,
        OP_CONDITIONS = 16,
        OP_SHIFTLEFT = 32,
        OP_SHIFTRIGHT = 48
    }

    private class Definition
    {
        public int StartInstruction { get; set; }

        public int EndInstruction{ get; set; }

        public int ResultStartIndex { get; set; }

       


    }

    private readonly Dictionary<string, int> variables = new Dictionary<string, int>();
    private readonly Dictionary<int, bool> ramAddresses = Enumerable.Range(0, 255).ToDictionary(key => key, value => false);
    private readonly Dictionary<int, bool> registerAddresses = Enumerable.Range(3, 11).ToDictionary(key => key, value => false);
    private readonly Dictionary<string, Definition> definitions = new Dictionary<string, Definition>();
    private readonly string code;
    private readonly List<String> result = new List<string>();

    private bool inDefinition = false;
    private int line = 0;

    // address 0: ram
    // address 1: ram address
    // address 2: clock
    // address 3: ram temp
    // address 4: ram temp
    // address 5: jump addresses
    // address 6: jump-back address

    public Compiler(string code)
    {
        this.code = code;
        Process();
    }

    private void ProcessLine(string line)
    {
        string[] keywords = line.Split(" ");
        switch (keywords[0].Trim())
        {
            case "VAR":
                {
                    // $VAR variable
                    // $ADDR address
                    // 0123 normal

                    string name = keywords[1].Trim();
                    string value = keywords[2].Trim();

                    // get value for variable
                    if (!ResolveAddress(value, out int valueAddress, out bool valueImmediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    int sourceAddress = -1;
                    if (VariableExists(name))
                    {
                        sourceAddress = variables[name];

                    }
                    else
                    {
                        if (!GetNonOccupiedMemoryAddress(out int? _sourceAddress) || _sourceAddress == null)
                        {
                            throw new Exception("No memory address available");
                        }

                        sourceAddress = _sourceAddress.Value;
                        variables.Add(name, sourceAddress);
                    }

                    result.Add($"{(int)KEYWORDS.ALU_MOVE + (int)KEYWORDS.OP_IMMEDIATE0} {sourceAddress} 0 2"); // move the source (ram) address to register 0
                    result.Add($"{(int)KEYWORDS.ALU_MOVE + (valueImmediate ? 1 : 0) * (int)KEYWORDS.OP_IMMEDIATE0} {valueAddress} 0 0"); // move the value to ram
                    break;
                }

            case "ADD":
                {
                    var _address0 = keywords[1].Trim();
                    var _address1 = keywords[2].Trim();
                    var _resultAddress = keywords[3].Trim();

                    // get value for variable
                    if (!ResolveAddress(_address0, out int address0, out bool address0Immediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    // get value for variable
                    if (!ResolveAddress(_address1, out int address1, out bool address1Immediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    // get value for variable
                    if (!ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    if (!address0Immediate && !address1Immediate) // both in ram
                    {
                        // todo: use register addresses
                        result.Add($"{(int)KEYWORDS.ALU_MOVE + KEYWORDS.OP_IMMEDIATE0} {address0} 0 2"); // move first address to ram read
                        result.Add($"{(int)KEYWORDS.ALU_MOVE} 0 0 3");
                        result.Add($"{6 + 128} {address1} 0 2"); // move second address to ram read
                        result.Add($"{(int)KEYWORDS.ALU_MOVE} 0 0 4");
                        result.Add($"{(int)KEYWORDS.ALU_MOVE + KEYWORDS.OP_IMMEDIATE0} {resultAddress} 0 2"); // move result address to ram write
                        result.Add($"{(int)KEYWORDS.ALU_ADD} 3 4 0");
                    }
                    break;
                }

            case "SUB":
                {
                    var _address0 = keywords[1].Trim();
                    var _address1 = keywords[2].Trim();
                    var _resultAddress = keywords[3].Trim();

                    // get value for variable
                    if (!ResolveAddress(_address0, out int address0, out bool address0Immediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    // get value for variable
                    if (!ResolveAddress(_address1, out int address1, out bool address1Immediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    // get value for variable
                    if (!ResolveAddress(_resultAddress, out int resultAddress, out bool resultImmediate) || resultImmediate)
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    if (!address0Immediate && !address1Immediate) // both in ram
                    {
                        // todo: use register addresses
                        result.Add($"{(int)KEYWORDS.ALU_MOVE + KEYWORDS.OP_IMMEDIATE0} {address0} 0 2"); // move first address to ram read
                        result.Add($"{(int)KEYWORDS.ALU_MOVE} 0 0 3");
                        result.Add($"{6 + 128} {address1} 0 2"); // move second address to ram read
                        result.Add($"{(int)KEYWORDS.ALU_MOVE} 0 0 4");
                        result.Add($"{(int)KEYWORDS.ALU_MOVE + KEYWORDS.OP_IMMEDIATE0} {resultAddress} 0 2"); // move result address to ram write
                        result.Add($"{(int)KEYWORDS.ALU_SUBTRACT} 3 4 0");
                    }
                    break;
                }
            case "DEF":
                {
                    string name = keywords[1].Trim();
                    if (DefinitionExists(name))
                    {
                        throw new Exception("A definition with this name already exists");
                    }

                    int currentPosition = (result.Count+2) * 4;
                    inDefinition = true;
                    definitions.Add(name, new Definition()
                    {
                        StartInstruction = currentPosition,
                        ResultStartIndex = result.Count
                    });


                    break;
                }
            case "ENDDEF":
                {
                    if (!inDefinition)
                    {
                        throw new Exception("Not in a definition");
                    }

                    inDefinition = false;

                    int currentPosition = (result.Count+4) * 4;
                    Definition definition = definitions.Last().Value;

                    // inject a jump-to reference before the definition start to jump to the instruction where the definition ends
                    result.Insert(definition.ResultStartIndex, $"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_MOVE + (int) KEYWORDS.OP_IMMEDIATE0} {currentPosition} 0 5");
                    result.Insert(definition.ResultStartIndex+1, $"{(int) KEYWORDS.OP_CONDITIONS + (int) KEYWORDS.CONDITIONS_ALLWAYS} 0 0 0");

                    // jump-back to origin
                    result.Add($"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_MOVE} 6 0 5");
                    result.Add($"{(int) KEYWORDS.OP_CONDITIONS + (int) KEYWORDS.CONDITIONS_ALLWAYS} 0 0 0");
                    
                    break;
                }
            case "CALL":
                {
                    string name = keywords[1].Trim();

                    if (!ResolveDefinition(name, out Definition definition))
                    {
                        throw new Exception("Definition not found");
                    }

                    result.Add($"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_MOVE + (int) KEYWORDS.OP_IMMEDIATE0} {(result.Count + 3) * 4} 0 6"); //jump-back address
                    result.Add($"{(int)KEYWORDS.OP_ALU + (int)KEYWORDS.ALU_MOVE + (int)KEYWORDS.OP_IMMEDIATE0} {definition.StartInstruction} 0 5");
                    result.Add($"{(int) KEYWORDS.OP_CONDITIONS + (int) KEYWORDS.CONDITIONS_ALLWAYS} 0 0 0");

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

    private bool ResolveAddress(string rawAddress, out int address, out bool immediate)
    {
        if (rawAddress.StartsWith("$"))
        {
            immediate = false;
            var resolved = ResolveVarName(rawAddress.Substring(1), out address);
            return resolved;
        } else if (rawAddress.StartsWith("@"))
        {
            immediate = false;
            bool issResolved = int.TryParse(rawAddress.Substring(1), out address);
            return issResolved;
        }
        immediate = true;
        var isResolved  = int.TryParse(rawAddress, out address);
        return isResolved;
    }

    private bool ResolveDefinition(string rawDefinition, out Definition definition)
    {
        return definitions.TryGetValue(rawDefinition, out definition);
    }

    private bool GetNonOccupiedMemoryAddress(out int? address)
    {
        address = ramAddresses.FirstOrDefault(kv => !kv.Value).Key;
        if (address != null)
        {
            ramAddresses[address.Value] = true;
            return true;
        }
        return false;
    }        

    private bool VariableExists(string varName)
    {
        return variables.ContainsKey(varName);
    }

    private bool DefinitionExists(string definitionName)
    {
        return definitions.ContainsKey(definitionName);
    }

    private bool ResolveVarName(string varName, out int resolved)
    {
        return variables.TryGetValue(varName, out resolved);
    }

}
