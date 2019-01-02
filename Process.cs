using Schalltech.EnterpriseLibrary.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Modules;

namespace WFM
{
    public class Process
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "Enabled")]
        public string Enabled = "true";

        [XmlElement(ElementName = "TempFileDirectory")]
        public string TempFileDirectory { get; set; }

        [XmlElement(ElementName = "LogCategory")]
        public LogCategory LogCategory { get; set; }

        public Cache SharedData { get; set; }

        [XmlIgnore]
        public Exception LastError { get; set; }

        public List<object> Modules = new List<object>();

        public List<object> PreModules = new List<object>();

        public List<object> PostModules = new List<object>();

        public Process()
        { }

        public Process(Cache shared_data, Process configuration)
        {
            SharedData             = shared_data;
            Name                   = configuration.Name;
            TempFileDirectory      = configuration.TempFileDirectory;
            Enabled                = TextParser.Parse(configuration.Enabled, null, SharedData, null);
            LogCategory            = configuration.LogCategory;

            SharedData.LogCategory       = LogCategory;
            SharedData.TempFileDirectory = TempFileDirectory;
            PreModules                   = configuration.PreModules;
            Modules                      = configuration.Modules;
            PostModules                  = configuration.PostModules;
        }

        private BaseModule InitializeModule(BaseModule configuration)
        {
            BaseModule module = null;

            switch (configuration.GetType().Name)
            {
                // ADD MODULE CONSTRUCTORS HERE
                case "Database":
                    module = new Database(SharedData, (Database)configuration);
                    break;
                case "DataLoader":
                    module = new DataLoader(SharedData, (DataLoader)configuration);
                    break;
                case "XsltWriter":
                    module = new XsltWriter(SharedData, (XsltWriter)configuration);
                    break;
                case "ExcelWriter":
                    module = new ExcelWriter(SharedData, (ExcelWriter)configuration);
                    break;
                case "EmailPoster":
                    module = new EmailPoster(SharedData, (EmailPoster)configuration);
                    break;
                case "Printer":
                    module = new Printer(SharedData, (Printer)configuration);
                    break;
                case "LanCleaner":
                    module = new LanCleaner(SharedData, (LanCleaner)configuration);
                    break;
                case "LanPoster":
                    module = new LanPoster(SharedData, (LanPoster)configuration);
                    break;
                case "LanCollector":
                    module = new LanCollector(SharedData, (LanCollector)configuration);
                    break;
                case "XMLFileReader":
                    module = new XMLFileReader(SharedData, (XMLFileReader)configuration);
                    break;
				case "ExcelReader":
					module = new ExcelReader(SharedData, (ExcelReader)configuration);
					break;
				case "RestService":
					module = new RestService(SharedData, (RestService)configuration);
					break;
				default:
                    throw new Exception("The configured module '" + configuration.Name + "' type '" + configuration.GetType().Name + "' is unknown.");
            }

            return module;
        }

        public void Execute()
        {
            string go_to_module        = null;
            BaseModule current_module  = null;
            string current_module_name = null;

            try
            {
                // Display the process settings.
                Logging.Logger.WriteLine("Process.Execute", "----------------------------------------------------------------------", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                              Process Log                             ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "----------------------------------------------------------------------", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "        PROCESS NAME: " + SharedData.ProcessName,                        System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "        PROCESS TYPE: " + SharedData.ProcessType,                        System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "        CURRENT DATE: " + SharedData.CurrentDate,                        System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "          START DATE: " + SharedData.StartDate,                          System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "            END DATE: " + SharedData.EndDate,                            System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG1: " + SharedData.Arg1,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG2: " + SharedData.Arg2,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG3: " + SharedData.Arg3,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG4: " + SharedData.Arg4,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG5: " + SharedData.Arg5,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG6: " + SharedData.Arg6,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG7: " + SharedData.Arg7,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG8: " + SharedData.Arg8,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "                ARG9: " + SharedData.Arg9,                               System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", "",                                                                       System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                // Ensure the process's temp directory exists and if not attempt to create it.
                if (!string.IsNullOrEmpty(SharedData.TempFileDirectory) && SharedData.TempFileDirectory != @"\")
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(TextParser.Parse(SharedData.TempFileDirectory + @"\", null, SharedData, null));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Unable to create the processes temp directory.", ex);
                    }
                }
                else
                    throw new Exception("The process's TempFileDirectory must be defined.");

                // Execute any modules configured to run before the process.
                for (int i = 0; i < PreModules.Count; i++)
                {
                    InitializeModule((BaseModule)PreModules[i]).Execute();
                }

                for (int i = 0; i < Modules.Count; i++ )
                {
                    // Initialie the next module in the list.
                    current_module = InitializeModule((BaseModule)Modules[i]);
                    current_module_name = current_module.Name;

                    // If we are not supposed to jump to a specified module then execute the current module.
                    // If we are supposed to skip to a specific module then verify the current module is the correct one.
                    if (string.IsNullOrEmpty(go_to_module) || current_module.Name == go_to_module)
                    {
                        current_module.Execute();

                        if (!current_module.ExitProcess)
                            go_to_module = null;
                    }

                    // The current module has completed it's execution.
                    // Check to see if we should continue processing any remaining modules.
                    if (current_module.ExitProcess && string.IsNullOrEmpty(current_module.GoToModule))
                    {
                        // Do not process any remaining modules.
                        break;
                    }
                    // Check to see if we are supposed to skip any modules in the list.
                    else if (!string.IsNullOrEmpty(current_module.GoToModule))
                    {
                        // Store the name of the module we need to jump to.
                        go_to_module = current_module.GoToModule;
                    }
                }

                // Update the process state and status.
                SharedData.ProcessState  = "COMPLETED";
                SharedData.ProcessStatus = "Process was successful.";
            }
            catch (Exception ex)
            {
                // TODO: LOG EXCEPTION TO DB
                // TODO: SEND EMAIL NOTIFICATION HERE
                
                // Update the process error details.
                LastError = ex;
                SharedData.ProcessState  = "COMPLETED";
                SharedData.ProcessStatus = string.Format("Failed: {0}.{1}Please review the process log '{2}' for more details.", LastError.Message, Environment.NewLine, ((Schalltech.EnterpriseLibrary.Logging.RollingFileAppenderConfiguration)SharedData.LogCategory.Appender("Default")).Path + ((Schalltech.EnterpriseLibrary.Logging.RollingFileAppenderConfiguration)SharedData.LogCategory.Appender("Default")).FileName);

                //Logging.Logger.WriteLine("Process.Execute", string.Format("{0} ERROR: {1}",!string.IsNullOrEmpty(current_module_name) ? current_module_name.PadLeft(14) : "".PadLeft(14), ex.ToString()), 1, 1, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logging.Logger.WriteLine("Process.Execute", string.Format("      CRITICAL ERROR: {0}", ex.ToString()), 1, 0, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }
            finally
            {
                try
                {
                    // Execute any modules configured to run after the process.
                    for (int i = 0; i < PostModules.Count; i++)
                    {
                        InitializeModule((BaseModule)PostModules[i]).Execute();
                    }
                }
                catch (Exception)
                { }
                finally
                {
                    Logging.Logger.WriteLine("Process.Execute", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logging.Logger.WriteLine("Process.Execute", "----------------------------------------------------------------------", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logging.Logger.WriteLine("Process.Execute", "                                End Log                               ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logging.Logger.WriteLine("Process.Execute", "----------------------------------------------------------------------", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }
            }
        }
    }

    public class ProcessCollection
    {
        [XmlElement(ElementName="Process")]
        public List<Process> Collection { get; set; }

        public void Add(Process process)
        {
            if (Collection == null)
                Collection = new List<Process>();

            Collection.Add(process);
        }
    }
}
