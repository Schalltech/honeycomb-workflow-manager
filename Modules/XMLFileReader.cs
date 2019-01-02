using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public class XMLFileReader : BaseFileReader
    {
        protected FileStream FStream { get; set; }

        public XMLFileReader()
        { }

        public XMLFileReader(Cache shared_data, XMLFileReader configuration)
            : base(shared_data, configuration)
        {
            Close += OnClose;
        }

        void OnClose(object sender, EventArgs e)
        {
            try
            {
                if (FStream != null)
                {
                    FStream.Close();
                    FStream.Dispose();
                    FStream = null;
                }

                if (LoadedFile != null)
                {
                    ((StreamReader)LoadedFile).Close();
                    ((StreamReader)LoadedFile).Dispose();
                    LoadedFile = null;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected override void OnOpen(object sender, EventArgs e)
        {
            try
            {
                // Load the file into the file readers file object.
                Logger.WriteLine("ColumnarFileReader.OnLoad", "File Path: " + FilePath + FileName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                FStream = new FileStream(FilePath + FileName, FileMode.Open, FileAccess.Read);
                LoadedFile = new StreamReader(FStream);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected override void OnLoad(object sender, EventArgs e)
        {
            int line_index = 0;

            if (File.ColumnContainer != null && File.ColumnContainer.Count > 0)
            {
                // Add the file columns to the CompleteFileContents data table.
                foreach (CacheColumn fileColumn in File.ColumnContainer.Items)
                {
                    CompleteFileContents.Columns.Add(TextParser.Parse(fileColumn.Name, DrivingData, SharedData, ModuleCommands));
                }
            }

            // If the StartRow setting is not zero then progress the LoadedFile to the row specified by StartRow.
            if(StartRow > 1)
            {
                for(int i = 0; i <= StartRow && !((StreamReader)LoadedFile).EndOfStream; i++)
                {
                    line_index += 1;
                    ((StreamReader)LoadedFile).ReadLine();
                }

                // Set StartRow to zero.
                // This is done because the FileReader's base class will process StartRow again. Since we just
                // processed it we need to set it to zero or unexpected results will occur.
                StartRow = 0;
            }

            SetModuleCommand("%FileName%",    TextParser.Parse(File.Name, DrivingData, SharedData, ModuleCommands));
            SetModuleCommand("%FileContent%", ((StreamReader)LoadedFile).ReadToEnd());

            AddResults();

            //if (EndRow <= 0)
            //{
            //    SetModuleCommand("%FileContent%", ((StreamReader)LoadedFile).ReadToEnd());
            //}
            //else
            //{
            //    while (line_index <= EndRow && !((StreamReader)LoadedFile).EndOfStream)
            //    {

            //        ((StreamReader)LoadedFile).ReadLine();

            //        // Begin reading the file.
            //        //((StreamReader)LoadedFile).
            //    }
            //}
        }

        protected override void OnLoadVariables(object sender, EventArgs e)
        {
            AddDefaultModuleVariable("FileName",    "STRING", "%FileName%");
            AddDefaultModuleVariable("FileContent", "STRING", "%FileContent%");

            base.OnLoadVariables(sender, e);
        }
    }
}
