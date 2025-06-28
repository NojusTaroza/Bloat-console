using System;

namespace BloatConsole
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ConsoleCommandAttribute : Attribute
    {
        public string CommandName { get; }
        public string Description { get; }
        public string Category { get; }

        public ConsoleCommandAttribute(string commandName, string description = "", string category = "General")
        {
            CommandName = commandName;
            Description = description;
            Category = category;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ConsoleVariableAttribute : Attribute
    {
        public string VariableName { get; }
        public string Description { get; }
        public string Category { get; }
        public bool ReadOnly { get; }

        public ConsoleVariableAttribute(string variableName, string description = "", string category = "Variables", bool readOnly = false)
        {
            VariableName = variableName;
            Description = description;
            Category = category;
            ReadOnly = readOnly;
        }
    }
}