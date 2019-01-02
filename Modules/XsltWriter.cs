using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public class XsltWriter : BaseModule
    {
        [XmlAttributeAttribute(AttributeName ="TemplateName")]
        public string TemplateName {get; set;}

        [XmlAttributeAttribute(AttributeName ="TemplateBody")] 
        public string TemplateBody {get; set;}

        [XmlAttributeAttribute(AttributeName ="FileName")] 
        public string FileName {get; set;}

        [XmlAttributeAttribute(AttributeName = "Overwrite")]
        public bool Overwrite { get; set; }

        [XmlAttributeAttribute(AttributeName ="AllowEmptyReport")] 
        public bool AllowEmptyReport {get; set;}

        [XmlAttributeAttribute(AttributeName ="Method")]  
        public string  Method {get; set;}

        [XmlAttributeAttribute(AttributeName ="OutputType")]  
        public string OutputType {get; set;}

        [XmlAttributeAttribute(AttributeName ="EmptyMessage")]  
        public string EmptyMessage {get; set;}

        [XmlElement(ElementName ="Arguments")] 
        public ArgumentContainer ArgumentContainer {get; set;}

        [XmlElement(ElementName ="DrivingTable")] 
        public string DrivingTableName {get; set;}

        [XmlElement(ElementName ="SourceDataTable")]
        public CacheTable SourceDataTable { get; set; }

        [XmlElement(ElementName ="DestinationDataTable")]
        public CacheTable DestinationDataTable { get; set; }

        [XmlAttribute(AttributeName = "GoToModuleOnNoData")]
        public string GoToModuleOnNoData { get; set; }

        public bool HasTemplate
        {
            get
            {
                if (!string.IsNullOrEmpty(TemplateName) || !string.IsNullOrEmpty(TemplateBody))
                    return true;
                else
                    return false;
            }
        }

        public XsltWriter()
        { }

        public XsltWriter(Cache sharedData, XsltWriter configuration) 
            : base(sharedData, configuration)
        {
            TemplateName         = configuration.TemplateName;
            TemplateBody         = configuration.TemplateBody;
            FileName             = configuration.FileName;
            Overwrite            = configuration.Overwrite;
            AllowEmptyReport     = configuration.AllowEmptyReport;
            Method               = configuration.Method;
            OutputType           = configuration.OutputType;
            ArgumentContainer    = configuration.ArgumentContainer;
            DrivingTableName     = configuration.DrivingTableName;
            EmptyMessage         = configuration.EmptyMessage;
            DestinationDataTable = new CacheTable(SharedData, DrivingData, configuration.DestinationDataTable);
            GoToModuleOnNoData   = configuration.GoToModuleOnNoData;

            ModuleCommands = new List<StringCommand>();

            if(configuration.SourceDataTable != null)
            {
                SourceDataTable = new CacheTable(SharedData, DrivingData, configuration.SourceDataTable);
            }
        }

        protected void TransformData()
        {
            MemoryStream xslt_stream, xml_stream;
            XmlTextReader xml_reader;
            XsltSettings xslt_settings;
            XPathDocument doc;
            DataTable DataSource = null;
            XslCompiledTransform xslt;
            XsltArgumentList arg_list = null;
			string template_path = null;

			try
            {
                CreateResultsTable();

                xslt = new XslCompiledTransform();
                xslt_settings = new XsltSettings(true, true);
                xslt_stream = new MemoryStream();
                xml_stream = new MemoryStream();

                if (HasTemplate)
                {
                    Logger.WriteLine("XsltWriter.TransformData", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                    // Determine if we are using an XSLT template on disk or if we are getting it from another module.
                    if (!string.IsNullOrEmpty(TemplateName))
                    {
						// Parse the templates path.
						template_path = TextParser.Parse(TemplateName, DrivingData, SharedData, ModuleCommands);

						// Check for the file expecting relative path.
						if (System.IO.File.Exists(System.IO.Path.GetFullPath(template_path)))
						{
							TemplateName = System.IO.Path.GetFullPath(template_path);
						}
						// Check for the file from absolute path.
						else if (System.IO.File.Exists(template_path))
						{
							TemplateName = template_path;
						}
						// Check for the file two layers up incase we are in the bin/debug|release folder.
						else if (System.IO.File.Exists(@"..\..\" + template_path))
						{
							TemplateName = System.IO.Path.GetFullPath(@"..\..\" + template_path);
						}
						else
							throw new Exception(string.Format("The XSLT template file '{0}' was not found.", template_path));

						Logger.WriteLine("XsltWriter.TransformData", "PHYSICAL STYLE SHEET: ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                        // Load the style sheet from the referenced path.
                        xslt.Load(TextParser.Parse(TemplateName, DrivingData, SharedData, ModuleCommands), xslt_settings, null);
                    }
                    else
                    {
                        Logger.WriteLine("XsltWriter.TransformData", " DYNAMIC STYLE SHEET: ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                        // Load the style sheet from the referenced module.
                        xslt.Load(new XmlTextReader(new StringReader(TextParser.Parse(TemplateBody, DrivingData, SharedData, ModuleCommands))), xslt_settings, null);
                    }

					if (SourceDataTable != null)
                    {
                        Logger.WriteLine("XsltWriter.TransformData", "          DATASOURCE: " + TextParser.Parse(SourceDataTable.Name, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        //Logger.WriteLine("XsltWriter.TransformData", "              FILTER: " + SourceDataTable.FilterExpression, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                        // Get the data source table.
                        DataSource = SourceDataTable.Process(DestinationDataTable.CacheTableCollection);
                    }

                    // Check to see if the XSLT writer has any arguments defined.
                    if(ArgumentContainer != null && ArgumentContainer.Count > 0)
                    {
                        Logger.WriteLine("XsltWriter.TransformData", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logger.WriteLine("XsltWriter.TransformData", "      ARGUMENT COUNT: " + ArgumentContainer.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                        arg_list = new XsltArgumentList();

                        // Load the argument list.
                        foreach(Argument arg in ArgumentContainer.Arguments)
                        {
                            Logger.WriteLine("XsltWriter.TransformData", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.WriteLine("XsltWriter.TransformData", "       ARGUMENT NAME: " + arg.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.WriteLine("XsltWriter.TransformData", "               VALUE: " + TextParser.Parse(arg.Value, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                            arg_list.AddParam(arg.Name, "", TextParser.Parse(arg.Value, DrivingData, SharedData, ModuleCommands));
                        }
                    }

                    if((DataSource != null && DataSource.Rows.Count > 0) || AllowEmptyReport)
                    {
                        if (DataSource == null)
                            DataSource = new DataTable("NOT_USED");

                        if(AllowEmptyReport && !string.IsNullOrEmpty(EmptyMessage) && DataSource.Rows.Count == 0)
                        {
                            DataSource.Columns.Add(new DataColumn("Message", typeof(string)));

                            var row = DataSource.NewRow();
                            row["Message"] = EmptyMessage;
                            DataSource.Rows.Add(row);
                        }

                        // Get the data from the source table in XML form.
                        DataSource.WriteXml(xml_stream);

                        // Make sure we are looking at the begining of our data stream.
                        xml_stream.Position = 0;

                        // Load the xml from our xml stream into our xml reader.
                        xml_reader = new XmlTextReader(xml_stream);

                        // Create an xml document from the xml reader.
                        doc = new XPathDocument(xml_reader);

                        // Transform the xml document with the xslt template and load it into our xslt_stream.
                        xslt.Transform(doc, arg_list, xslt_stream);

                        xslt_stream.Flush();
                        xslt_stream.Position = 0;

                        Logger.WriteLine("XsltWriter.TransformData", "", TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logger.WriteLine("XsltWriter.TransformData", "    CREATE TRANSFORM: SUCCESSFULL", TraceEventType.Information, 2, 0, SharedData.LogCategory);

                        Save(xslt_stream);
                    }
                    else
                    {
                        Logger.WriteLine("XsltWriter.TransformData", "", TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logger.WriteLine("XsltWriter.TransformData", "DISCARDING TRANSFORM: NO DATA", TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    }
                }
                else
                    throw new Exception("XSLT writer must have a template defined.");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected override void OnProcess(object sender, EventArgs e)
        {
            // Check to see if the writer has a driving table defined. When a driving table is 
            // used against the writer, a transform is created for EACH item in the driving table.
            if (!string.IsNullOrEmpty(DrivingTableName))
            {
                // A driving table is defined in the config. Make sure it exists in the global dataset.
                if (SharedData.Data.Contains(DrivingTableName))
                {
                    if (SharedData.Data.Tables(DrivingTableName).Rows.Count > 0)
                    {
                        // Loop through the driving table and create a transform based on each row of the driving table.
                        foreach (DataRow row in SharedData.Data.Tables(DrivingTableName).Rows)
                        {
                            DrivingData = row;

                            // Re-initialize the source data table so it will use the current driving data.
                            if (SourceDataTable != null)
                                SourceDataTable = new CacheTable(SharedData, DrivingData, SourceDataTable);

                            TransformData();

                            DrivingData = null;
                        }
                    }
                    else
                    {
                        Logger.WriteLine("XsltWriter.TransformData", "", TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logger.WriteLine("XsltWriter.TransformData", "DISCARDING TRANSFORM: NO DATA", TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        GoToModule = GoToModuleOnNoData;
                    }
                }
                else
                    throw new Exception("Driving data table " + DrivingTableName + " is missing from the global dataset.");
            }
            else
                TransformData();
        }

        protected void Save(MemoryStream input_stream)
        {
            FileStream file            = null;
            StreamWriter writer_stream = null;
            ASCIIEncoding ascii        = new ASCIIEncoding();
            StreamReader reader_stream = null;

            int count = 1;
            string file_name;
            string file_extension;
            string full_path;
            string directory;
            bool renamed_file = false;

            try
            {
                Logger.WriteLine("XsltWriter.TransformData", "", TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("XsltWriter.TransformData", "    SAVING TRANSFORM", TraceEventType.Information, 2, 0, SharedData.LogCategory);

                // See if we are saving the file to a temp directory.
                if (Method.ToLower() == "disk")
                {
                    // Make sure a file name has been defined.
                    if (!string.IsNullOrEmpty(FileName))
                    {
                        // Get the full path to the new file.
                        full_path = TextParser.Parse(SharedData.TempFileDirectory + "/" + TextParser.Parse(FileName, DrivingData, SharedData, ModuleCommands), DrivingData, SharedData, ModuleCommands);
                        directory = Path.GetDirectoryName(full_path);

                        Logger.WriteLine("XsltWriter.TransformData", "           FILE NAME: " + Path.GetFileName(full_path), TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logger.WriteLine("XsltWriter.TransformData", "            LOCATION: " + directory, TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        
                        // Check to see if files can be overwritten. Default is false.
                        if (!Overwrite)
                        {
                            // Get the file name without the extension.
                            file_name = Path.GetFileNameWithoutExtension(full_path);

                            // Get the extension of the file.
                            file_extension = Path.GetExtension(full_path);

                            // Check to see if a file with the same name already exists.
                            while (System.IO.File.Exists(full_path))
                            {
                                renamed_file = true;

                                // Update the file name to include the count. Ex: myFile(2).txt
                                full_path = Path.Combine(directory, string.Format("{0}({1}){2}", file_name, Convert.ToString(count++), file_extension));
                            }

                            if(renamed_file)
                            {
                                Logger.WriteLine("XsltWriter.TransformData", "        RENAMED FILE: " + Path.GetFileName(full_path), TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            }
                        }

                        // Set the commands for other modules to access.
                        SetModuleCommand("%FileName%", Path.GetFileName(full_path));
                        SetModuleCommand("%FileFullName%", full_path);

                        // Make sure the temp directory exists.
                        if (!System.IO.Directory.Exists(TextParser.Parse(SharedData.TempFileDirectory + "/", DrivingData, SharedData, ModuleCommands)))
                            System.IO.Directory.CreateDirectory(TextParser.Parse(SharedData.TempFileDirectory + "/", DrivingData, SharedData, ModuleCommands));

                        // Write the file to disk.
                        file = System.IO.File.Create(full_path);

                        // Create the stream to write to the file.
                        writer_stream = new System.IO.StreamWriter(file, System.Text.Encoding.ASCII);

                        // Read the data from the input stream and encode it as ascii.
                        reader_stream = new System.IO.StreamReader(input_stream, System.Text.Encoding.ASCII);

                        writer_stream.Flush();

                        writer_stream.Write(reader_stream.ReadToEnd());
                    }
                    else
                        throw new Exception("The file name attribute must be provided to save the XSLT writers transform to disk.");
                }
                else
                    Logger.WriteLine("XsltWriter.TransformData", "            LOCATION: IN MEMORY", TraceEventType.Information, 2, 0, SharedData.LogCategory);

                QueReport(input_stream.GetBuffer());
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                input_stream.Close();
                input_stream.Dispose();
                input_stream = null;

                if(reader_stream != null)
                {
                    reader_stream.Close();
                    reader_stream.Dispose();
                    reader_stream = null;
                }

                if(writer_stream != null)
                {
                    writer_stream.Close();
                    writer_stream.Dispose();
                    writer_stream = null;
                }

                if(file != null)
                {
                    file.Close();
                    file.Dispose();
                    file = null;
                }
            }
        }

        public string GetStaticCommand(string name)
        {
            if (ModuleCommands != null)
            {
                foreach (StringCommand command in ModuleCommands)
                {
                    if (command.Name == name)
                        return command.Value;
                }
            }
            else if (name == "%DateTimeNow%")
            {
                return DateTime.Now.ToString();
            }

            return null;
        }

        public void SetStaticCommand(string name, string value)
        {
            if(ModuleCommands != null)
            {
                foreach(StringCommand command in ModuleCommands)
                {
                    if(command.Name == name)
                    {
                        command.Value = value;
                        return;
                    }
                }
            }
            else
                ModuleCommands = new List<StringCommand>();

            ModuleCommands.Add(new StringCommand { Name = name, Value = value });
        }

        protected void CreateResultsTable()
        {
            string results_table_name;

            try
            {
                if (Method.ToLower() == "disk" || Method.ToLower() == "stream")
                {
                    // Parse the destination table name for commands.
                    results_table_name = TextParser.Parse(DestinationDataTable.Name, DrivingData, SharedData, ModuleCommands);

                    // Check to see if the destination table has already been created and added to the global dataset.
                    if (!SharedData.Data.Contains(results_table_name))
                    {
                        // Make sure the memory management column exist in the destination table.
                        if (!DestinationDataTable.CacheColumnCollection.Contains("_Memory_Management_"))
                            DestinationDataTable.CacheColumnCollection.Add("_Memory_Management_", "STRING", Method.ToLower());

                        // If We are streaming the report from memory, make sure the raw report column exists in the destination table.
                        if (Method.ToLower() == "stream" && !DestinationDataTable.CacheColumnCollection.Contains("_Raw_Report_"))
                            DestinationDataTable.CacheColumnCollection.Add("_Raw_Report_", "BYTE()", "%RawReport%");

                        // Add the destination table to the cache.
                        SharedData.Add(results_table_name, DestinationDataTable);
                    }
                }
                else
                    throw new Exception("Method[" + Method + "] is not recognized.");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected void QueReport(byte[] buffer)
        {
            DataRow dr = SharedData.Data.Tables(DestinationDataTable.Name).NewRow();

            for (int i = 0; i < DestinationDataTable.CacheColumnCollection.Items.Count; i++ )
            {
                // Check to see if the command is to add the files raw data as bytes.
                // * This command can not be parsed because it would require any file accessed by 
                // * the application to be opened and stored in memory.
                if (DestinationDataTable.CacheColumnCollection.Items[i].Value.ToUpper() == "%RAWREPORT%")
                    dr[DestinationDataTable.CacheColumnCollection.Items[i].Name] = buffer;
                else
                    dr[DestinationDataTable.CacheColumnCollection.Items[i].Name] = TextParser.Parse(DestinationDataTable.CacheColumnCollection.Items[i].Value, DrivingData, SharedData, ModuleCommands);
            }

            SharedData.Data.Tables(DestinationDataTable.Name).Rows.Add(dr);
        }
    }

    public class ArgumentContainer
    {
        [XmlElement(ElementName = "Argument")]
        public List<Argument> Arguments { get; set; }

        public int Count
        {
            get
            {
                if (Arguments != null)
                    return Arguments.Count;
                else
                    return 0;
            }
        }

        public int Length
        {
            get
            {
                if (Arguments != null)
                    return Arguments.Count - 1;
                else
                    return -1;
            }
        }
    }

    public class Argument
    {
        [XmlAttributeAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlAttributeAttribute(AttributeName = "Value")]
        public string Value { get; set; }
    }
}
