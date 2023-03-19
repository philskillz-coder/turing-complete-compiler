using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Loader;
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

    private enum ADDRESSES
    {
        RAM = 0,
        JB_RAM = 1,
        CLOCK = 2,
        RAM_ADDRESS = 3,
        JB_RAM_ADDRESS = 4,
        RAM_TEMP_0 = 5,
        RAM_TEMP_1 = 6,
        JUMP_ADDRESS = 7,
        JB_LATEST = 8
    }

    private class Definition
    {
        public int StartInstruction { get; set; }

        public int EndInstruction{ get; set; }

        public int ResultStartIndex { get; set; }
    }

    private class DefinitionManager
    {
        private readonly Dictionary<string, Definition> definitions = new Dictionary<string, Definition>();
        private readonly List<Definition> openDefinitions = new List<Definition>();
        private readonly List<Definition> openCodeDefinitions = new List<Definition>();

        public DefinitionManager() { }

        public bool ResolveDefinition(string rawDefinition, out Definition? definition)
        {
            return definitions.TryGetValue(rawDefinition, out definition);
        }

        public bool DefinitionExists(string definitionName)
        {
            return definitions.ContainsKey(definitionName);
        }

        public bool CreateDefinition(string name, int startInstruction, int resultStartIndex, out Definition? definition)
        {
            if (definitions.ContainsKey(name))
            {
                definition = null;
                return false;
            }

            definitions[name] = new Definition() {
                StartInstruction = startInstruction,
                ResultStartIndex = resultStartIndex
            };

            definition = definitions[name];
            return true;
        }

        public Definition Latest()
        {
            return definitions.Last().Value;
        }


        public void AddOpenCodeDefinition(Definition definition)
        {
            openCodeDefinitions.Add(definition);
        }

        public Definition LatestOpenCodeDefinition()
        {
            return openCodeDefinitions.Last();
        }

        public void CloseLatestOpenCodeDefinition()
        {
            openCodeDefinitions.RemoveAt(openCodeDefinitions.Count - 1);
        }

        public int OpenCodeDefinitionCount()
        {
            return openCodeDefinitions.Count;
        }


    }

    private class Scope
    {
        private readonly Dictionary<string, int> variables = new Dictionary<string, int>();
        public readonly string name;

        public Scope(string scopeName) {
            name = scopeName;
        }

        public bool VariableExists(string varName)
        {
            return variables.ContainsKey(varName);
        }

        public bool ResolveVarName(string varName, out int resolved)
        {
            return variables.TryGetValue(varName, out resolved);
        }

        public bool ResolveAddress(string rawAddress, out int address, out bool immediate)
        {
            if (rawAddress.StartsWith("$"))
            {
                immediate = false;
                var resolved = ResolveVarName(rawAddress.Substring(1), out address);
                return resolved;
            }
            else if (rawAddress.StartsWith("@"))
            {
                immediate = false;
                bool issResolved = int.TryParse(rawAddress.Substring(1), out address);
                return issResolved;
            }
            immediate = true;
            var isResolved = int.TryParse(rawAddress, out address);
            return isResolved;
        }

        public bool SetVariable(string name, int address)
        {
            if (!variables.ContainsKey(name))
            {
                variables.Add(name, address);
                return false;
            }
            variables[name] = address;
            return true;
        }
    }

    private class ScopeManager
    {
        public Scope currentScope;
        private readonly Dictionary<string, Scope> scopes = new Dictionary<string, Scope>();

        public ScopeManager(Scope scope)
        {
            scopes[scope.name] = scope;
            currentScope = scope;
        }

        public bool SetCurrentScope(Scope scope)
        {
            currentScope = scope;
            return true;
        }

        public bool CreateScope(string name, out Scope? scope)
        {
            if (scopes.ContainsKey(name))
            {
                scope = null;
                return false;
            }

            scopes[name] = new Scope(name);
            scope = scopes[name];
            return true;

        }

        public bool GetScope(string name, out Scope? scope)
        {
            return scopes.TryGetValue(name, out scope);
        }

    }

    private class MemoryManager
    {
        private readonly Dictionary<int, bool> ramAddresses = Enumerable.Range(0, 255).ToDictionary(key => key, value => false);
        private readonly Dictionary<int, bool> callStack = Enumerable.Range(0, 255).ToDictionary(key => key, value => false);
        private readonly Dictionary<int, bool> registerAddresses = Enumerable.Range(3, 11).ToDictionary(key => key, value => false);

        public bool GetNonOccupiedRamAddress(out int? address)
        {
            address = ramAddresses.FirstOrDefault(kv => !kv.Value).Key;
            if (address != null)
            {
                ramAddresses[address.Value] = true;
                return true;
            }
            return false;
        }

    }

    private readonly ScopeManager scopeManager = new ScopeManager(new Scope("global"));
    private readonly DefinitionManager definitionManager = new DefinitionManager();
    private readonly MemoryManager memoryManager = new MemoryManager();
    
    private readonly string code;
    private readonly List<String> result = new List<string>();

    private bool inDefinition = false;
    private int line = 0;
    private int callDepth = 0;


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
                    if (!scopeManager.currentScope.ResolveAddress(value, out int valueAddress, out bool valueImmediate))
                    {
                        throw new Exception("Memory address could not be resolved");
                    }

                    int sourceAddress = -1;
                    if (scopeManager.currentScope.VariableExists(name))
                    {
                        scopeManager.currentScope.SetVariable(name, valueAddress);
                    }
                    else
                    {
                        if (!memoryManager.GetNonOccupiedRamAddress(out int? _sourceAddress) || _sourceAddress == null)
                        {
                            throw new Exception("No memory address available");
                        }

                        sourceAddress = _sourceAddress.Value;
                        scopeManager.currentScope.SetVariable(name, sourceAddress);
                        
                    }

                    result.Add($"{(int)KEYWORDS.ALU_MOVE + (int)KEYWORDS.OP_IMMEDIATE0} {sourceAddress} 0 {(int) ADDRESSES.RAM_ADDRESS}"); // move the source (ram) address to register 0
                    result.Add($"{(int)KEYWORDS.ALU_MOVE + (valueImmediate ? 1 : 0) * (int)KEYWORDS.OP_IMMEDIATE0} {valueAddress} 0 {(int) ADDRESSES.RAM}"); // move the value to ram
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
                        result.Add($"{(int)KEYWORDS.ALU_MOVE + KEYWORDS.OP_IMMEDIATE0} {address0} 0 {(int) ADDRESSES.RAM_ADDRESS}"); // move first address to ram read
                        result.Add($"{(int)KEYWORDS.ALU_MOVE} 0 0 {(int) ADDRESSES.RAM_TEMP_0}");
                        result.Add($"{6 + 128} {address1} 0 {(int) ADDRESSES.RAM_ADDRESS}"); // move second address to ram read
                        result.Add($"{(int)KEYWORDS.ALU_MOVE} 0 0 {(int) ADDRESSES.RAM_TEMP_1}");
                        result.Add($"{(int)KEYWORDS.ALU_MOVE + KEYWORDS.OP_IMMEDIATE0} {resultAddress} 0 {(int)ADDRESSES.RAM_ADDRESS}"); // move result address to ram write
                        result.Add($"{(int)KEYWORDS.ALU_ADD} {(int) ADDRESSES.RAM_TEMP_0} {(int) ADDRESSES.RAM_TEMP_1} {(int) ADDRESSES.RAM}");
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
                        result.Add($"{(int)KEYWORDS.ALU_MOVE + KEYWORDS.OP_IMMEDIATE0} {address0} 0  {(int)ADDRESSES.RAM_ADDRESS}"); // move first address to ram read
                        result.Add($"{(int)KEYWORDS.ALU_MOVE} 0 0 {(int) ADDRESSES.RAM_TEMP_0}");
                        result.Add($"{6 + 128} {address1} 0 {(int) ADDRESSES.RAM_ADDRESS}"); // move second address to ram read
                        result.Add($"{(int)KEYWORDS.ALU_MOVE} 0 0 {(int) ADDRESSES.RAM_TEMP_1}");
                        result.Add($"{(int)KEYWORDS.ALU_MOVE + KEYWORDS.OP_IMMEDIATE0} {resultAddress} 0 {(int) ADDRESSES.RAM_ADDRESS}"); // move result address to ram write
                        result.Add($"{(int)KEYWORDS.ALU_SUBTRACT} {(int) ADDRESSES.RAM_TEMP_0} {(int) ADDRESSES.RAM_TEMP_1} {(int) ADDRESSES.RAM}");
                    }
                    break;
                }

            case "DEF":
                {
                    string name = keywords[1].Trim();
                    int currentPosition = (result.Count+2) * 4;
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
                    int currentPosition = (result.Count+6) * 4;
                    Definition definition = definitionManager.LatestOpenCodeDefinition();
                    definitionManager.CloseLatestOpenCodeDefinition();

                    // inject a jump-to reference before the definition start to jump to the instruction where the definition ends
                    result.Insert(definition.ResultStartIndex, $"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_MOVE + (int) KEYWORDS.OP_IMMEDIATE0} {currentPosition} 0 {(int) ADDRESSES.JUMP_ADDRESS}");
                    result.Insert(definition.ResultStartIndex+1, $"{(int) KEYWORDS.OP_CONDITIONS + (int) KEYWORDS.CONDITIONS_ALLWAYS} 0 0 0");

                    // jump-back to origin
                    result.Add($"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_SUBTRACT + (int) KEYWORDS.OP_IMMEDIATE1} {(int) ADDRESSES.JB_LATEST} 1 {(int) ADDRESSES.JB_LATEST}");
                    result.Add($"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_MOVE} {(int) ADDRESSES.JB_LATEST} 0 {(int) ADDRESSES.JB_RAM_ADDRESS}");
                    
                    result.Add($"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_MOVE} {(int) ADDRESSES.JB_RAM} 0 {(int) ADDRESSES.JUMP_ADDRESS}");
                    result.Add($"{(int) KEYWORDS.OP_CONDITIONS + (int) KEYWORDS.CONDITIONS_ALLWAYS} 0 0 0");
                    
                    break;
                }

            case "CALL":
                {
                    string name = keywords[1].Trim();

                    if (!definitionManager.ResolveDefinition(name, out Definition? definition) || definition == null)
                    {
                        throw new Exception("Definition not found");
                    }

                    result.Add($"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_MOVE} {(int)ADDRESSES.JB_LATEST} 0 {(int) ADDRESSES.JB_RAM_ADDRESS}");
                    result.Add($"{(int)KEYWORDS.OP_ALU + (int)KEYWORDS.ALU_ADD + (int)KEYWORDS.OP_IMMEDIATE1} {(int)ADDRESSES.JB_LATEST} 1 {(int)ADDRESSES.JB_LATEST}");


                    // +3 for 3 extra actions before actually jumping to definition
                    result.Add($"{(int) KEYWORDS.OP_ALU + (int) KEYWORDS.ALU_MOVE + (int) KEYWORDS.OP_IMMEDIATE0} {(result.Count + 4) * 4} 0 {(int) ADDRESSES.JB_RAM}"); //jump-back address
                    result.Add($"{(int)KEYWORDS.OP_ALU + (int)KEYWORDS.ALU_MOVE + (int)KEYWORDS.OP_IMMEDIATE0} {definition.StartInstruction} 0 {(int) ADDRESSES.JUMP_ADDRESS}");
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


}
