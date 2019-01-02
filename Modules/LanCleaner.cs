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
    public class LanCleaner 
        : BaseModule
    {
        [XmlElement(ElementName = "FileDeletions")]
        public FileDeletionCollection FileDeletionCollection { get; set; }

        protected DataTable DeletedFilesTable { get; set; }

        public LanCleaner()
        { }

        public LanCleaner(Cache shared_data, LanCleaner configuration) 
            : base(shared_data, configuration)
        {
            FileDeletionCollection = configuration.FileDeletionCollection;

            // Clone the module variables table to the table that will contain the files deleted by the module.
            DeletedFilesTable = GlobalOutputTable.Clone();
        }

        protected override void OnProcess(object sender, EventArgs e)
        {
            foreach(FileDeletion file in FileDeletionCollection.Items)
            {
                if(TextParser.Parse(file.Enabled, DrivingData, SharedData, ModuleCommands).ToLower() == bool.TrueString.ToLower())
                {
                    if(TextParser.IsTableCommand(file.FileName, SharedData))
                    {
                        foreach(DataRow row in TextParser.GetCommandTable(file.FileName, SharedData).Select(TextParser.Parse(file.Filter, DrivingData, SharedData, ModuleCommands)))
                        {
                            DrivingData = row;
                            DeleteFile(file.FileName, file.Directory);

                            DrivingData = null;
                        }
                    }
                    else
                    {
                        DeleteFile(file.FileName, file.Directory, file.Filter);
                    }
                }
                else
                {
                    Logger.WriteLine("LanCleaner.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logger.WriteLine("LanCleaner.Process", "   SOURCE DIRECTORY: " + file.FileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logger.WriteLine("LanCleaner.Process", "            ENABLED: " + TextParser.Parse(file.Enabled, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }
            }

            // Update the modules output table to contain all files deleted by this module.
            foreach(DataRow deleted_file in DeletedFilesTable.Rows)
            {
                GlobalOutputTable.Rows.Add(deleted_file.ItemArray);
                GlobalOutputTable.AcceptChanges();
            }
        }

        public void DeleteFile(string current_file_name, string current_directory)
        {
            DeleteFile(current_file_name, current_directory, null);
        }

        public void DeleteFile(string current_file_name, string current_directory, string filter)
        {
            DirectoryInfo current_directory_info = null;

            // Parse the directory and file name using the driving data.
            if(!string.IsNullOrEmpty(current_directory))
            {
                current_directory = TextParser.Parse(current_directory, DrivingData, SharedData, ModuleCommands);
            }
            else
            {
                current_directory = TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands);
            }

            current_file_name = TextParser.Parse(current_file_name, DrivingData, SharedData, ModuleCommands);

            Logger.WriteLine("LanCleaner.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logger.WriteLine("LanCleaner.Process", "    SOURCE DIRECTORY: " + current_directory, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logger.WriteLine("LanCleaner.Process", "      SEARCH PATTERN: " + current_file_name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

            // Verify the directory exists.
            if(System.IO.Directory.Exists(current_directory))
            {
                // Get the directory the cleaner is referencing.
                current_directory_info = new DirectoryInfo(current_directory);

                // Collect the files. If a wildcard is used in the name, multiple files could be returned.
                var file_list = current_directory_info.GetFiles(current_file_name, SearchOption.TopDirectoryOnly).ToList();
                Logger.WriteLine("LanCleaner.Process", "  MATCHED FILE COUNT: " + file_list.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                foreach(FileInfo info in file_list)
                {
                    Logger.WriteLine("LanCleaner.Process", "             MATCHED: " + info.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                    // Add the current file results to the modules global output table.
                    SetModuleCommands(info);
                    AddResults();
                }

                Logger.WriteLine("LanCleaner.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                if(!string.IsNullOrEmpty(filter))
                {
                    Logger.WriteLine("LanCleaner.Process", "              FILTER: " + TextParser.Parse(filter, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }

                foreach(DataRow row in GlobalOutputTable.Select(TextParser.Parse(filter, DrivingData, SharedData, ModuleCommands)))
                {
                    // Delete the files that meet the filter criteria.
                    Logger.WriteLine("LanCleaner.Process", "            DELETING: " + row["FileName"].ToString(), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    System.IO.File.Delete(row["FileFullName"].ToString());

                    // Copy the files the module deleted to the DeleteFilesTable.
                    DeletedFilesTable.Rows.Add(row.ItemArray);
                }

                // Accept the new rows to the table.
                DeletedFilesTable.AcceptChanges();

                // Clear all the rows from the modules global output table.
                // This needs to be done because at this point the table will contain all files that
                // matched the results of CurrentDirectoryInfo.GetFiles and not just the files that
                // met the filter criteria. Once processing is complete for the module the global cache table
                // will be updated with the DeletedFilesTable.
                GlobalOutputTable.Rows.Clear();
            }
        }

        public void SetModuleCommands(FileInfo current_file)
        {
            SetModuleCommand("%FileName%",              current_file.Name);
            SetModuleCommand("%FileCreationTime%",      current_file.CreationTime.ToString());
            SetModuleCommand("%FileCreationTimeUTC%",   current_file.CreationTimeUtc.ToString());
            SetModuleCommand("%FileDirectoryName%",     current_file.DirectoryName);
            SetModuleCommand("%FileExtension%",         current_file.Extension);
            SetModuleCommand("%FileFullName%",          current_file.FullName);
            SetModuleCommand("%FileIsReadonly%",        current_file.IsReadOnly.ToString());
            SetModuleCommand("%FileLastAccessTime%",    current_file.LastAccessTime.ToString());
            SetModuleCommand("%FileLastAccessTimeUTC%", current_file.LastAccessTimeUtc.ToString());
            SetModuleCommand("%FileLastWriteTime%",     current_file.LastWriteTime.ToString());
            SetModuleCommand("%FileLastWriteTimeUTC%",  current_file.LastWriteTimeUtc.ToString());
            SetModuleCommand("%FileLength%",            current_file.Length.ToString());
        }

        protected override void OnLoadVariables(object sender, EventArgs e)
        {
            // Add the default variables for this module and link them to their corresponding commands.
            AddDefaultModuleVariable("FileName",                     "String", "%FileName%");
            AddDefaultModuleVariable("FileLength",                   "String", "%FileLength%");
            AddDefaultModuleVariable("FileCreationTime",             "String", "%FileCreationTime%");
            AddDefaultModuleVariable("FileCreationTimeUTC",          "String", "%FileCreationTimeUTC%");
            AddDefaultModuleVariable("FileDirectoryName",            "String", "%FileDirectoryName%");
            AddDefaultModuleVariable("FileExtension",                "String", "%FileExtension%");
            AddDefaultModuleVariable("FileFullName",                 "String", "%FileFullName%");
            AddDefaultModuleVariable("FileIsReadonly",               "String", "%FileIsReadonly%");
            AddDefaultModuleVariable("FileLastAccessTime",           "String", "%FileLastAccessTime%");
            AddDefaultModuleVariable("FileLastAccessTimeUTC",        "String", "%FileLastAccessTimeUTC%");
            AddDefaultModuleVariable("FileLastWriteTime",            "String", "%FileLastWriteTime%");
            AddDefaultModuleVariable("FileLastWriteTimeUTC",         "String", "%FileLastWriteTimeUTC%");
            AddDefaultModuleVariable("UnzippedChildDirectory",       "String", "%UnzippedChildDirectory%");
            AddDefaultModuleVariable("ZippedParentFileName",         "String", "%ZippedParentFileName%");
            AddDefaultModuleVariable("ZippedParentFileCreationTime", "String", "%ZippedParentFileCreationTime%");

            // File collectors will ALWAYS copy the files to disk in the temp directory.
            AddDefaultModuleVariable("_Memory_Management_", "String", "disk");

            base.OnLoadVariables(sender, e);
        }
    }

    public class FileDeletionCollection
    {
        [XmlElement(ElementName = "FileDeletion")]
        public List<FileDeletion> Items { get; set; }

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

    public class FileDeletion
    {
        string enabled = Boolean.TrueString;

        [XmlAttribute(AttributeName = "FileName")]
        public string FileName { get; set; }

        [XmlAttribute(AttributeName = "Directory")]
        public string Directory { get; set; }

        [XmlAttribute(AttributeName = "Enabled")]
        public string Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                enabled = value;
            }
        }

        [XmlElement(ElementName = "Filter")]
        public string Filter { get; set; }

        public FileDeletion()
        { }
    }
}
