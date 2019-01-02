using SpreadsheetGear;
using SpreadsheetGear.Drawing.Printing;
using SpreadsheetGear.Printing;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public class Printer : BaseModule
    {
        [XmlAttribute(AttributeName = "PrinterName")]
        public string PrinterName { get; set; }

        [XmlElement(ElementName = "FileName")]
        public string FileName { get; set; }

        [XmlElement(ElementName = "SourceModule")]
        public CacheTable SourceModule { get; set; }

        public Printer()
        { }

        public Printer(Cache shared_data, Printer configuration) 
            : base(shared_data, configuration)
        {
            Name         = configuration.Name;
            PrinterName  = configuration.PrinterName;
            FileName     = configuration.FileName;
            SourceModule = new CacheTable(SharedData, DrivingData, configuration.SourceModule);
        }

        protected override void OnProcess(object sender, EventArgs e)
        {
            string local_file_name               = null;
            string local_file_full_path          = null;
            FileInfo local_file_info             = null;
            IWorkbook book                       = null;

            try
            {
                try
                {
                    Logger.Aquire();
                    Logger.Write("Printer.OnProcess", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logger.Write("Printer.OnProcess", "     NETWORK PRINTER: " + TextParser.Parse(PrinterName, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                    if (SourceModule != null)
                        Logger.Write("Printer.OnProcess", "       SOURCE MODULE: " + SourceModule.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    else if (!string.IsNullOrEmpty(FileName))
                        Logger.Write("Printer.OnProcess", "                FILE: " + TextParser.Parse(FileName, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    else
                        throw new Exception(string.Format("The printer module '{0}' must have either the source module or the file name setting defined.", TextParser.Parse(Name, DrivingData, SharedData, ModuleCommands)));
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    Logger.Release();
                }

                if (SourceModule != null)
                {
                    foreach (DataRow row in SourceModule.Process().Rows)
                    {
                        DrivingData = row;

                        local_file_name = TextParser.Parse(FileName, DrivingData, SharedData, ModuleCommands);

                        if (!local_file_name.Contains(TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands)))
                        {
                            local_file_full_path = TextParser.Parse(SharedData.TempFileDirectory + local_file_name, DrivingData, SharedData, ModuleCommands);
                        }
                        else
                        {
                            local_file_full_path = local_file_name;
                            local_file_name = local_file_name.Replace(TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands), "");
                        }

                        local_file_info = new FileInfo(local_file_full_path);

                        Logger.Write("Printer.OnProcess", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logger.Write("Printer.OnProcess", "            PRINTING: " + local_file_name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                        if (local_file_info.Extension == ".xlsx" || local_file_info.Extension == ".xls")
                        {
                            book = Factory.GetWorkbook(local_file_full_path);

                            using (WorkbookPrintDocument print_document = new WorkbookPrintDocument(book.Sheets[0], SpreadsheetGear.Printing.PrintWhat.Sheet))
                            {
                                print_document.PrinterSettings.PrinterName = TextParser.Parse(PrinterName, DrivingData, SharedData, ModuleCommands);
                                print_document.Print();

                                Logger.Write("Printer.OnProcess", "             RESULTS: SUCCESS", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            }
                        }
                        else
                        {
                            Logger.Write("Printer.OnProcess", "             RESULTS: FAILED", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            throw new Exception(string.Format("The extension {0} is not currently supported. Unable to print {1}.", local_file_info.Extension, local_file_name));
                        }
                    }
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
