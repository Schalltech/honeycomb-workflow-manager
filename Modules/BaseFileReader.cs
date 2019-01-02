using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public abstract class BaseFileReader : BaseModule
    {
        private int startRow = 1;
        private string delimiter = ",";

        /// <summary>
        /// Row the FileReader begins reading from the file.
        /// </summary>
        [XmlAttribute(AttributeName = "StartRow")]
        public int StartRow
        {
            get
            {
                return startRow;
            }
            set
            {
                startRow = value;
            }
        }

        /// <summary>
        /// Row the FileReader stops reading from the file.
        /// </summary>
        [XmlAttribute(AttributeName = "EndRow")]
        public int EndRow { get; set; }

        [XmlAttribute(AttributeName = "Delimiter")]
        public string Delimiter
        {
            get
            {
                return delimiter;
            }
            set
            {
                delimiter = value;
            }
        }

        [XmlElement(ElementName = "File")]
        public WFM.Data.File File { get; set; }

        [XmlElement(ElementName = "DrivingModule")]
        public string DrivingModule { get; set; }

        protected DataTable CompleteFileContents { get; set; }

        /// <summary>
        /// Contains the loaded file.
        /// </summary>
        protected object LoadedFile { get; set; }

        /// <summary>
        /// Full path to the file being read.
        /// </summary>
        /// <remarks>
        /// The path to the file is derived from the filename. If a path is not found
        /// in the filename then the file path defaults to the owning processes root temporary directory.
        /// </remarks>
        protected string FilePath
        {
            get
            {
                string path = "";

                try
                {
                    path = TextParser.Parse(File.FileName, DrivingData, SharedData, ModuleCommands);

                    if (path.Contains(@"\"))
                        return path.Substring(0, path.LastIndexOf(@"\") + 1);

                    return TextParser.Parse(SharedData.TempFileDirectory, DrivingData, SharedData, ModuleCommands);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        protected string FileName
        {
            get
            {
                string name = "";

                try
                {
                    name = TextParser.Parse(File.FileName, DrivingData, SharedData, ModuleCommands);

                    if (name.Contains(@"\"))
                        return name.Substring(name.LastIndexOf(@"\") + 1);

                    return name;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        protected event EventHandler Open;
        protected event EventHandler Close;
        protected event EventHandler Load;

        public BaseFileReader()
        { }

        public BaseFileReader(Cache shared_data, BaseFileReader configuration)
            : base(shared_data, configuration)
        {
            Open += OnOpen;
            Load += OnLoad;

            StartRow  = configuration.StartRow;
            EndRow    = configuration.EndRow;
            File      = configuration.File;
            Delimiter = configuration.Delimiter;

            CompleteFileContents = new DataTable();
		}

        protected abstract void OnLoad(object sender, EventArgs e);

        protected abstract void OnOpen(object sender, EventArgs e);

        protected override void OnProcess(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(DrivingModule))
                {
                    if (SharedData.Data.Contains(DrivingModule))
                    {
                        foreach (DataRow driving_data in SharedData.Data.Tables(DrivingModule).Rows)
                        {
                            DrivingData = driving_data;

                            ProcessFile();
                        }
                    }
                    else
                        throw new Exception(String.Format("Driving data table " + DrivingModule + " is missing from the global cache."));
                }
                else
                {
                    ProcessFile();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("BaseFileReader.OnProcess", " ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("BaseFileReader.OnProcess", "              STATUS: FAILED", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                throw ex;
            }
        }

        private void ProcessFile()
        {
            DataRow fileContentRow = null;
            DataTable fileContentShell = null;

            // Load the file into the readers file object.
            Logger.WriteLine("BaseFileReader.OnProcess", "             OPENING: " + FileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Open(this, new EventArgs());

            // Load the file into the readers CompleteFileContents data table.
            Load(this, new EventArgs());

			fileContentShell = CompleteFileContents.Clone();
			fileContentShell.TableName = Name;

			// Add the cloned table to the shared data as the modul's output table.
			SharedData.Add(fileContentShell);

			if (EndRow == 0 || EndRow > CompleteFileContents.Rows.Count)
                EndRow = CompleteFileContents.Rows.Count;

            if (StartRow <= CompleteFileContents.Rows.Count)
            {
                // Loop through the CompleteFileContents.Rows until we reach EndRow.
                for (int i = (StartRow - 1); i <= EndRow - 1; i++)
                {
                    // Copy the file's row contents to the reader's output table.
                    fileContentRow = GlobalOutputTable.NewRow();
                    fileContentRow.ItemArray = CompleteFileContents.Rows[i].ItemArray;

                    GlobalOutputTable.Rows.Add(fileContentRow);
                }

                Logger.WriteLine("BaseFileReader.OnProcess", " ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("BaseFileReader.OnProcess", "              STATUS: SUCCESSFULL", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("BaseFileReader.OnProcess", "            ROWCOUNT: " + GlobalOutputTable.Rows.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }
            else
            {
                Logger.WriteLine("BaseFileReader.OnProcess", " ", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("BaseFileReader.OnProcess", "              STATUS: NO DATA", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }

            Close(this, new EventArgs());
        }
    }
}
