using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WFM.Data
{
    public class ApplicationVariableCollection
    {
        [XmlElement(ElementName = "Variable")]
        public List<ApplicationVariable> Items { get; set; }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }

        public DataTable DataTable
        {
            get
            {
                DataTable application_variables = new DataTable("ApplicationVariables");
                DataRow row = null;

                if(Items != null && Items.Count > 0)
                {
                    foreach(ApplicationVariable variable in Items)
                    {
                        application_variables.Columns.Add(new DataColumn 
                        { 
                            ColumnName = variable.Name,
                            DataType = typeof(string)
                        });
                    }

                    row = application_variables.NewRow();

                    foreach(DataColumn column in application_variables.Columns)
                    {
                        foreach(ApplicationVariable variable in Items)
                        {
                            if(variable.Name == column.ColumnName)
                            {
                                row[column.ColumnName] = variable.Value;
                                break;
                            }
                        }
                    }

                    application_variables.Rows.Add(row);
                    return application_variables;
                }

                return null;
            }
        }

        public string Variable(string name)
        {
            DataTable variables = DataTable;

            foreach(DataColumn column in variables.Columns)
            {
                if(column.ColumnName == name)
                {
                    return variables.Rows[0][column].ToString();
                }
            }

            return null;
        }
    }

    public class ApplicationVariable
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }
    }

    public class ModuleVariableCollection
    {
        private string table_name = "";

        [XmlElement(ElementName = "Variable")]
        public List<ModuleVariable> Items { get; set; }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }

        public DataTable DataTable
        {
            get
            {
                DataTable module_variables = new DataTable(table_name);
                DataColumn column = null;

                if (Items != null && Items.Count > 0)
                {
                    
                    foreach(ModuleVariable variable in Items)
                    {
                        column = new DataColumn(variable.Name);
                        column.DataType = System.Type.GetType("System." + variable.DataType, true, true);
                        module_variables.Columns.Add(column);
                    }

                    return module_variables;
                }

                return null;
            }
        }

        public string Variable(string name)
        {
            DataTable variables = DataTable;

            foreach (DataColumn column in variables.Columns)
            {
                if (column.ColumnName == name)
                {
                    return variables.Rows[0][column].ToString();
                }
            }

            return null;
        }

        public ModuleVariableCollection()
        { }

        public ModuleVariableCollection(string module_name, List<ModuleVariable> default_variables, ModuleVariableCollection configuration)
        {
            table_name = module_name;

            Items = default_variables;

            if(configuration != null && configuration.Count > 0)
            {
                foreach(ModuleVariable variable in configuration.Items)
                {
                    foreach(ModuleVariable default_variable in Items)
                    {
                        if (variable.Name == default_variable.Name)
                            throw new Exception("Module variable '" + variable.Name + "' is reserved as a default variable.");
                    }

                    // Add the variable to the list.
                    Items.Add(variable);
                }
            }
        }
    }

    public class ModuleVariable
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }

        [XmlAttribute(AttributeName = "DataType")]
        public string DataType { get; set; }

        public ModuleVariable()
        {}

        public ModuleVariable(string name, string type, string value)
        {
            Name = name;
            DataType = type;
            Value = value;
        }
    }

    public class CommandLineArgumentCollection
    {
        [XmlElement(ElementName = "Command")]
        public List<CommandLineArgument> Items { get; set; }

        public CommandLineArgument GetCommandFromArgument(string argument)
        {
            foreach(CommandLineArgument command in Items)
            {
                if (command.Argument == argument)
                    return command;
            }

            throw new Exception(string.Format("'{0}' is an unknow command line argument.", argument));
        }
    }

    public class CommandLineArgument
    {
        [XmlAttribute(AttributeName = "Argument")]
        public string Argument { get; set; }

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "DataType")]
        public string DataType { get; set; }

        [XmlAttribute(AttributeName = "Required")]
        public bool Required { get; set; }

        [XmlAttribute(AttributeName = "DefaultValue")]
        public string DefaultValue { get; set; }

        [XmlAttribute(AttributeName = "Values")]
        public string Values { get; set; }
    }

    public enum ProcessTypes
    {
        Daily = 1,
        Weekly = 2,
        MonthlyFirstToFirst = 3,
        Adhoc = 4, 
        WeeklyFridayToFriday = 5,
        WeeklySaturdayToSaturday = 6,
        WeeklySundayToSunday = 7,
        WeeklyMondayToMonday = 8,
        YearlyFirstToFirst = 9
    }
}
