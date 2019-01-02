using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;

namespace WFM.Modules
{
    [XmlInclude(typeof(Database))]
    public abstract class BaseModule
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Enabled")]
        public string Enabled = "true";

        [XmlElement(ElementName = "ModuleVariables")]
        public ModuleVariableCollection ModuleVariableCollection { get; set; }

        protected event EventHandler PreProcess;
        protected event EventHandler Process;
        protected event EventHandler PostProcess;
        protected event EventHandler LoadVariables;

        /// <summary>
        /// Contains data that will be shared between all modules.
        /// </summary>
        protected Cache SharedData { get; set; }

        protected DataRow DrivingData { get; set; }

        /// <summary>
        /// Contains a list of default variables owned by the module.
        /// </summary>
        /// <remarks>
        /// Module Variables are used to access values externally by other modules. As 
        /// an example, the value of a module's command can be referenced by a variable.
        /// That value can then be accessed externally by another module.
        /// </remarks>
        protected List<ModuleVariable> DefaultModuleVariables { get; set; }

        /// <summary>
        /// Contains a list of commands owned by the module.
        /// </summary>
        /// <remarks>
        /// Module commands are used internally by modules to reference values of 
        /// various settings.
        /// </remarks>
        protected List<StringCommand> ModuleCommands { get; set; }

        public bool ExitProcess { get; set; }

        public string GoToModule { get; set; }

        /// <summary>
        /// Contains the results of the modules operations.
        /// </summary>
        protected DataTable GlobalOutputTable
        {
            get
            {
                if(SharedData.Data.Contains(Name))
                    return SharedData.Data.Tables(Name);

                return null;
            }
        }

        public string GetModuleCommand(string command)
        {
            if(ModuleCommands != null)
            {
                foreach(StringCommand item in ModuleCommands)
                {
                    if (item.Name == command)
                        return item.Value;
                }
            }

            return null;
        }

        public void SetModuleCommand(string command, string value)
        {
            if(ModuleCommands != null)
            {
                for (int i = 0; i < ModuleCommands.Count; i++)
                {
                    if(ModuleCommands[i].Name == command)
                    {
                        ModuleCommands[i].Value = value;
                        return;
                    }
                }

                ModuleCommands.Add(new StringCommand
                {
                    Name  = command,
                    Value = value
                });
            }
            else
            {
                ModuleCommands = new List<StringCommand> 
                { 
                    new StringCommand
                    {
                        Name = command,
                        Value = value
                    }
                };
            }
        }

        protected abstract void OnProcess(object sender, EventArgs e);

        public BaseModule()
        { }

        public BaseModule(Cache shared_data, BaseModule configuration)
        { 
            Process       += OnProcess;
            LoadVariables += OnLoadVariables;

            if (!string.IsNullOrEmpty(configuration.Name))
                Name = configuration.Name;
            else
                throw new Exception("The name setting is missing for a module of type '" + this.GetType().Name + "'. All modules must have their name defined.");

            //if (ModuleCommands == null)
            //    ModuleCommands = new List<StringCommand> { new StringCommand{ Name = "%DateTimeNow%", Value = "" }};

            SharedData = shared_data;
            Enabled = TextParser.Parse(configuration.Enabled, DrivingData, SharedData, ModuleCommands);

            if (LoadVariables != null)
                LoadVariables(this, new EventArgs());
        }

        protected void AddResults()
        {
            AddResults(null);
        }

        /// <summary>
        /// Inserts the current values of the modules variables as a datarow into the
        /// modules global output table.
        /// </summary>   
        protected void AddResults(byte[] buffer)
        {
            DataTable table = null;
            DataRow row = null;

            if(SharedData.Data.Contains(Name))
            {
                table = SharedData.Data.Tables(Name);
                row   = table.NewRow();

                // There should be a matching data column for every variable in the modules output table.
                foreach(ModuleVariable variable in ModuleVariableCollection.Items)
                {
                    foreach(DataColumn column in table.Columns)
                    {
                        // Match the variable name to the data column name and copy the value of the variable
                        // to the column's value.
                        if(variable.Name == column.ColumnName)
                        {
                            if (column.ColumnName == "_Raw_Report_")
                            {
                                row[column.ColumnName] = buffer;
                            }
                            else
                            {
                                // Parse the value of the variable. The value could be plain text, driving values, 
                                // external module variables or internal module commands.
                                try
                                {
                                    row[column.ColumnName] = TextParser.Parse(variable.Value, DrivingData, SharedData, ModuleCommands);
                                    break;
                                }
                                catch (Exception)
                                { }
                            }
                        }
                    }
                }

                // Add the results row to the modules output table.
                SharedData.Data.Tables(Name).Rows.Add(row);
            }
        }

		/// <summary>
		/// Adds a default variable to the module. This is translated into a column on the modules output table
		/// </summary>
		/// <param name="name">The name of the variable/column</param>
		/// <param name="data_type">The data type of the variable/column</param>
		/// <param name="value">The name of the %command% to access the variable</param>
		protected void AddDefaultModuleVariable(string name, string data_type, string value)
        {
            ModuleVariable variable = new ModuleVariable(name, data_type, value);

            if (DefaultModuleVariables == null)
                DefaultModuleVariables = new List<ModuleVariable>();

            DefaultModuleVariables.Add(variable);
        }

        public void Execute()
        {
            try
            {
                DisplayHeader();

                // Trigger the pre-process event.
                if (PreProcess != null)
                    PreProcess(this, new EventArgs());

                // Verify the process is enabled.
                if (Enabled.ToLower() == System.Boolean.TrueString.ToLower())
                {
                    // Trigger the process event.
                    if (Process != null)
                        Process(this, new EventArgs());
                }
                else
                {
                    Logging.Logger.WriteLine("Base.Execute", "Module is disabled.", System.Diagnostics.TraceEventType.Information, 0, 0, SharedData.LogCategory);
                }

                // Trigger the post process event.
                if (PostProcess != null)
                    PostProcess(this, new EventArgs());
            }
            catch (Exception ex)
            {
                Logging.Logger.WriteLine("Base.Execute", ex.Message, System.Diagnostics.TraceEventType.Error, 0, 0, SharedData.LogCategory);
                throw new Exception(ex.Message, ex);
            }
        }

        protected void DisplayHeader()
        {
            string title = null;

            title = (("--- " + this.GetType().Name + " ").PadRight(70, '-'));

            Logging.Logger.WriteLine("BaseModule.DisplayHeader", " ".PadLeft(70), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logging.Logger.WriteLine("BaseModule.DisplayHeader", title.ToUpper(), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logging.Logger.WriteLine("BaseModule.DisplayHeader", " ".PadLeft(70), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logging.Logger.WriteLine("BaseModule.DisplayHeader", " MODULE: " + Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logging.Logger.WriteLine("BaseModule.DisplayHeader", "ENABLED: " + Enabled, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logging.Logger.WriteLine("BaseModule.DisplayHeader", "".PadLeft(70,'-'), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
        }

        /// <summary>
        /// Loads all module variables into memory.
        /// </summary>
        /// <remarks>
        /// Override this method in a derived class to add default variables specific to the derived module.
        /// Call the base classes OnLoadVariables method after setting the derived modules default variables
        /// through the use of the AddDefaultModuleVariable method.
        /// </remarks>
        protected virtual void OnLoadVariables(object sender, EventArgs e)
        {
            // Add any user defined module variables from the configuration file.
            ModuleVariableCollection = new ModuleVariableCollection(Name, DefaultModuleVariables, ((BaseModule)sender).ModuleVariableCollection);

            if (ModuleVariableCollection != null && ModuleVariableCollection.DataTable != null)
                SharedData.Data.Add(ModuleVariableCollection.DataTable);
        }

        public static string encode(string text)
        {
            byte[] mybyte = System.Text.Encoding.UTF8.GetBytes(text);
            string returntext = System.Convert.ToBase64String(mybyte);
            return returntext;
        }
 
        public static string decode(string text)
        {
            byte[] mybyte = System.Convert.FromBase64String(text);
            string returntext = System.Text.Encoding.UTF8.GetString(mybyte);
            return returntext;
        }
    }
}
