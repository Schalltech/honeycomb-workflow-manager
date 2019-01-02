using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Modules;

namespace WFM
{
    public class Manager
    {
        [XmlElement(ElementName = "Process")]
        public Process Process { get; set; }

        [XmlElement(ElementName = "ApplicationVariables")]
        public ApplicationVariableCollection ApplicationVariableCollection { get; set; }

        [XmlElement(ElementName = "CommandLineArguments")]
        public CommandLineArgumentCollection CommandLineArgumentCollection { get; set; }

        [XmlElement(ElementName = "PreProcess")]
        public DefaultModules PreProcess { get; set; }

        [XmlElement(ElementName = "PostProcess")]
        public DefaultModules PostProcess { get; set; }

        private TimeSpan OneDay  = new TimeSpan(1, 0, 0, 0);
        private TimeSpan OneWeek = new TimeSpan(7, 0, 0, 0);

        public Cache SharedData = new Cache();

        public DataTable ApplicationVariables
        {
            get
            {
                return ApplicationVariableCollection.DataTable;
            }
        }

        public Manager()
        {

        }

        public Manager(List<string> args, Manager configuration, string process_name, string process_type, string current_date, string start_date, string end_date, 
                        string arg1, string arg2, string arg3, string arg4, string arg5, string arg6, string arg7, string arg8, string arg9)
        {
            ApplicationVariableCollection = configuration.ApplicationVariableCollection;
            CommandLineArgumentCollection = configuration.CommandLineArgumentCollection;
            Process                       = configuration.Process;

            SharedData.Data.Add(configuration.ApplicationVariables);

            SharedData.ProcessDate = DateTime.Now;

            if (!string.IsNullOrEmpty(process_type))
            {
                SharedData.ProcessType = process_type;
            }
            else if (!string.IsNullOrEmpty(configuration.CommandLineArgumentCollection.GetCommandFromArgument("/t").DefaultValue))
            {
                SharedData.ProcessType = configuration.CommandLineArgumentCollection.GetCommandFromArgument("/t").DefaultValue;
            }
            else
                throw new Exception("ProcessType [/t] (Daily,Weekly,etc.) is required to be supplied if configuration does not have a default value.");

            if (!string.IsNullOrEmpty(current_date))
                SharedData.CurrentDate = Convert.ToDateTime(current_date);
            else
                SharedData.CurrentDate = DateTime.Now;

            // Set the start and end dates based on the process type.
            if(SharedData.ProcessType == ProcessTypes.Daily.ToString())
            {
                SharedData.StartDate = SharedData.CurrentDate;
                SharedData.EndDate   = SharedData.CurrentDate;
            }
            else if(SharedData.ProcessType == ProcessTypes.Weekly.ToString())
            {
                SharedData.StartDate = SharedData.CurrentDate.AddDays(-7);
                SharedData.EndDate   = SharedData.CurrentDate;
            }
            else if (SharedData.ProcessType == ProcessTypes.MonthlyFirstToFirst.ToString())
            {
                if(SharedData.CurrentDate.Month == 1)
                {
                    SharedData.StartDate = new DateTime(SharedData.CurrentDate.Year - 1, 12, 1);
                    SharedData.EndDate   = new DateTime(SharedData.CurrentDate.Year, 1, 1);
                }
                else
                {
                    SharedData.StartDate = new DateTime(SharedData.CurrentDate.Year, SharedData.CurrentDate.Month - 1, 1);
                    SharedData.EndDate   = new DateTime(SharedData.CurrentDate.Year, SharedData.CurrentDate.Month, 1);
                }
            }
            else if (SharedData.ProcessType == ProcessTypes.WeeklyFridayToFriday.ToString())
            {
                SharedData.StartDate = GetPreviousDayByDayOfWeek(SharedData.CurrentDate, DayOfWeek.Friday);
                SharedData.EndDate   = SharedData.EndDate.Subtract(OneWeek);
            }
            else if (SharedData.ProcessType == ProcessTypes.WeeklySaturdayToSaturday.ToString())
            {
                SharedData.StartDate = GetPreviousDayByDayOfWeek(SharedData.CurrentDate, DayOfWeek.Saturday);
                SharedData.EndDate   = SharedData.EndDate.Subtract(OneWeek);
            }
            else if (SharedData.ProcessType == ProcessTypes.WeeklySundayToSunday.ToString())
            {
                SharedData.StartDate = GetPreviousDayByDayOfWeek(SharedData.CurrentDate, DayOfWeek.Sunday);
                SharedData.EndDate   = SharedData.EndDate.Subtract(OneWeek);
            }
            else if (SharedData.ProcessType == ProcessTypes.WeeklyMondayToMonday.ToString())
            {
                SharedData.StartDate = GetPreviousDayByDayOfWeek(SharedData.CurrentDate, DayOfWeek.Monday);
                SharedData.EndDate   = SharedData.EndDate.Subtract(OneWeek);
            }
            else if (SharedData.ProcessType == ProcessTypes.YearlyFirstToFirst.ToString())
            {
                SharedData.StartDate = new DateTime(SharedData.CurrentDate.Year - 1, 1, 1);
                SharedData.EndDate   = new DateTime(SharedData.CurrentDate.Year, 1, 1);
            }
            else if (SharedData.ProcessType.ToLower() == ProcessTypes.Adhoc.ToString().ToLower())
            {
                if (!string.IsNullOrEmpty(start_date))
                    SharedData.StartDate = Convert.ToDateTime(start_date);
                else
                    SharedData.StartDate = SharedData.CurrentDate;

                if (!string.IsNullOrEmpty(end_date))
                    SharedData.EndDate = Convert.ToDateTime(end_date);
                else
                    SharedData.EndDate = SharedData.CurrentDate;
            }

            SharedData.ProcessName = process_name;
            SharedData.MachineName = System.Environment.MachineName;

            SharedData.Arg1 = arg1;
            SharedData.Arg2 = arg2;
            SharedData.Arg3 = arg3;
            SharedData.Arg4 = arg4;
            SharedData.Arg5 = arg5;
            SharedData.Arg6 = arg6;
            SharedData.Arg7 = arg7;
            SharedData.Arg8 = arg8;
            SharedData.Arg9 = arg9;
            SharedData.Args = args;
        }

        public void Run()
        {
            Process = new Process(SharedData, Process);
            Process.Execute();

            if (Process.LastError != null)
                throw new Exception(string.Format("{0} - Please review the processes log file for more details.", Process.LastError.Message));
        }

        private DateTime GetPreviousDayByDayOfWeek(DateTime current, DayOfWeek day)
        {
            while (current.DayOfWeek != day)
                current = current.Subtract(OneDay);

            return current;
        }
    }

    public class DefaultModules
    {
        [XmlElement(ElementName = "Modules")]
        public ModuleCollection Modules = new ModuleCollection();
    }

    public class ModuleCollection
    {
        [XmlElement(ElementName = "Module")]
        public List<BaseModule> Items { get; set; }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }

        public int Length
        {
            get
            {
                if (Items != null)
                    return Items.Count - 1;

                return -1;
            }
        }
    }
}
