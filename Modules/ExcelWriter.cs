using SpreadsheetGear;
using SpreadsheetGear.Drawing.Printing;
using SpreadsheetGear.Printing;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public class ExcelWriter : BaseModule
    {
        string method = "disk";
        bool allow_empty_report = false;

        [XmlAttribute(AttributeName = "TemplateName")]
        public string TemplateName { get; set; }

        [XmlAttribute(AttributeName = "FileName")]
        public string FileName { get; set; }

        [XmlAttribute(AttributeName = "AllowEmptyReport")]
        public bool AllowEmptyReport
        {
            get
            {
                return allow_empty_report;
            }
            set
            {
                allow_empty_report = value;
            }
        }

        [XmlAttribute(AttributeName = "GoToModuleOnEmptyReport")]
        public string GoToModuleOnEmptyReport { get; set; }

        [XmlAttribute(AttributeName = "Method")]
        public string Method
        {
            get 
            { 
                return method;
            }
            set
            {
                method = value;
            }
        }

        [XmlElement(ElementName = "DrivingTable")]
        public string DrivingTableName { get; set; }

        [XmlElement(ElementName = "Worksheets")]
        public WorksheetCollection WorksheetCollection { get; set; }

        public bool HasTemplate
        {
            get
            {
                if (!string.IsNullOrEmpty(TemplateName))
                    return true;

                return false;
            }
        }

        public ExcelWriter()
        { }

        public ExcelWriter(Cache shared_data, ExcelWriter configuration) 
            : base (shared_data, configuration)
        {
            TemplateName         = configuration.TemplateName;
            FileName             = configuration.FileName;
            AllowEmptyReport     = configuration.AllowEmptyReport;
            Method               = configuration.Method;
            DrivingTableName     = configuration.DrivingTableName;
            WorksheetCollection  = configuration.WorksheetCollection;
        }

        protected void CreateWorkbook()
        {
            IWorkbook workbook = null;
            bool contains_data = false;
            bool has_driving_table = false;
            string driving_table_name = null;
            int current_sheet_number = 0;
            int sheet_number = 0;
            int insert_after = -1;
            bool delete = false;
            int sheet_count = 0;
            Worksheet sheet = null;

            try
            {
                Logger.Aquire();
                Logger.Write("ExcelWriter.CreateWorkbook", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.Write("ExcelWriter.CreateWorkbook", "  CREATEING WORKBOOK: " + TextParser.Parse(FileName, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.Write("ExcelWriter.CreateWorkbook", "              METHOD: " + Method, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.Write("ExcelWriter.CreateWorkbook", "               TABLE: " + TextParser.Parse(Name, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Logger.Release();
            }

            // Store the documents name as a command.
            SetModuleCommand("%FileName%", TextParser.Parse(FileName, DrivingData, SharedData, ModuleCommands));

            // Initialize the workbook.
            workbook = InitializeWorkBook();

            // Create each worksheet and add it to the workbook.
            for(int i = 0; i < WorksheetCollection.Items.Count; i++)
            {
                sheet = WorksheetCollection.Items[i];

                // If the workbook does not have a template defined make sure it has enough room for the current worksheet.
                if(!HasTemplate)
                {
                    while(workbook.Sheets.Count < sheet.SheetNumber)
                    {
                        workbook.Worksheets.Add();
                    }
                }

                delete            = false;
                insert_after      = -1;
                has_driving_table = false;

                if (sheet.Template != null)
                {
                    if (!string.IsNullOrEmpty(sheet.Template.DrivingTableName))
                    {
                        driving_table_name = sheet.Template.DrivingTableName;
                        has_driving_table  = true;
                    }

                    sheet_number = sheet.Template.SheetNumber;
                    insert_after = sheet.Template.InsertAfterSheetNumber;
                    delete       = sheet.Template.Delete;
                }

                sheet_count = 0;

                // If the InsertAfterSheetNumber is not defined, then the sheet should be added at the end.
                if (insert_after > -1)
                    insert_after = workbook.Sheets.Count - 1;

                if (Convert.ToInt32(insert_after) >= workbook.Sheets.Count)
                    throw new Exception("The InsertAfterSheetNumber value is greater than the number of sheets in the workbook.");

                // Check to see if the workbook has a driving table.
                if(has_driving_table)
                {
                    // Verify the driving table exists in the cache set.
                    if (SharedData.Data.Contains(driving_table_name))
                    {
                        foreach(DataRow row in SharedData.Data.Tables(driving_table_name).Rows)
                        {
                            DrivingData = row;

                            current_sheet_number = insert_after + sheet_count + 1;

                            // Add a new sheet to the workbook using the previous sheet as the template.
                            workbook.Sheets[sheet_number].CopyAfter(workbook.Sheets[current_sheet_number - 1]);

                            // Initialize the sheet from its configuration.
                            //sheet = new Worksheet(SharedData, DrivingData, ModuleCommands, DestinationDataTable.CacheTableCollection, sheet);
                            sheet = new Worksheet(SharedData, DrivingData, ModuleCommands, null, sheet);

                            // Process the sheet and add it to the workbook.
                            // If the sheet contains data after it is processed then it will return true.
                            if(sheet.Process(workbook.Worksheets[current_sheet_number]) && !sheet.IgnoreContent)
                            {
                                // The workbook contains data.
                                contains_data = true;
                            }

                            sheet_count += 1;
                        }

                        if (delete)
                            workbook.Sheets[sheet_number].Delete();
                    }
                    else
                        throw new Exception(string.Format("The sheets driving table {0} does not exist in the global cache set.", driving_table_name));
                }
                else
                {
                    // If the sheet number was not specified in the configuration then use the insert_after value in its place.
                    if (sheet.SheetNumber <= -1)
                    {
                        current_sheet_number = insert_after + 1;

                        // Add a new sheet to the workbook using the previous sheet as the template.
                        workbook.Sheets[sheet_number].CopyAfter(workbook.Sheets[current_sheet_number - 1]);
                    }
                    else
                        current_sheet_number = sheet.SheetNumber;

                    // Initialize the sheet from its configuration.
                    //sheet = new Worksheet(SharedData, DrivingData, ModuleCommands, DestinationDataTable.CacheTableCollection, sheet);
                    sheet = new Worksheet(SharedData, DrivingData, ModuleCommands, null, sheet);

                    // Process the sheet and add it to the workbook.
                    // If the sheet contains data after it is processed then it will return true.
                    if (sheet.Process(workbook.Worksheets[current_sheet_number]) && !sheet.IgnoreContent)
                    {
                        // The workbook contains data.
                        contains_data = true;
                    }

                    if (delete)
                        workbook.Sheets[sheet_number].Delete();
                }
            }

            // Select the first sheet in the workbook.
            if (contains_data)
                workbook.Sheets[0].Select();

            if(!AllowEmptyReport || !string.IsNullOrEmpty(GoToModuleOnEmptyReport))
            {
                if (contains_data)
                    Save(workbook);
                else
                {
                    Logger.WriteLine("ExcelWriter.CreateWorkbook", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logger.WriteLine("ExcelWriter.CreateWorkbook", " DISCARDING WORKBOOK: NO DATA", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                    if(!string.IsNullOrEmpty(GoToModuleOnEmptyReport))
                    {
                        Logger.WriteLine("ExcelWriter.CreateWorkbook", "  SKIPPING TO MODULE: " + GoToModuleOnEmptyReport, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        GoToModule = GoToModuleOnEmptyReport;
                    }
                }
            }
            else
            {
                // We dont care if the workbook contains data or not.
                Save(workbook);
            }
        }

        protected void Save(IWorkbook workbook)
        {
            try
            {
                try
                {
                    Logger.Aquire();
                    Logger.Write("ExcelWriter.Save", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logger.Write("ExcelWriter.Save", "     SAVING WORKBOOK: " + TextParser.Parse(FileName, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    Logger.Release();
                }

                if (Method.ToLower() == "disk")
                {
                    // Make sure the temp directory exits.
                    System.IO.Directory.CreateDirectory(TextParser.Parse(SharedData.TempFileDirectory + "/", DrivingData, SharedData, ModuleCommands));

                    // Save the workbook to the temp directory.
                    workbook.FullName = TextParser.Parse(SharedData.TempFileDirectory + "/" + FileName, DrivingData, SharedData, ModuleCommands);
                    workbook.Save();

                    // Add the results to the modules output table.
                    AddResults();

                    Logger.WriteLine("ExcelWriter.Save", "            LOCATION: " + workbook.FullName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }
                else if (Method.ToLower() == "stream")
                {
                    // Save the workbook to memory.
                    using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
                    {
                        workbook.SaveToStream(stream, SpreadsheetGear.FileFormat.Excel8);

                        // Add the results to the modules output table.
                        AddResults(stream.GetBuffer());
                    }

                    Logger.WriteLine("ExcelWriter.Save", "            LOCATION: IN MEMORY", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected override void OnLoadVariables(object sender, EventArgs e)
        {
            AddDefaultModuleVariable("FileName",            "STRING", "%FileName%");
            AddDefaultModuleVariable("_Memory_Management_", "STRING", Method.ToLower());
            AddDefaultModuleVariable("_Raw_Report_",        "BYTE[]", "%RawReport%");

            base.OnLoadVariables(sender, e);
        }

        protected IWorkbook InitializeWorkBook()
        {
            IWorkbookSet workbook_set = null;
            string template_path = null;

            if(!string.IsNullOrEmpty(TemplateName))
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
                    throw new Exception(string.Format("The excel template file '{0}' was not found.", template_path));
            }

            workbook_set = SpreadsheetGear.Factory.GetWorkbookSet();

            // Copy the template to the workbook.
            if (!string.IsNullOrEmpty(TemplateName))
                return workbook_set.Workbooks.Open(TemplateName);
            
            return workbook_set.Workbooks.Add();
        }

        protected override void OnProcess(object sender, EventArgs e)
        {
            if(!string.IsNullOrEmpty(DrivingTableName))
            {
                if (SharedData.Data.Contains(DrivingTableName))
                {
                    foreach (DataRow driving_data in SharedData.Data.Tables(DrivingTableName).Rows)
                    {
                        DrivingData = driving_data;

                        CreateWorkbook();
                    }
                }
                else
                    throw new Exception(String.Format("Driving data table " + DrivingTableName + " is missing from the global cache."));
            }
            else
            {
                CreateWorkbook();
            }
        }
    }

    public class WorksheetCollection
    {
        [XmlElement(ElementName = "Worksheet")]
        public List<Worksheet> Items { get; set; }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }
    }

    public class Worksheet
    {
        [XmlAttribute(AttributeName = "SheetNumber")]
        public int SheetNumber { get; set; }

        [XmlAttribute(AttributeName = "NewSheetName")]
        public string NewSheetName { get; set; }

        [XmlAttribute(AttributeName = "AutoFitColumns")]
        public bool AutoFitColumns { get; set; }

        [XmlAttribute(AttributeName = "MaxColumnWidth")]
        public string MaxColumnWidth { get; set; }

        [XmlAttribute(AttributeName = "StartColumn")]
        public string StartColumn { get; set; }

        [XmlAttribute(AttributeName = "StartRow")]
        public string StartRow { get; set; }

        [XmlAttribute(AttributeName = "MaxRows")]
        public string MaxRows { get; set; }

        [XmlAttribute(AttributeName = "RowToDelete")]
        public string RowToDelete { get; set; }

        [XmlAttribute(AttributeName = "IgnoreContent")]
        public bool IgnoreContent { get; set; }

        [XmlElement(ElementName = "Header")]
        public string Header { get; set; }

        [XmlElement(ElementName = "Footer")]
        public string Footer { get; set; }

        [XmlElement(ElementName = "SourceDataTable")]
        public CacheTable SourceDataTable { get; set; }

        [XmlElement(ElementName = "Formatting")]
        public WorksheetFormatting Formatting { get; set; }

        [XmlElement(ElementName = "ColumnNumberToWrap")]
        public string ColumnNumberToWrap { get; set; }

        [XmlElement(ElementName = "Template")]
        public Template Template { get; set; }

        protected Cache SharedData { get; set; }

        protected DataRow DrivingData { get; set; }

        protected List<StringCommand> ModuleCommands { get; set; }

        protected CacheTableCollection ConditionalTables { get; set; }

        public bool HasSourceTable
        {
            get
            {
                if (SourceDataTable != null)
                    return true;

                return false;
            }
        }

        public Worksheet()
        {}

        public Worksheet(Cache shared_data, DataRow driving_data, List<StringCommand> commands, CacheTableCollection conditional_tables, Worksheet configuration)
        {
            SharedData        = shared_data;
            DrivingData       = driving_data;
            ModuleCommands    = commands;
            ConditionalTables = conditional_tables;

            SheetNumber        = configuration.SheetNumber;
            NewSheetName       = configuration.NewSheetName;
            AutoFitColumns     = configuration.AutoFitColumns;
            MaxColumnWidth     = configuration.MaxColumnWidth;
            StartColumn        = configuration.StartColumn;
            StartRow           = configuration.StartRow;
            MaxRows            = configuration.MaxRows;
            RowToDelete        = configuration.RowToDelete;
            IgnoreContent      = configuration.IgnoreContent;
            Header             = configuration.Header;
            Footer             = configuration.Footer;
            Formatting         = configuration.Formatting;
            ColumnNumberToWrap = configuration.ColumnNumberToWrap;

            SourceDataTable = new CacheTable(SharedData, DrivingData, configuration.SourceDataTable);
        }

        public bool Process(IWorksheet worksheet)
        {
            bool contains_data = false;
            int start_row      = -1;

            try
            {
                Logger.Aquire();
                Logger.Write("Worksheet.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.Write("Worksheet.Process", "PROCESSING WORKSHEET: " + worksheet.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.Write("Worksheet.Process", "       SOURCE MODULE: " + SourceDataTable.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.Write("Worksheet.Process", "              FILTER: " + SourceDataTable.FilterExpression, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                if (StartRow.ToLower() == "%lastusedrow%")
                {
                    start_row = worksheet.UsedRange.RowCount;
                }
                else if (StartRow.ToLower() == "%firstnewrow%")
                {
                    start_row = worksheet.UsedRange.Row + 1;
                }
                else
                    start_row = Convert.ToInt32(StartRow);

                Logger.Write("Worksheet.Process", "    START COLUMN|ROW: " + StartColumn + start_row, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Logger.Release();
            }

            // Rename the worksheet if applicable.
            if(!string.IsNullOrEmpty(NewSheetName))
            {
                Logger.WriteLine("Worksheet.Process", "       CHANGING NAME: " + TextParser.Parse(NewSheetName, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                worksheet.Name = TextParser.Parse(NewSheetName, DrivingData, SharedData, ModuleCommands);

                if (worksheet.Name.Length > 31)
                    throw new Exception("The worksheet name (" + worksheet.Name + ") cannot have more than 31 characters.");
            }

            // If the worksheet does not have a source table defined then return false.
            // Without a source table the worksheet will be empty.
            if (HasSourceTable)
            {
                if (!SharedData.Data.Contains(SourceDataTable.Name))
                    throw new Exception(string.Format("The source data table '{0}' for sheet at index '{1}' was not found in the global cache set.", SourceDataTable.Name, SheetNumber));

                // Load the worksheet with the source table's data.
                if (!Load(SourceDataTable.Process(ConditionalTables), worksheet))
                {
                    // No data wasloaded into the worksheet.
                    contains_data = false;
                }
                else
                {
                    // Perform any required formatting.
                    Format(worksheet);

                    contains_data = true;
                }
            }
            else
                contains_data = false;

            return contains_data;
        }

        public bool Load(DataTable data_source, IWorksheet worksheet)
        {
            if (data_source.Rows.Count <= 0)
            {
                // No data was found in the data source table. This will be an empty worksheet.
                Logger.Write("Worksheet.Process", "DISCARDING WORKSHEET: NO DATA ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                return false;
            }
            else
                Logger.Write("Worksheet.Process", "        LOADING DATA: ROW COUNT " + data_source.Rows.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

            if (string.IsNullOrEmpty(StartColumn))
                throw new Exception(string.Format("SheetNumber [{0}] SheetTableName [{1}] does not contain a StartDataColumn", SheetNumber, SourceDataTable.Name));

            if (string.IsNullOrEmpty(StartRow))
                throw new Exception(string.Format("SheetNumber [{0}] SheetTableName [{1}] does not contain a StartDataRow", SheetNumber, SourceDataTable.Name));

            // Set the sheets header.
            if (!string.IsNullOrEmpty(Header))
                worksheet.PageSetup.CenterHeader = TextParser.Parse(Header, DrivingData, SharedData, ModuleCommands);

            // Set the sheets footer.
            if (!string.IsNullOrEmpty(Footer))
                worksheet.PageSetup.CenterFooter = TextParser.Parse(Footer, DrivingData, SharedData, ModuleCommands);

            // Check the start row for commands.
            if (StartRow.ToLower() == "%lastusedrow%")
            {
                StartRow = worksheet.UsedRange.RowCount.ToString();
            }
            else if (StartRow.ToLower() == "%firstnewrow%")
            {
                StartRow = (worksheet.UsedRange.RowCount + 1).ToString();
            }

            // Check to see if only certain columns are desired.
            if(SourceDataTable.CacheColumnCollection != null && SourceDataTable.CacheColumnCollection.Count > 0)
            {
                object[,] values = null;

                if(!string.IsNullOrEmpty(SourceDataTable.AggregateExpression))
                {
                    values = new object[0, SourceDataTable.CacheColumnCollection.Length];

                    if (SourceDataTable.Aggregate.Expression.ToLower() == "sum")
                    {
                        for (int i = 0; i < SourceDataTable.CacheColumnCollection.Length; i++)
                            values[0, i] = 0;

                        for (int row = 0; row < data_source.Rows.Count; row++)
                        {
                            for (int col = 0; col < SourceDataTable.CacheColumnCollection.Count; col++)
                            {
                                values[0, col] = Convert.ToInt32(values[0, col]) + Convert.ToInt32(data_source.Rows[row][SourceDataTable.CacheColumnCollection.GetColumn(col).Name]);
                            }
                        }
                    }
                    else
                        throw new Exception("DataTableColumnAggregate [" + SourceDataTable.Aggregate.Expression + "] is unknown.");
                }
                else if(SourceDataTable.CacheColumnCollection != null && SourceDataTable.CacheColumnCollection.Count > 0)
                {
                    values = new object[data_source.Rows.Count, SourceDataTable.CacheColumnCollection.Count];

                    for (int row = 0; row < data_source.Rows.Count; row++)
                    {
                        for (int col = 0; col < SourceDataTable.CacheColumnCollection.Count; col++)
                        {
                            values[row, col] = data_source.Rows[row][SourceDataTable.CacheColumnCollection.GetColumn(col).Name];
                        }
                    }
                }
                else
                {
                    values = new object[data_source.Rows.Count - 1, SharedData.Data.Tables(TextParser.Parse(SourceDataTable.Name, DrivingData, SharedData, ModuleCommands)).Columns.Count - 1];

                    for (int row = 0; row < data_source.Rows.Count; row++)
                    {
                        for (int col = 0; col < SharedData.Data.Tables(TextParser.Parse(SourceDataTable.Name, DrivingData, SharedData, ModuleCommands)).Columns.Count; col++)
                        {
                            values[row, col] = data_source.Rows[row][col];
                        }
                    }
                }

                int column_index = worksheet.Cells[StartColumn + StartRow].Column;

                if(values.GetUpperBound(0) == 0 && values.GetUpperBound(1) == 0)
                {
                    worksheet.Cells[Convert.ToInt32(StartRow) - 1, column_index, (Convert.ToInt32(StartRow) - 1) + values.GetUpperBound(0), column_index + values.GetUpperBound(1)].Value = values[0, 0];
                }
                else
                {
                    worksheet.Cells[Convert.ToInt32(StartRow) - 1, column_index, (Convert.ToInt32(StartRow) - 1) + values.GetUpperBound(0), column_index + values.GetUpperBound(1)].Value = values;
                }
            }
            else
            {
                // Set the worksheet to wrap to a new sheet if there is too much data for one sheet.
                worksheet.Cells[StartColumn + StartRow].CopyFromDataTable(data_source, SpreadsheetGear.Data.SetDataFlags.WrapToNewWorksheet | SpreadsheetGear.Data.SetDataFlags.NoColumnHeaders);
            }

            return true;
        }

        public void Format(IWorksheet worksheet)
        {
            if (AutoFitColumns)
                worksheet.UsedRange.Columns.AutoFit();

            // Check to see if formatting has been configured for the worksheet.
            if(Formatting != null)
            {
                // Perform any column formatting.
                if(Formatting.ColumnFormatCollection != null && Formatting.ColumnFormatCollection.Count > 0)
                {
                    foreach(WorksheetColumnFormat column in Formatting.ColumnFormatCollection.Items)
                    {
                        if (!string.IsNullOrEmpty(column.ID) && !string.IsNullOrEmpty(column.Width))
                        {
                            worksheet.UsedRange[column.ID + "1"].ColumnWidth = Convert.ToDouble(column.Width);
                        }
                        else
                            throw new Exception(string.Format("Worksheet '{0}' has column formatting missing an id.", worksheet.Name));
                    }
                }

                // Perform any cell formatting.
                if(Formatting.CellFormatCollection != null && Formatting.CellFormatCollection.Count > 0)
                {
                    foreach(WorksheetCellFormat cell in Formatting.CellFormatCollection.Items)
                    {
                        if(!string.IsNullOrEmpty(cell.Column) && !string.IsNullOrEmpty(cell.Row))
                        {
                            if(cell.Row.ToLower() == "%lastusedrow%")
                            {
                                cell.Row = worksheet.UsedRange.RowCount.ToString();
                            }
                            else if (cell.Row.ToLower() == "%firstnewrow%")
                            {
                                cell.Row = (worksheet.UsedRange.RowCount + 1).ToString();
                            }

                            if(cell.Range != null)
                            {
                                IRange cell_range = worksheet.Cells[Convert.ToInt32(cell.Row) - 1, worksheet.Cells[cell.Column + cell.Row].Column, (cell.Range.RowOffset > 0 ? cell.Range.RowOffset : Convert.ToInt32(cell.Row) - 1), cell.Range.ColumnOffset];

                                if(cell.Range.Merge)
                                    cell_range.Merge();

                                

                                cell_range.Borders[BordersIndex.EdgeBottom].Color = Color.FromArgb(int.Parse(cell.BottomBorderColor, NumberStyles.AllowHexSpecifier));
                            }

                            // Merge cells.
                            //if (!string.IsNullOrEmpty(cell.MergeToColumn))
                            //{
                            //    worksheet.Cells[Convert.ToInt32(cell.Row) - 1, 2, Convert.ToInt32(cell.Row) - 1, 9].Merge();
                            //    worksheet.Cells[Convert.ToInt32(cell.Row) - 1, 2, Convert.ToInt32(cell.Row) - 1, 9].Borders[BordersIndex.EdgeBottom].Color = Color.FromArgb(int.Parse(cell.BottomBorderColor, NumberStyles.AllowHexSpecifier));
                            //}

                            // Set values to cells.
                            if(!string.IsNullOrEmpty(cell.Value))
                            {
                                Logger.WriteLine("Worksheet.Format", "           FORMATING: " + cell.Column + cell.Row, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                                Logger.WriteLine("Worksheet.Format", "       SETTING VALUE: " + TextParser.Parse(cell.Value, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                                worksheet.UsedRange[cell.Column + cell.Row].Value = TextParser.Parse(cell.Value, DrivingData, SharedData, ModuleCommands);
                            }

                            //// Format any borders.
                            //if (!string.IsNullOrEmpty(cell.TopBorderColor))
                            //    worksheet.UsedRange[cell.Column + cell.Row].Borders[BordersIndex.EdgeTop].Color = Color.FromArgb(int.Parse(cell.TopBorderColor, NumberStyles.AllowHexSpecifier));

                            //if (!string.IsNullOrEmpty(cell.LeftBorderColor))
                            //    worksheet.UsedRange[cell.Column + cell.Row].Borders[BordersIndex.EdgeLeft].Color = Color.FromArgb(int.Parse(cell.LeftBorderColor, NumberStyles.AllowHexSpecifier));

                            //if (!string.IsNullOrEmpty(cell.RightBorderColor))
                            //    worksheet.UsedRange[cell.Column + cell.Row].Borders[BordersIndex.EdgeRight].Color = Color.FromArgb(int.Parse(cell.RightBorderColor, NumberStyles.AllowHexSpecifier));

                            //if (!string.IsNullOrEmpty(cell.BottomBorderColor))
                            //    worksheet.UsedRange[cell.Column + cell.Row].Borders[BordersIndex.InsideHorizontal].Color = Color.FromArgb(int.Parse(cell.BottomBorderColor, NumberStyles.AllowHexSpecifier));

                            
                        }
                        else
                            throw new Exception(string.Format("Worksheet '{0}' has cell/row formatting missing an id.", worksheet.Name));
                    }
                }

                // Check MaxRows and delete any extra rows if needed.
                if(!string.IsNullOrEmpty(MaxRows))
                {
                    if(worksheet.UsedRange.RowCount>Convert.ToInt32(MaxRows))
                    {
                        try
                        {
                            Logger.Aquire();
                            Logger.Write("Worksheet.Format", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.Write("Worksheet.Format", "           WORKSHEET: " + worksheet.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.Write("Worksheet.Format", "CURRENT|MAX ROWCOUNT: " + worksheet.UsedRange.RowCount + "|" + Convert.ToInt32(MaxRows), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.Write("Worksheet.Format", "        DELETING ROW: " + RowToDelete, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        { Logger.Release(); }

                        worksheet.UsedRange.Range[Convert.ToInt32(RowToDelete) - 1, worksheet.UsedRange.ColumnCount].EntireRow.Delete();
                    }
                }
            }
        }
    }

    public class WorksheetFormatting
    {
        [XmlElement(ElementName = "Columns")]
        public WorksheetColumnFormatCollection ColumnFormatCollection { get; set; }

        [XmlElement(ElementName = "Cells")]
        public WorksheetCellFormatCollection CellFormatCollection { get; set; }
    }

    public class WorksheetColumnFormatCollection
    {
        [XmlElement(ElementName = "Column")]
        public List<WorksheetColumnFormat> Items { get; set; }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }
    }

    public class WorksheetColumnFormat
    {
        [XmlAttribute(AttributeName = "ID")]
        public string ID { get; set; }

        [XmlAttribute(AttributeName = "Width")]
        public string Width { get; set; }
    }

    public class WorksheetCellFormatCollection
    {
        [XmlElement(ElementName = "Cell")]
        public List<WorksheetCellFormat> Items { get; set; }

        public int Count
        {
            get
            {
                if(Items != null)
                    return Items.Count;

                return 0;
            }
        }
    }

    public class WorksheetCellFormat
    {
        [XmlAttribute(AttributeName = "Column")]
        public string Column { get; set; }

        [XmlAttribute(AttributeName = "Row")]
        public string Row { get; set; }

        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }

        [XmlAttribute(AttributeName = "TopBorderColor")]
        public string TopBorderColor { get; set; }

        [XmlAttribute(AttributeName = "LeftBorderColor")]
        public string LeftBorderColor { get; set; }

        [XmlAttribute(AttributeName = "RightBorderColor")]
        public string RightBorderColor { get; set; }

        [XmlAttribute(AttributeName = "BottomBorderColor")]
        public string BottomBorderColor { get; set; }

        [XmlAttribute(AttributeName = "MergeToColumn")]
        public string MergeToColumn { get; set; }

        [XmlAttribute(AttributeName = "MergeToRow")]
        public string MergeToRow { get; set; }

        [XmlElement(ElementName = "Range")]
        public CellRange Range { get; set; }
    }

    public class CellRange
    {
        [XmlAttribute(AttributeName = "ColumnOffset")]
        public int ColumnOffset { get; set; }

        [XmlAttribute(AttributeName = "RowOffset")]
        public int RowOffset { get; set; }

        [XmlAttribute(AttributeName = "Merge")]
        public bool Merge { get; set; }
    }

    public class Template
    {
        protected int insert_after_sheet_number = -1;

        [XmlAttribute(AttributeName = "SheetNumber")]
        public int SheetNumber { get; set; }

        [XmlAttribute(AttributeName = "InsertAfterSheetNumber")]
        public int InsertAfterSheetNumber 
        {
            get
            {
                return insert_after_sheet_number;
            }
            set
            {
                insert_after_sheet_number = value;
            }
        }

        [XmlAttribute(AttributeName = "Delete")]
        public bool Delete { get; set; }

        [XmlElement(ElementName = "DrivingTableName")]
        public string DrivingTableName { get; set; }
    }
}
