using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace WFM
{
    class Program
    {
        static void Main(string[] args)
        {
            XmlDocument doc         = null;
            Manager process_manager = null;
            XmlNode current_node    = null;

            string process_name = null, process_type, current_date, start_date, end_date, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9 = null;

            try
            {
                // Print out the input arguments provided to the process.
                if (args != null)
                    for (int i = 0; i < args.GetUpperBound(0); i++)
                        Console.WriteLine(!string.IsNullOrEmpty(args[i]) ? string.Format("args({0}) = [{1}]", i, args[i]) : string.Format("args({0}) = [{args[{0} is nothing}]]", i));

                if (args.Count() > 0)
                {
                    process_name = GetArgValueByCommand(args, "/p");
                    process_type = GetArgValueByCommand(args, "/t");
                    current_date = GetArgValueByCommand(args, "/c");
                    start_date   = GetArgValueByCommand(args, "/s");
                    end_date     = GetArgValueByCommand(args, "/e");
                    arg1         = GetArgValueByCommand(args, "/arg1");
                    arg2         = GetArgValueByCommand(args, "/arg2");
                    arg3         = GetArgValueByCommand(args, "/arg3");
                    arg4         = GetArgValueByCommand(args, "/arg4");
                    arg5         = GetArgValueByCommand(args, "/arg5");
                    arg6         = GetArgValueByCommand(args, "/arg6");
                    arg7         = GetArgValueByCommand(args, "/arg7");
                    arg8         = GetArgValueByCommand(args, "/arg8");
                    arg9         = GetArgValueByCommand(args, "/arg9");

                    // Verify the process name is defined and display the header.
                    if (!string.IsNullOrEmpty(process_name))
                        DisplayHeader(process_name);
                    else
                        throw new Exception("/p <process name> is required.");

                    // Validate required parameters.
                    if (!string.IsNullOrEmpty(process_type) && process_type.Trim().ToUpper() == "ADHOC" &&
                        (string.IsNullOrEmpty(start_date) || string.IsNullOrEmpty(end_date)))
                    {
                        throw new Exception("/s <start date> /e <end date> arguments are requried when specifying /t adhoc for a process.");
                    }

                    // Load the framework configuration.
                    doc = new XmlDocument();
                    doc.Load(Schalltech.EnterpriseLibrary.IO.Path.GetAbsolutePath(System.Configuration.ConfigurationManager.AppSettings["WFMConfiguration"].ToString()));

                    current_node = doc.SelectSingleNode("Manager").Clone();

                    Manager manager = DeserializeProcessManager(current_node);

                    // Initialize the manager based on its configuration.
                    process_manager = new Manager(args.ToList(), manager, process_name, process_type, current_date, start_date, end_date, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);

                    current_node = doc.SelectSingleNode("Manager/Processes");

                    // Initialize the specified process and add it to the manager.
                    process_manager.Process = DeserializeProcess(process_name, current_node);
                    process_manager.Process.PreModules.AddRange(manager.PreProcess.Modules.Items);
                    process_manager.Process.PostModules.AddRange(manager.PostProcess.Modules.Items);

                    try
                    {
                        // Execute the process.
                        process_manager.Run();
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.WriteLine("Main", string.Format("Process {0}: FAILED - {1}", process_name.ToUpper(), ex.Message), System.Diagnostics.TraceEventType.Error, 2, 0, null);
                    }
                }
                else
                {
                    // TODO: Show help screen output here.
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.WriteLine("Main", "ERROR: " + ex.ToString(), TraceEventType.Error, 0, 0, null);
            }
            finally
            {
                DisplayFooter(process_name);
            }
        }

        static Manager DeserializeProcessManager(XmlNode configuration)
        {
            foreach (XmlNode child in configuration.ChildNodes)
            {
                if(child.Name == "Processes")
                {
                    child.RemoveAll();
                    break;
                }
            }

            return new XmlSerializer(typeof(Manager)).Deserialize(new XmlNodeReader(configuration)) as Manager;
        }

        static Process DeserializeProcess(string name, XmlNode process_collection)
        {
            Process process          = null;
            List<object> modules     = new List<object>();
            XmlReader reader         = null;
            XmlSerializer serializer = null;

            if (process_collection.HasChildNodes)
            {
                foreach(XmlNode process_configuration in process_collection.ChildNodes)
                {
                    if(process_configuration.NodeType == XmlNodeType.Element)
                    {
                        if(process_configuration.Attributes.Count > 0 && process_configuration.Attributes["Name"].Value.ToLower() == name.ToLower())
                        {
                            if(process_configuration.Attributes["Enabled"].Value.ToLower() == "true")
                            {
                                // The requested process has been found.
                                // Access the section of the process configuration that defines its collection of modules.
                                foreach(XmlNode section in process_configuration.ChildNodes)
                                {
                                    if(section.NodeType == XmlNodeType.Element && section.Name.ToLower() == "modules")
                                    {
                                        // Deserialize each of the processes modules.
                                        foreach(XmlNode module_configuration in section.ChildNodes)
                                        {
                                            // Verify we are not accessing a comment.
                                            if(module_configuration.NodeType == XmlNodeType.Element)
                                            {
                                                try
                                                {
                                                    // Attempt to generate a serializer based on the module type.
                                                    serializer = new XmlSerializer(Type.GetType("WFM.Modules." + module_configuration.Name));
                                                }
                                                catch (Exception)
                                                {
                                                    throw new Exception(module_configuration.Name + " is an unsupported module type.");
                                                }

                                                // Deserialize the configuration into an actual module.
                                                using (reader = new XmlNodeReader(module_configuration))
                                                {
                                                    modules.Add(serializer.Deserialize(reader));
                                                    serializer = null;
                                                }
                                            }
                                        }

                                        // The module collection has been deserialized.
                                        break;
                                    }
                                }

                                // Deserialize the process configuration into an actual process.
                                using (reader = new XmlNodeReader(process_configuration))
                                {
                                    serializer = new XmlSerializer(typeof(Process));
                                    process    = (Process)serializer.Deserialize(reader);
                                    serializer = null;

                                    // Add the module collection to the process.
                                    process.Modules = modules;
                                }

                                // The process configuration and it's modules have been instantiated.
                                break;
                            }
                            else
                            {
                                throw new Exception("Process '" + name + "' is currently disabled. Please enable the process and try again.");
                            }
                        }
                        else
                        {
                            if(process_configuration.NextSibling == null)
                            {
                                throw new Exception("Process '" + name + "' was not found.");
                            }
                        }
                    }
                }
            }

            return process;
        }

        static string GetArgValueByCommand(string[] args, string command)
        {
            short index;

            index = (short)Array.IndexOf(args, command);

            if (index >= 0 && !string.IsNullOrEmpty(args[index + 1]))
                return args[index + 1];

            return "";
        }

        static void DisplayHeader(string process_name)
        {
            Console.WriteLine(process_name);
            Logging.Logger.WriteLine("Main", string.Format("Process {0}: STARTING ", process_name.ToUpper()), TraceEventType.Information, 0, 0, null);
        }

        static void DisplayFooter(string process_name)
        {
            Logging.Logger.WriteLine("Main", string.Format("Process {0}: COMPLETED ", process_name.ToUpper()), TraceEventType.Information, 0, 0, null);
        }
    }
}
