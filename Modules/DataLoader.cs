using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WFM.Data;
using WFM.Logging;

namespace WFM.Modules
{
    public class DataLoader : BaseModule
    {
        [XmlAttributeAttribute(AttributeName = "MaxBuckets")]
        public int MaxBuckets { get; set; }

        [XmlAttributeAttribute(AttributeName = "ReaderCount")]
        public int ReaderCount { get; set; }

        [XmlAttributeAttribute(AttributeName = "MaxFileReads")]
        public int MaxFileReads { get; set; }

        [XmlAttributeAttribute(AttributeName = "TrimSetting")]
        public string TrimSetting { get; set; }

        [XmlAttributeAttribute(AttributeName = "LineTrimSetting")]
        public string LineTrimSetting { get; set; }

        [XmlAttributeAttribute(AttributeName = "Delimeter")]
        public string Delimeter { get; set; }

        [XmlElement(ElementName = "Filter")]
        public string ModuleFilter { get; set; }

        [XmlElement(ElementName = "SkipLine")]
        public int SkipLine { get; set; }

        [XmlElement(ElementName = "File")]
        public WFM.Data.File File { get; set; }

        [XmlElement(ElementName = "Bucket")]
        public Bucket BaseBucket { get; set; }

        [XmlElement(ElementName = "WriterFactory")]
        public WriterFactory WriterFactory { get; set; }

        protected FileInfo CurrentFile
        {
            get
            {
                return currentFile;
            }
            set
            {
                currentFile = value;

                //SetModuleCommand("%FileName%",              currentFile.Name);
                //SetModuleCommand("%FileCreationTime%",      currentFile.CreationTime.ToString());
                //SetModuleCommand("%FileCreationTimeUTC%",   currentFile.CreationTimeUtc.ToString());
                //SetModuleCommand("%FileDirectoryName%",     currentFile.DirectoryName);
                //SetModuleCommand("%FileExtension%",         currentFile.Extension);
                //SetModuleCommand("%FileFullName%",          currentFile.FullName);
                //SetModuleCommand("%FileIsReadonly%",        currentFile.IsReadOnly.ToString());
                //SetModuleCommand("%FileLastAccessTime%",    currentFile.LastAccessTime.ToString());
                //SetModuleCommand("%FileLastAccessTimeUTC%", currentFile.LastAccessTimeUtc.ToString());
                //SetModuleCommand("%FileLastWriteTime%",     currentFile.LastWriteTime.ToString());
                //SetModuleCommand("%FileLastWriteTimeUTC%",  currentFile.LastWriteTimeUtc.ToString());
                //SetModuleCommand("%FileLength%",            currentFile.Length.ToString());
            }
        }

        protected StreamReader FileStreamReader;

        protected object FileLock = new object();

        protected Bucket CurrentBucket;

        protected int WriterThreadPool;

        protected int ReaderThreadPool;

        protected DataQueue BucketQueue;

        protected DataQueue ParsedQueue;

        protected FileInfo currentFile;

        protected bool CurrentFileEOF;

        protected bool LoaderThreadsComplete;

        protected long CurrentFileLineBeingRead;

        private static object padLock = new object();

        private static bool stopThreads = false;

        public static bool StopThreads
        {
            get
            {
                lock (padLock)
                {
                    return stopThreads;
                }
            }
            private set
            {
                lock (padLock)
                {
                    stopThreads = value;
                }
            }
        }

        #region Constructors 

        public DataLoader()
        { }

        public DataLoader(Cache sharedData, DataLoader configuration)
            : base(sharedData, configuration)
        {
            MaxBuckets      = configuration.MaxBuckets;
            ReaderCount     = configuration.ReaderCount;
            MaxFileReads    = configuration.MaxFileReads;
            TrimSetting     = configuration.TrimSetting;
            LineTrimSetting = configuration.LineTrimSetting;
            Delimeter       = configuration.Delimeter;
            ModuleFilter    = configuration.ModuleFilter;
            SkipLine        = configuration.SkipLine;
            File            = configuration.File;
            BaseBucket      = configuration.BaseBucket;
            WriterFactory   = configuration.WriterFactory;
        }

        #endregion

        protected override void OnProcess(object sender, EventArgs e)
        {
            DataTable file_table;

            BucketQueue = new DataQueue(SharedData, MaxBuckets);
            ParsedQueue = new DataQueue(SharedData, MaxFileReads);
            
            if(File != null && !string.IsNullOrEmpty(File.Name))
            {
                if(TextParser.IsTableCommand(File.Name, SharedData))
                {
                    if(SharedData.Data.Contains(File.Name.Substring(0, File.Name.IndexOf(".")).Replace("%","")))
                    {
                        // Reference the table containing the list of files.
                        file_table = SharedData.Data.Tables(File.Name.Substring(0, File.Name.IndexOf(".")).Replace("%",""));

                        // Check the table for the column the config is referencing.
                        if(file_table.Columns.Contains(File.Name.Substring(File.Name.IndexOf(".") + 1).Replace("%", "")))
                        {
                            Logger.WriteLine("DataLoader.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.WriteLine("DataLoader.Process", "           FILE LIST: " + file_table.TableName, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                            Logger.WriteLine("DataLoader.Process", "          FILE COUNT: " + file_table.Rows.Count, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                            // Process each file from the source module output table.
                            foreach(DataRow row in file_table.Select(ModuleFilter))
                            {
                                DrivingData = row;

                                // Get the file from the temp directory.
                                CurrentFile = new FileInfo(TextParser.Parse(SharedData.TempFileDirectory + @"\" + File.Name, DrivingData, SharedData, ModuleCommands));

                                // Persist zip information if exists.
                                if(file_table.Columns.Contains("ZippedParentFileCreationTime"))
                                {
                                    SetModuleCommand("%ZippedParentFileCreationTime%", DrivingData["ZippedParentFileCreationTime"].ToString());
                                }

                                if(file_table.Columns.Contains("ZippedParentFileName"))
                                {
                                    SetModuleCommand("%ZippedParentFileName%", DrivingData["ZippedParentFileName"].ToString());
                                }

                                Logger.WriteLine("DataLoader.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                                Logger.WriteLine("DataLoader.Process", "     PROCESSING FILE: " + CurrentFile.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                                // Load the data from the file.
                                ProcessFile();

                                CurrentFile = null;
                                DrivingData = null;
                            }
                        }
                    }
                }
                else
                {
                    // Get the file from the temp directory.
                    CurrentFile = new FileInfo(TextParser.Parse(SharedData.TempFileDirectory + @"\" + File.Name, DrivingData, SharedData, ModuleCommands));

                    Logger.WriteLine("DataLoader.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logger.WriteLine("DataLoader.Process", "     PROCESSING FILE: " + CurrentFile.Name, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

                    // Load the data from the file.
                    ProcessFile();

                    CurrentFile = null;
                }
            }
            else
            {
                Logger.WriteLine("DataLoader.Process", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("DataLoader.Process", "     PROCESSING FILE: NO FILES FOUND", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            }
        }

        private void HandleThreadExceptions(Action method, out Exception e)
        {
            try
            {
                e = null;
                method.Invoke();
            }
            catch (Exception ex)
            {
                e = ex;

                DataLoader.StopThreads = true;
                
                //StopThreads = true;
                //ExitProcess = true;
            }
        }

        protected void ProcessFile()
        {
            List<Thread> reader_threads = new List<Thread>();
            List<Thread> writer_threads = new List<Thread>();

            Thread loader_thread;
            FileStream file_stream;

            Exception threadEx = null;

            CurrentFileEOF = false;
            WriterThreadPool = 0;
            ReaderThreadPool = 0;

            // Set module command values.
            SetModuleCommand("%FileStartReadTime%",     DateTime.Now.ToString());
            SetModuleCommand("%FileName%",              CurrentFile.Name);
            SetModuleCommand("%FileCreationTime%",      CurrentFile.CreationTime.ToString());
            SetModuleCommand("%FileCreationTimeUTC%",   CurrentFile.CreationTimeUtc.ToString());
            SetModuleCommand("%FileExtension%",         CurrentFile.Extension);
            SetModuleCommand("%FileFullName%",          CurrentFile.FullName);
            SetModuleCommand("%FileIsReadonly%",        CurrentFile.IsReadOnly.ToString());
            SetModuleCommand("%FileLastAccessTime%",    CurrentFile.LastAccessTime.ToString());
            SetModuleCommand("%FileLastAccessTimeUTC%", CurrentFile.LastAccessTimeUtc.ToString());
            SetModuleCommand("%FileLastWriteTime%",     CurrentFile.LastWriteTime.ToString());
            SetModuleCommand("%FileLastWriteTimeUTC%",  CurrentFile.LastWriteTimeUtc.ToString());
            SetModuleCommand("%FileLength%",            CurrentFile.Length.ToString());

            // Set the stream reader object.
            file_stream = new FileStream(CurrentFile.FullName, FileMode.Open, FileAccess.Read);
            FileStreamReader = new StreamReader(file_stream);

            // If start line is defined then move the reader to that line in the file.
            if(SkipLine > 0)
            {
                for(int line = 0; line < SkipLine; line++)
                {
                    FileStreamReader.ReadLine();
                }
            }

            CurrentBucket = new Bucket(BaseBucket);

            Logger.WriteLine("DataLoader.ProcessFile", "      READER WORKERS: " + ReaderCount, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            Logger.WriteLine("DataLoader.ProcessFile", "      WRITER WORKERS: " + WriterFactory.MaxWorkerCount, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);

            // Start the reader threads.
            for(int i = 0; i < ReaderCount; i++)
            {
                reader_threads.Add(new Thread(() => HandleThreadExceptions(() => ReaderThreads(), out threadEx)));
                reader_threads.Last().Start();

                //try
                //{
                //    Logger.Aquire();
                //    Logger.Write("DataLoader.ProcessFile", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logger.WriteLine("DataLoader.ProcessFile", "   READER THREAD [" + i + "]: STARTED", 1, 0, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                //}
                //catch (Exception e)
                //{
                //    throw e;
                //}
                //finally
                //{
                //    Logger.Release();
                //}

                // Check for any errors on the thread.
                if (threadEx != null)
                {
                    throw threadEx;
                }
            }

            // Start the loader thread.
            loader_thread = new Thread(() => HandleThreadExceptions(() => LoaderThread(), out threadEx));
            loader_thread.Start();

            //try
            //{
            //    Logger.Aquire();
            //    Logger.Write("DataLoader.ProcessFile", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("DataLoader.ProcessFile", "   LOADER THREAD [0]: STARTED", 1, 0, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
            //}
            //catch (Exception e)
            //{
            //    throw e;
            //}
            //finally
            //{
            //    Logger.Release();
            //}

            // Start the reader threads.
            for (int i = 0; i < WriterFactory.MaxWorkerCount; i++)
            {
                writer_threads.Add(new Thread(() => HandleThreadExceptions(() => WriterThreads(), out threadEx)));
                writer_threads.Last().Start();

                //try
                //{
                //    Logger.Aquire();
                //    Logger.Write("DataLoader.ProcessFile", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.WriteLine("DataLoader.ProcessFile", "   WRITER THREAD [" + i + "]: STARTED", 1, 0, System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                //}
                //catch (Exception e)
                //{
                //    throw e;
                //}
                //finally
                //{
                //    Logger.Release();
                //}
                
                // Check for any errors on the thread.
                if (threadEx != null)
                {
                    throw threadEx;
                }
            }

            // Wait for the reader threads to finish.
            for (int i = 0; i < ReaderCount; i++)
            {
                reader_threads[i].Join();

                try
                {
                    Logger.Aquire();
                    Logger.Write("DataLoader.ProcessFile", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                    Logger.Write("DataLoader.ProcessFile", string.Format("   READER THREAD [" + i + "]: {0}", DataLoader.StopThreads ? "TERMINATED" : "COMPLETED"), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    Logger.Release();
                }
            }

            // The threads reading the file are finished so we can release the file.
            file_stream.Close();
            file_stream.Dispose();
            file_stream = null;
            FileStreamReader.Close();
            FileStreamReader.Dispose();
            FileStreamReader = null;

            // Wait for the loader thread to finish.
            loader_thread.Join();

            try
            {
                Logger.Aquire();
                Logger.Write("DataLoader.ProcessFile", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.Write("DataLoader.ProcessFile", string.Format("       LOADER THREAD: {0}", DataLoader.StopThreads ? "TERMINATED" : "COMPLETED"), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                Logger.Write("DataLoader.ProcessFile", string.Format("       LOADER THREAD: Processed {0} lines", CurrentFileLineBeingRead), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);                
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                Logger.Release();
            }

            LoaderThreadsComplete = true;

            // Wait for the writer threads to finish.
            for (int i = 0; i < WriterFactory.MaxWorkerCount; i++)
            {
                writer_threads[i].Join();

                try
                {
                    Logger.Aquire();
                    Logger.Write("DataLoader.ProcessFile", "", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory); 
                    Logger.Write("DataLoader.ProcessFile", string.Format("   WRITER THREAD [" + i + "]: {0}", DataLoader.StopThreads ? "TERMINATED" : "COMPLETED"), System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    Logger.Release();
                }
            }

            // Check for any errors on the thread.
            if (threadEx != null)
            {
                throw threadEx;
            }
        }

        /// <summary>
        /// Reads a line of data from the current file and parses it into an array. The
        /// array is then loaded on the parsing queue to be processed by the loader threads.
        /// </summary>
        public void ReaderThreads()
        {
            // Index of the current thread.
            int thread_index = -1;

            // Text from the current line in the file.
            string line_text;

            // Contains the parsed data from the current line in the file.
            List<ColumnArrayItem> line_columns;

            long current_file_line_number;

            try
            {
                line_text = null;
                line_columns = new List<ColumnArrayItem>();
                current_file_line_number = 0;

                ReaderThreadPool = ReaderThreadPool + 1;
                thread_index = ReaderThreadPool;

                while (!CurrentFileEOF)
                {
                    try
                    {
                        //Lock access the FileStreamReader.
                        System.Threading.Monitor.Enter(FileLock);

                        // Read the next line of text from the file.
                        line_text = FileStreamReader.ReadLine();

                        if (!string.IsNullOrEmpty(line_text))
                        {
                            // This will keep all other threads in sync on the current line.
                            CurrentFileLineBeingRead = CurrentFileLineBeingRead + 1;

                            // This will keep this particular thread in sync.
                            current_file_line_number = CurrentFileLineBeingRead;
                        }

                        // Check to see if we have reached the end of the file.
                        if (FileStreamReader.EndOfStream)
                        {
                            CurrentFileEOF = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {
                        // Unlock access to the FileStreamReader.
                        System.Threading.Monitor.Exit(FileLock);
                    }

                    if(!string.IsNullOrEmpty(line_text)) // && !string.IsNullOrEmpty(LineTrimSetting))
                    {
                        //switch(LineTrimSetting.ToUpper())
                        //{
                        //    // TODO: ADD CSV CALLS...
                        //}

                        // Parse the text into an array.
                        line_columns = ParseLine(line_text, File.GetFileColumnArray());

                        // Add a column containing the unparsed line to the list.
                        line_columns.Insert(0, new ColumnArrayItem
                        {
                            ColumnName = "FileRowRawData",
                            ColumnValue = line_text
                        });

                        // Add a column containing the line number of the text from the file.
                        line_columns.Insert(0, new ColumnArrayItem
                        {
                            ColumnName = "FileRowNumber",
                            ColumnValue = current_file_line_number.ToString()
                        });

                        // Pass the parsed data to the queue.
                        ParsedQueue.Enqueue(line_columns);

                        line_text = null;
                        line_columns = null;
                    }
                }
            }
            catch (Exception ex)
            {
                //Logger.WriteLine("DataLoader.ProcessFile", "   WRITER THREAD [" + i + "]: COMPLETED", System.Diagnostics.TraceEventType.Information, 2, 0, SharedData.LogCategory);
                throw new Exception(string.Format("{0}", ex.Message), ex);
            }
        }

        public void WriterThreads()
        {
            int thread_index;
            Bucket current_bucket = null;

            WriterThreadPool = WriterThreadPool + 1;

            thread_index = WriterThreadPool;

            while (!DataLoader.StopThreads)
            {
                // Get the next bucket from the queue.
                current_bucket = BucketQueue.Dequeue(thread_index) as Bucket;

                if(current_bucket == null)
                {
                    // Check to see if the current file has reached EOF.
                    if(LoaderThreadsComplete && CurrentFileEOF && ParsedQueue.Count <= 0 )
                    {
                        // The parser queue and the bucket queue are empty and the current file
                        // has reached EOF. Exit the thread.
                        break;
                    }
                    else
                    {
                        // The queue is empty. Sleep a moment and try again.
                        Thread.Sleep(10);
                    }
                }
                else
                {
                    // Execute each database command defined in the writer.
                    if(WriterFactory.BaseWriter.StoredProcedureContainer.Count > 0)
                    {
                        for (int i = 0; i < WriterFactory.BaseWriter.StoredProcedureContainer.Items.Count; i++)
                        {
                            // HACK THIS TOGETHER BY ADDING THE SINGLE VARCHAR VALUES FROM THE ARRAY COMMANDS TO THE
                            // STRING ARRAY COLLECTION.
                            //
                            // THIS SHOULD BE REMOVED LATER ONCE DATABASE LAYER IS UPDATED TO SUPPORT ACCEPTING ARRAYS
                            // OF VARCHAR INSTEAD OF JUST SINGLE VARCHAR VALUES.
                            List<StringCommand> hack = new List<StringCommand>();
                            foreach(ArrayCommand ac in current_bucket.GetArrayCommands())
                            {
                                hack.Add(new StringCommand { Name = ac.Name, Value = ac.Value != null && ac.Value.Count > 0 ? ac.Value[0] : "" });
                            }

                            hack.AddRange(current_bucket.StringCommands);

                            //WriterFactory.BaseWriter.StoredProcedureContainer.Items[i] = new DatabaseCommand(thread_index, WriterFactory.BaseWriter.StoredProcedureContainer.Items[i], SharedData, current_bucket.GetArrayCommands(), current_bucket.StringCommands, null, current_bucket.RowPointer);
                            WriterFactory.BaseWriter.StoredProcedureContainer.Items[i] = new DatabaseCommand(thread_index, this, WriterFactory.BaseWriter.StoredProcedureContainer.Items[i], SharedData, current_bucket.GetArrayCommands(), hack, DrivingData, current_bucket.RowPointer);
                            WriterFactory.BaseWriter.StoredProcedureContainer.Items[i].Execute(false);

                            if (WriterFactory.BaseWriter.StoredProcedureContainer.Items[i].LastError != null)
                            {
                                //throw WriterFactory.BaseWriter.StoredProcedureContainer.Items[i].LastError;
                            }
                        }
                    }

                    current_bucket = null;
                }
            }
        }

        public void LoaderThread()
        {
            Bucket current_bucket;
            List<ColumnArrayItem> file_row_columns;

            current_bucket = new Bucket(BaseBucket);

            while (!DataLoader.StopThreads)
            {                
                try
                {
                    // Get the next item in the queue.
                    file_row_columns = ParsedQueue.Dequeue(0) as List<ColumnArrayItem>;

                    if (file_row_columns == null)
                    {
                        // Check to see if the current file has readed EOF.
                        if (FileStreamReader == null && ParsedQueue.Count <= 0)
                        {
                            // The parser queue is empty and the current file
                            // has reached EOF. Stop loading data.
                            break;
                        }
                        else
                        {
                            // The queue is empty. Sleep a moment and try again.
                            System.Threading.Thread.Sleep(10);
                        }
                    }
                    else
                    {
                        if (current_bucket.Load(ModuleCommands, file_row_columns))
                        {
                            // If the current bucket is full or we are at the endo of the file, load the bucket
                            // into the queue.
                            if (current_bucket.IsFull || (FileStreamReader == null && ParsedQueue.Count <= 0))
                            {
                                BucketQueue.Enqueue(current_bucket);
                                current_bucket = null;

                                if (!CurrentFileEOF || ParsedQueue.Count > 0)
                                {
                                    current_bucket = new Bucket(BaseBucket);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {                    
                    throw ex;
                }

            };

            if(current_bucket != null && current_bucket.RowPointer > 0)
            {
                BucketQueue.Enqueue(current_bucket);
                current_bucket = null;
            }
        }

        public List<StringCommand> GetStringCommands()
        {
            return ModuleCommands;
        }

        public List<ColumnArrayItem> ParseLine(string file_row, List<ColumnArrayItem> columns)
        {
            List<string> row_array = null;

            // TODO: SUPPORT TRIMING WHITESPACE FROM VALUES AND CSV PARSING ROUTINE.
            //string CSV.WhiteSpaces trimmer_setting = CSV.WhiteSpaces.NoTrim;
            //if(!string.IsNullOrEmpty(TrimSetting)){}
            //row_array = CSV.CsvParse(file_row, columns.Count, trimmer_setting);

            if (!string.IsNullOrEmpty(Delimeter))
            {
                row_array = file_row.Split(new string[] { Delimeter }, StringSplitOptions.None).ToList();

                if (row_array.Count == columns.Count)
                {
                    var column_array = columns.ToArray();

                    for (int i = 0; i < row_array.Count - 1; i++)
                    {
                        column_array[i].ColumnValue = row_array[i];
                    }

                    return column_array.ToList();
                }
                else
                {
                    if(row_array.Count > columns.Count)
                    {
                        throw new Exception(string.Format("{0} Module '{1}' encountered additional fields while parsing extract {2}. The interface expects {3} fields and found {4}.", GetType().Name, Name, CurrentFile.Name, columns.Count, row_array.Count));
                    }
                    else
                    {
                        throw new Exception(string.Format("{0} Module '{1}' detected missing fields while parsing extract {2}. The interface expects {3} fields and found {4}.", GetType().Name, Name, CurrentFile.Name, columns.Count, row_array.Count));
                    }
                }
            }
            else
            {
                throw new Exception(string.Format("{0} Module '{1}' does not have a delimeter defined.", GetType().Name, Name));
            }
        }
    }

    public class Bucket
    {
        private ConditionContainer conditionContainer;
        private CacheColumnCollection columnCollection;
        private List<BucketColumn> bucketShell;
        private List<StringCommand> stringCommands;

        [XmlAttributeAttribute(AttributeName = "MaxRowCount")]
        public int MaxRowCount { get; set; }
        public int RowPointer { get; set; }

        [XmlElement(ElementName = "Columns")]
        public CacheColumnCollection ColumnContainer
        {
            get
            {
                if (columnCollection == null)
                    columnCollection = new CacheColumnCollection();

                return columnCollection;
            }
            set
            {
                columnCollection = value;
            }
        }

        [XmlElement(ElementName = "Conditions")]
        public ConditionContainer ConditionContainer
        {
            get
            {
                if (conditionContainer == null)
                    conditionContainer = new ConditionContainer();

                return conditionContainer;
            }
            set
            {
                conditionContainer = value;
            }
        }

        public List<BucketColumn> BucketShell
        {
            get
            {
                if (bucketShell == null)
                    bucketShell = new List<BucketColumn>();

                return bucketShell;
            }
            set
            {
                bucketShell = value;
            }
        }

        public List<StringCommand> StringCommands
        {
            get
            {
                if (stringCommands == null)
                    stringCommands = new List<StringCommand>();

                return stringCommands;
            }
            set
            {
                stringCommands = value;
            }
        }
        
        public bool IsFull
        {
            get
            {
                if(RowPointer >= MaxRowCount)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public Bucket()
        {}

        public Bucket(Bucket configuration)
        {
            BucketColumn item;

            MaxRowCount         = configuration.MaxRowCount;
            ColumnContainer     = configuration.ColumnContainer;
            ConditionContainer  = configuration.ConditionContainer;

            foreach(CacheColumn column in ColumnContainer.Items)
            {
                item = new BucketColumn();
                item.ColumnName = column.Name;
                item.ColumnArray = new List<string>();

                BucketShell.Add(item);
            }
        }

        private void AddRow(ColumnArrayItem[] column_values)
        {
            foreach(BucketColumn column in BucketShell)
            {
                foreach(ColumnArrayItem item in column_values)
                {
                    if(item.ColumnName == column.ColumnName)
                    {
                        //column.ColumnArray[RowPointer] = item.ColumnValue;
                        column.ColumnArray.Add(item.ColumnValue);
                    }
                }
            }

            RowPointer = RowPointer + 1;
        }

        public List<ArrayCommand> GetArrayCommands()
        {
            List<ArrayCommand> command_list = new List<ArrayCommand>();
            ArrayCommand command;

            for (int i = 0; i < ColumnContainer.Length; i++)
            {
                //if (ColumnContainer.Items[i].DataType.ToUpper() == "ARRAY")
                //{
                    command = new ArrayCommand();
                    command.Name = "%" + ColumnContainer.Items[i].Name + "%";

                    foreach (BucketColumn column in BucketShell)
                    {
                        if (("%" + column.ColumnName + "%") == command.Name)
                        {
                            command.Value = column.ColumnArray;
                            break;
                        }
                    }

                    command_list.Add(command);
                //}
            }

            return command_list;
        }

        public ColumnArrayItem[] NewRow()
        {
            List<ColumnArrayItem> columns = new List<ColumnArrayItem>();

            for (int i = 0; i < ColumnContainer.Length; i++ )
            {
                columns.Add(new ColumnArrayItem
                {
                    ColumnName = ColumnContainer.Items[i].Name
                });
            }

            return columns.ToArray();
        }

        public bool Load(List<StringCommand> commands, List<ColumnArrayItem> file_row_columns)
        {
            ColumnArrayItem[] bucket_row = null;
            StringCommands = commands;

            // Create a new instance of a bucket ro ith the values nulled out.
            bucket_row = NewRow();

            // Match values to the file array columns.
            foreach(CacheColumn dc in ColumnContainer.Items)
            {
                // Check to see if the buckets column is referencing a command.
                if(dc.Value.StartsWith("%") && dc.Value.EndsWith("%"))
                {
                    // Check to see if the command is referencing the value of a file column.
                    foreach (ColumnArrayItem fc in file_row_columns)
                    {
                        // Try to match the value of the data column to the name of the file column.
                        if(fc.ColumnName == dc.Value.Remove(dc.Value.Length - 1, 1).Remove(0, 1))
                        {
                            for (int i = 0; i < bucket_row.Length; i++)
                            {
                                // Match the column in the new bucket row to the current bucket column.
                                if (bucket_row[i].ColumnName == dc.Name)
                                {
                                    // Copy the value of the file column to the new bucket rows column.
                                    bucket_row[i].ColumnValue = fc.ColumnValue;
                                    break;
                                }
                            }

                            break;
                        }
                    }
                }
                else
                {
                    throw new Exception("Add bucket columns must reference a command.");
                }
            }

            // Check for null values in the bucket row columns.
            // If any are found see if they reference any string commands.
            for (int i = 0; i < bucket_row.Length; i++)
            {
                if(string.IsNullOrEmpty(bucket_row[i].ColumnValue))
                {
                    for(int c = 0; c < ColumnContainer.Items.Count - 1; c++)
                    {
                        if(ColumnContainer.Items[c].Name == bucket_row[i].ColumnName)
                        {
                            for(int cmd = 0; cmd < StringCommands.Count - 1; cmd++)
                            {
                                if(ColumnContainer.Items[c].Value == StringCommands[cmd].Name)
                                {
                                    bucket_row[i].ColumnValue = StringCommands[cmd].Value;
                                }
                            }
                        }
                    }
                }
            }

            file_row_columns = null;

            // Make sure the row meets the buckets condition criteria.
            if (ConditionCheck(bucket_row))
            {
                // Add the row to the bucket.
                AddRow(bucket_row);

                return true;
            }
            else
            {
                return false;
            }
        }

        protected bool ConditionCheck(ColumnArrayItem[] row)
        {
            if(ConditionContainer != null && ConditionContainer.Count > 0)
            {
                foreach(Condition con in ConditionContainer.Items)
                {
                    // Search for the column defined in the condition.
                    for(int i = 0; i < row.Length; i++)
                    {
                        if(row[i].ColumnName == con.Column)
                        {
                            // Compare the value against the condition.
                            if(con.Bool != string.Equals(row[i].ColumnValue, con.Value))
                            {
                                return false;
                            }

                            // We met the requirements for this condition.
                            break;
                        }
                        else
                        {
                            // If the column listed in the condition is not found in the bucket row then throw an error.
                            if(i >= row.Length)
                            {
                                throw new Exception("Column [" + con.Column + "] listed in condition is not found in the bucket row.");
                            }
                        }
                    }
                }
            }

            return true;
        }
    }

    public struct BucketColumn
    {
        public string ColumnName;
        public List<string> ColumnArray;
    }

    public class ConditionContainer
    {
        private List<Condition> items;

        [XmlElement(ElementName = "Condition")]
        public List<Condition> Items
        {
            get
            {
                if (items == null)
                    items = new List<Condition>();

                return items;
            }
            set
            {
                items = value;
            }
        }

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

    public class Condition
    {
        [XmlAttributeAttribute(AttributeName = "Column")]
        public string Column { get; set; }

        [XmlAttributeAttribute(AttributeName = "Value")]
        public string Value { get; set; }

        [XmlAttributeAttribute(AttributeName = "Boolean")]
        public bool Bool { get; set; }
    }

    public class WriterFactory
    {
        [XmlAttributeAttribute(AttributeName = "MaxWorkerCount")]
        public int MaxWorkerCount { get; set; }

        [XmlElement(ElementName = "Writer")]
        public Writer BaseWriter { get; set; }
    }

    public class Writer
    {
        private DatabaseCommandCollection storedProcedureContainer;

        [XmlElement(ElementName = "Commands")]
        public DatabaseCommandCollection StoredProcedureContainer
        {
            get
            {
                if (storedProcedureContainer == null)
                    storedProcedureContainer = new DatabaseCommandCollection();

                return storedProcedureContainer;
            }
            set
            {
                storedProcedureContainer = value;
            }
        }
    }
    
    public class DataQueue : System.Collections.Queue
    {
        int maxCount = 10;
        Cache sharedData;

        public int MaxCount
        {
            get
            {
                return maxCount;
            }
            set
            {
                maxCount = value;
            }
        }

        public bool IsFull
        {
            get
            {
                if(Count >= maxCount)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public DataQueue() : base()
        {

        }

        public DataQueue(Cache shared_data, int max_count) : base()
        {
            maxCount   = max_count;
            sharedData = shared_data;
        }

        public object Dequeue(int thread_id)
        {
            try
            {
                Monitor.Enter(this);

                if (Count > 0)
                {
                    return base.Dequeue();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Monitor.Exit(this);
            }
        }

        public override void Enqueue(object obj)
        {
            while (!DataLoader.StopThreads)
            {
                try
                {
                    Monitor.Enter(this);

                    if (!IsFull)
                    {
                        base.Enqueue(obj);
                        break;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }
        }
    }
}
