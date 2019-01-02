using EnterpriseLibrary.Utilities;
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
    public abstract class BaseFileCollector : BaseModule
    {
        /// <summary>
        /// The directory to collect files from.
        /// </summary>
        [XmlAttributeAttribute(AttributeName="Directory")] public string Directory = "";

        /// <summary>
        /// Defines the sub directory of the temporary directory the file will be collected to.
        /// </summary>
        [XmlAttributeAttribute(AttributeName="SubDirectory")] public string SubDirectory = "";

        //[XmlAttributeAttribute(AttributeName="Folder")> public Folder As String = ""
        [XmlAttributeAttribute(AttributeName="MaxCount")] public int MaxCount = 0;
        [XmlAttributeAttribute(AttributeName="ExitOnNotFound")] public bool ExitOnNotFound = false;
        [XmlAttributeAttribute(AttributeName="ExitOnNotFoundAndGotoModule")] public string ExitOnNotFoundAndGotoModule;
        [XmlAttributeAttribute(AttributeName="GotoModuleOnNotFound")] public string GotoModuleOnNotFound;
        [XmlElement(ElementName="CheckDirectories")] public DirectoryContainer DirectoryContainer;
        [XmlElement(ElementName="SourceFiles")] public SourceFileContainer SourceFiles;

        private int NumberOfCollectedFiles = 0;

        /// <summary>
        /// Datatable containing the list of files to collect.
        /// </summary>
        protected DataTable RemoteFileList;

        protected abstract DataTable GetRemoteFileList(SourceFile SourceFile);
        protected abstract FileInfo CollectRemoteFile(DataRow FileName);

        public BaseFileCollector() 
            : base()
        {
            
        }

        public BaseFileCollector(Cache shared_data, BaseFileCollector Configuration) 
            : base(shared_data, Configuration)
        {
            Directory                   = Configuration.Directory;
            SourceFiles                 = Configuration.SourceFiles;
            MaxCount                    = Configuration.MaxCount;
            ExitOnNotFound              = Configuration.ExitOnNotFound;
            ExitOnNotFoundAndGotoModule = Configuration.ExitOnNotFoundAndGotoModule;
            GotoModuleOnNotFound        = Configuration.GotoModuleOnNotFound;

            if(!string.IsNullOrEmpty(GotoModuleOnNotFound))
                ExitOnNotFoundAndGotoModule = GotoModuleOnNotFound;

            if(!string.IsNullOrEmpty(ExitOnNotFoundAndGotoModule))
                ExitOnNotFound = true;
        }

        public void SetModuleCommands(FileInfo CurrentFile, FileInfo ParentFile,string Source)
        {
            string TempDirectory = TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands);

            SetModuleCommand("%FileName%",                CurrentFile.Name);
            SetModuleCommand("%FileCreationTime%",        CurrentFile.CreationTime.ToString());
            SetModuleCommand("%FileCreationTimeUTC%",     CurrentFile.CreationTimeUtc.ToString());
            SetModuleCommand("%FileDirectoryName%",       CurrentFile.DirectoryName);
            SetModuleCommand("%SourceFileDirectoryName%", Source);
            SetModuleCommand("%FileExtension%",           CurrentFile.Extension);

            if(!string.IsNullOrEmpty(CurrentFile.Extension) && CurrentFile.Extension.Length > 0)
                SetModuleCommand("%FileNameWithoutExtension%", CurrentFile.Name.Substring(0, CurrentFile.Name.Length - CurrentFile.Extension.Length));
            else
                SetModuleCommand("%FileNameWithoutExtension%", "");

            SetModuleCommand("%FileFullName%",          CurrentFile.FullName);
            SetModuleCommand("%FileIsReadonly%",        CurrentFile.IsReadOnly.ToString());
            SetModuleCommand("%FileLastAccessTime%",    CurrentFile.LastAccessTime.ToString());
            SetModuleCommand("%FileLastAccessTimeUTC%", CurrentFile.LastAccessTimeUtc.ToString());
            SetModuleCommand("%FileLastWriteTime%",     CurrentFile.LastWriteTime.ToString());
            SetModuleCommand("%FileLastWriteTimeUTC%",  CurrentFile.LastWriteTimeUtc.ToString());
            SetModuleCommand("%FileLength%",            CurrentFile.Length.ToString());

            if(ParentFile != null)
            {

                SetModuleCommand("%UnzippedChildDirectory%",       (CurrentFile.FullName.Replace(TempDirectory, "")).Replace(CurrentFile.Name, ""));
                SetModuleCommand("%ZippedParentFileName%",         ParentFile.Name);
                SetModuleCommand("%ZippedParentFileCreationTime%", ParentFile.CreationTime.ToString());
            }
            else
            {
                SetModuleCommand("%UnzippedChildDirectory%",       "");
                SetModuleCommand("%ZippedParentFileName%",         null);
                SetModuleCommand("%ZippedParentFileCreationTime%", DateTime.MinValue.ToString());
            }
        }

        protected bool StopCollecting
        {
            get
            {
                try 
	            {	        
		            if(MaxCount > 0)
                    {
                        if(NumberOfCollectedFiles >= MaxCount)
                            return true;
                    }
	            }
	            catch (Exception ex)
	            {
		            throw ex;
	            }

                return false;
            }
        }

        protected override void OnProcess(object sender, EventArgs e)
        {
 	        if(SourceFiles != null)
            {
                // Make sure the temp directory already exists.
                System.IO.Directory.CreateDirectory(TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands));

                // Grab each defined file in the collectors source file list.
                foreach(SourceFile remote_file in SourceFiles.List)
                {
                    if(!StopCollecting)
                    {
                        if(TextParser.IsTableCommand(remote_file.Name, SharedData))
                        {
                            foreach(DataRow row in TextParser.GetCommandTable(remote_file.Name, SharedData).Rows)
                            {
                                // Set the current row from the table as the Driving Data.
                                DrivingData = row;

                                // ContinueProcessing returns false if max number of files threshold has been reached.
                                if (!ContinueProcessing(remote_file))
                                    break;
                            }
                        }
                        else
                        {
                            // Continue processing the current remote file.
                            ContinueProcessing(remote_file);
                        }
                    }
                    else
                    {
                        // The collector has already collected the maximum number of files
                        // allowed.
                        Logger.WriteLine("BaseFileCollector.OnProcess", "COLLECTION THRESHOLD: Collector//s MaxCount [" + MaxCount + "] threshold has been reached.", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    }
                }

                // See if the process should exit if no files were collected.
                if(NumberOfCollectedFiles <= 0)
                {
                    if(ExitOnNotFound)
                    {
                        ExitProcess = true;
                        GoToModule = ExitOnNotFoundAndGotoModule;
                    }
                }
            }
            else
            {
                throw new Exception("No source files are defined for File Collector [" + Name + "].");
            }
        }

        protected bool ContinueProcessing(SourceFile remote_file)
        {
            FileInfo collected_file;

            try
            {
                Logger.WriteLine("BaseFileCollector.OnProcess", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("BaseFileCollector.OnProcess", "    SOURCE DIRECTORY: " + TextParser.Parse(Directory, DrivingData, SharedData, ModuleCommands), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("BaseFileCollector.OnProcess", "      SEARCH PATTERN: " + remote_file.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                // Get a list of file(s) that match the source file.
                RemoteFileList = GetRemoteFileList(remote_file);
                
                Logger.WriteLine("BaseFileCollector.OnProcess", "  MATCHED FILE COUNT: " + RemoteFileList.Rows.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                if(base.ExitProcess)
                {
                    if (string.IsNullOrEmpty(base.GoToModule))
                        Logger.WriteLine("BaseFileCollector.OnProcess", "         ExitProcess: " + base.ExitProcess.ToString(), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    else
                    {
                        Logger.WriteLine("BaseFileCollector.OnProcess", "         ExitProcess: " + base.ExitProcess.ToString(), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        Logger.WriteLine("BaseFileCollector.OnProcess", "          GotoModule: " + base.GoToModule, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    }
                }

                foreach(DataRow matching_file in RemoteFileList.Rows)
                {
                    if(!StopCollecting)
                    {
                        // Collect the file and place it in the temporary directory.
                        collected_file = CollectRemoteFile(matching_file);

                        // Increment the collected files counter.
                        NumberOfCollectedFiles += 1;

                        SetModuleCommands(collected_file, null, matching_file["FileDirectoryName"].ToString());

                        AddResults();

                        if(collected_file.Extension == ".zip" && remote_file.Unzip)
                        {
                            // Unzip the file into the temp directory and return a list of the child files.
                            Logger.WriteLine("BaseFileCollector.OnProcess", "      UNZIPPING FILE: " + collected_file.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                            foreach (FileInfo unzipped_file in Zip.Decompress(collected_file.FullName, TextParser.Parse(SharedData.TempFileDirectory + @"\" + collected_file.Name.Replace(collected_file.Extension, ""), DrivingData, SharedData, ModuleCommands)))
                            {
                                SetModuleCommands(unzipped_file, collected_file, matching_file["FileDirectoryName"].ToString());

                                AddResults();
                            }
                        }
                    }
                    else
                    {
                        // The collector has already collected the maximum number of files allowed.
                        Logger.WriteLine("BaseFileCollector.OnProcess", "COLLECTION THRESHOLD: Collector's MaxCount [" + MaxCount + "] threshold has been reached.", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("The network path was not found."))
                {
                    throw new Exception(string.Format("{0} Module '{1}' was unable to resolve the network path'{2}'.", GetType().Name, Name, TextParser.Parse(Directory, DrivingData, SharedData, ModuleCommands)), ex);
                }

                throw ex;
            }
        }

        protected override void OnLoadVariables(object sender, EventArgs e)
        {
            //Add the default variables for this module and link them to their corresponding commands.
            AddDefaultModuleVariable("FileName",                     "String", "%FileName%");
            AddDefaultModuleVariable("FileLength",                   "String", "%FileLength%");
            AddDefaultModuleVariable("FileCreationTime",             "String", "%FileCreationTime%");
            AddDefaultModuleVariable("FileCreationTimeUTC",          "String", "%FileCreationTimeUTC%");
            AddDefaultModuleVariable("FileDirectoryName",            "String", "%FileDirectoryName%");
            AddDefaultModuleVariable("SourceFileDirectoryName",      "String", "%SourceFileDirectoryName%");
            AddDefaultModuleVariable("FileExtension",                "String", "%FileExtension%");
            AddDefaultModuleVariable("FileNameWithoutExtension",     "String", "%FileNameWithoutExtension%");
            AddDefaultModuleVariable("FileFullName",                 "String", "%FileFullName%");
            AddDefaultModuleVariable("FileIsReadonly",               "String", "%FileIsReadonly%");
            AddDefaultModuleVariable("FileLastAccessTime",           "String", "%FileLastAccessTime%");
            AddDefaultModuleVariable("FileLastAccessTimeUTC",        "String", "%FileLastAccessTimeUTC%");
            AddDefaultModuleVariable("FileLastWriteTime",            "String", "%FileLastWriteTime%");
            AddDefaultModuleVariable("FileLastWriteTimeUTC",         "String", "%FileLastWriteTimeUTC%");
            AddDefaultModuleVariable("UnzippedChildDirectory",       "String", "%UnzippedChildDirectory%");
            AddDefaultModuleVariable("ZippedParentFileName",         "String", "%ZippedParentFileName%");
            AddDefaultModuleVariable("ZippedParentFileCreationTime", "String", "%ZippedParentFileCreationTime%");

            //File collectors will ALWAYS copy the files to disk in the temp directory.
            AddDefaultModuleVariable("_Memory_Management_", "String", "disk");

            base.OnLoadVariables(sender, e);
        }
    }
}
